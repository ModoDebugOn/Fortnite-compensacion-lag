using System.Collections.Generic;
using LagCompensation.Domain.Movement;
using UnityEngine;
using Pose = LagCompensation.Domain.Movement.Pose;

namespace LagCompensation.Presentation.Players
{
    /// <summary>
    /// Draws a player's history (the server's album) as a trail of fading ghost capsules: where it
    /// was in the last snapshots. The newest are the most solid; the oldest almost vanish.
    /// </summary>
    public sealed class HistoryTrailView : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] GameObject ghostPrefab;
        [Tooltip("Layer the ghosts are drawn on (the server's screen).")]
        [SerializeField] string layer = "Screen Server";
        [SerializeField] Color color = new Color(0.55f, 0.85f, 1f, 0.35f);
        [Tooltip("Seconds of history drawn.")]
        [SerializeField, Range(0.1f, 2f)] float seconds = 0.8f;
        [Tooltip("Draw one sample out of every N (30 snapshots a second is too many ghosts).")]
        [SerializeField, Range(1, 10)] int every = 3;

        System.Func<PoseHistory> source;
        readonly List<Renderer> pool = new List<Renderer>();
        MaterialPropertyBlock block;

        public void Bind(System.Func<PoseHistory> history) => source = history;

        void Awake() => block = new MaterialPropertyBlock();

        void LateUpdate()
        {
            var history = source?.Invoke();
            int used = 0;
            if (history != null && history.Count > 0)
            {
                var samples = history.Samples;
                double newest = samples[samples.Count - 1].Time;
                for (int i = samples.Count - 1; i >= 0; i -= every)
                {
                    double age = newest - samples[i].Time;
                    if (age > seconds) break;
                    Place(Ghost(used++), samples[i].Pose, 1f - (float)(age / seconds));
                }
            }
            for (int i = used; i < pool.Count; i++) pool[i].enabled = false;
        }

        void Place(Renderer ghost, Pose pose, float strength)
        {
            ghost.enabled = true;
            ghost.transform.SetPositionAndRotation(pose.Position.ToUnity(), Quaternion.LookRotation(pose.Forward.ToUnity()));
            block.SetColor(BaseColor, new Color(color.r, color.g, color.b, color.a * strength));
            ghost.SetPropertyBlock(block);
        }

        Renderer Ghost(int index)
        {
            while (pool.Count <= index)
            {
                var ghost = Instantiate(ghostPrefab, transform);
                ghost.name = $"History {pool.Count}";
                int layerIndex = LayerMask.NameToLayer(layer);
                foreach (var t in ghost.GetComponentsInChildren<Transform>()) t.gameObject.layer = layerIndex;
                foreach (var tint in ghost.GetComponentsInChildren<GhostTint>()) Destroy(tint);   // the colour is ours
                pool.Add(ghost.GetComponent<Renderer>());
            }
            return pool[index];
        }
    }
}
