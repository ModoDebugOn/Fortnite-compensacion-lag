using UnityEngine;
using Pose = LagCompensation.Domain.Movement.Pose;

namespace LagCompensation.Presentation.Players
{
    /// <summary>
    /// Puts a character (or a ghost of it) where a pose says. It only reads: whoever binds it
    /// decides which version it shows: the real one, the server's, or what another player sees.
    /// While the pose is unknown (null) it hides.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        System.Func<Pose?> source;
        Renderer[] renderers;

        /// <summary>The pose to show every frame (null = not known yet, hidden).</summary>
        public void Bind(System.Func<Pose?> pose) => source = pose;

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();

            // Every piece in the character's layer (its screen's version): an accessory added to
            // the outfit later arrives in Default, and would show up on every screen.
            foreach (var piece in GetComponentsInChildren<Transform>(true)) piece.gameObject.layer = gameObject.layer;
        }

        void LateUpdate()
        {
            if (source == null) return;
            var pose = source();
            SetVisible(pose.HasValue);
            if (!pose.HasValue) return;

            transform.SetPositionAndRotation(pose.Value.Position.ToUnity(), Quaternion.LookRotation(pose.Value.Forward.ToUnity()));
        }

        void SetVisible(bool visible)
        {
            foreach (var r in renderers) r.enabled = visible;
        }
    }
}
