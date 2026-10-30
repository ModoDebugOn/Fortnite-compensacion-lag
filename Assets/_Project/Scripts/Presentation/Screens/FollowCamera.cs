using UnityEngine;
using Pose = LagCompensation.Domain.Movement.Pose;

namespace LagCompensation.Presentation.Screens
{
    /// <summary>
    /// Keeps the camera at a fixed offset from a player and looking at it, smoothly. The offset is
    /// in world space (not behind the player's back), so the camera does not swing when it turns.
    /// </summary>
    [DefaultExecutionOrder(100)]   // after the PlayerViews have moved
    public sealed class FollowCamera : MonoBehaviour
    {
        [Tooltip("Where the camera sits, relative to the player.")]
        [SerializeField] Vector3 offset = new Vector3(-2f, 3.5f, 7f);
        [Tooltip("Point it looks at, relative to the player.")]
        [SerializeField] Vector3 lookOffset = new Vector3(0f, 0.5f, -6f);
        [Tooltip("Seconds to catch up with the player. 0 = glued to it.")]
        [SerializeField, Range(0f, 1f)] float smoothTime = 0.25f;

        System.Func<Pose?> target;
        Vector3 velocity;

        public void Bind(System.Func<Pose?> pose)
        {
            target = pose;
            var current = target();
            if (current.HasValue) Place(current.Value.Position.ToUnity(), snap: true);
        }

        void LateUpdate()
        {
            var pose = target?.Invoke();
            if (pose.HasValue) Place(pose.Value.Position.ToUnity(), snap: false);
        }

        void Place(Vector3 player, bool snap)
        {
            var goal = player + offset;
            transform.position = snap || smoothTime <= 0f
                ? goal
                : Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
            transform.LookAt(player + lookOffset);
        }
    }
}
