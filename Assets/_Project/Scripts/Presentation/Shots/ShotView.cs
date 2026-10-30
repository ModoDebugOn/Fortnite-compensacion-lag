using System.Collections.Generic;
using LagCompensation.Application;
using LagCompensation.Presentation.Screens;
using UnityEngine;

namespace LagCompensation.Presentation.Shots
{
    /// <summary>
    /// Shows what the server decided about each shot, as soon as it decides:
    /// - The shot's line (green = hit, red = miss), fading out. Visible on every screen.
    /// - Where the server had the target when it checked the shot: a ghost in the same colour,
    ///   only on the server's screen. That is what the shot was really compared with.
    /// - "¡IMPACTO!" / "FALLO" on the screens that care.
    /// </summary>
    public sealed class ShotView : MonoBehaviour
    {
        [SerializeField] Color hitColor = new Color(0.2f, 1f, 0.35f);
        [SerializeField] Color missColor = new Color(1f, 0.25f, 0.2f);
        [Tooltip("A shot that would have hit, but a wall stopped it.")]
        [SerializeField] Color wallColor = new Color(0.85f, 0.85f, 0.85f);
        [Tooltip("Seconds (real time) a shot stays on screen.")]
        [SerializeField, Range(0.2f, 5f)] float showFor = 1.5f;
        [SerializeField, Range(0.01f, 0.2f)] float lineWidth = 0.06f;

        [Header("Checked target")]
        [Tooltip("Ghost capsule placed where the server had the target when checking the shot.")]
        [SerializeField] GameObject checkedTargetPrefab;
        [Tooltip("Layer of that ghost: the server's screen.")]
        [SerializeField] string checkedTargetLayer = "Screen Server";

        [Header("Messages")]
        [SerializeField] GameScreen[] announceOn;

        System.Func<IReadOnlyList<ShotResult>> source;
        System.Func<bool> paused;
        ShotResult lastShown;
        readonly List<(LineRenderer line, float bornAt)> lines = new List<(LineRenderer, float)>();
        float clock;   // seconds the lines have been on screen: it stops while the simulation is paused
        GameObject checkedTarget;
        Material lineMaterial;

        /// <summary>The shots to show, and whether the simulation is paused: while paused nothing
        /// fades, so a frozen impact keeps its line and its message.</summary>
        public void Bind(System.Func<IReadOnlyList<ShotResult>> shots, System.Func<bool> isPaused = null)
        {
            source = shots;
            paused = isPaused;
        }

        void Awake() => lineMaterial = new Material(Shader.Find("Sprites/Default"));

        void LateUpdate()
        {
            if (paused != null && paused())
                foreach (var screen in announceOn) screen.ExtendFlash(Time.unscaledDeltaTime);   // the message stays
            else
                clock += Time.unscaledDeltaTime;
            ShowNewShots();
            Fade();
        }

        void ShowNewShots()
        {
            var shots = source?.Invoke();
            if (shots == null || shots.Count == 0) return;

            // Everything after the last one shown is new.
            int first = 0;
            for (int i = shots.Count - 1; i >= 0; i--)
                if (shots[i] == lastShown) { first = i + 1; break; }
            for (int i = first; i < shots.Count; i++) Show(shots[i]);
            lastShown = shots[shots.Count - 1];
        }

        void Show(ShotResult shot)
        {
            var color = shot.Hit ? hitColor : shot.BlockedByWall ? wallColor : missColor;

            var line = new GameObject("Shot").AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = lineMaterial;
            line.widthMultiplier = lineWidth;
            line.positionCount = 2;
            line.SetPosition(0, shot.Ray.Origin.ToUnity());
            line.SetPosition(1, shot.Ray.PointAt(shot.Ray.Range).ToUnity());
            line.startColor = line.endColor = color;
            lines.Add((line, clock));

            PlaceCheckedTarget(shot, color);
            string verdict = shot.Hit ? "¡IMPACTO!"
                           : shot.BlockedByWall ? "PARED"
                           : shot.RewindLimited ? "FALLO: DEMASIADO LAG"
                           : "FALLO";
            foreach (var screen in announceOn) screen.Flash(verdict, color);
        }

        void PlaceCheckedTarget(ShotResult shot, Color color)
        {
            if (checkedTargetPrefab == null) return;
            if (checkedTarget == null)
            {
                checkedTarget = Instantiate(checkedTargetPrefab, transform);
                checkedTarget.name = "Checked target";
                int layer = LayerMask.NameToLayer(checkedTargetLayer);
                foreach (var t in checkedTarget.GetComponentsInChildren<Transform>()) t.gameObject.layer = layer;
                foreach (var tint in checkedTarget.GetComponentsInChildren<Players.GhostTint>()) Destroy(tint);   // the colour is ours
            }
            checkedTarget.SetActive(true);
            checkedTarget.transform.SetPositionAndRotation(shot.TargetPose.Position.ToUnity(),
                                                           Quaternion.LookRotation(shot.TargetPose.Forward.ToUnity()));
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(color.r, color.g, color.b, 0.55f));
            checkedTarget.GetComponent<Renderer>().SetPropertyBlock(block);
        }

        void Fade()
        {
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                float age = (clock - lines[i].bornAt) / showFor;
                var line = lines[i].line;
                if (age >= 1f)
                {
                    Destroy(line.gameObject);
                    lines.RemoveAt(i);
                    continue;
                }
                var c = line.startColor;
                c.a = 1f - age;
                line.startColor = line.endColor = c;
            }
            if (checkedTarget != null && lines.Count == 0) checkedTarget.SetActive(false);
        }
    }
}
