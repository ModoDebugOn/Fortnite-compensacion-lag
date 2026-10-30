using UnityEngine;

namespace LagCompensation.Infrastructure
{
    /// <summary>
    /// One stop of a scripted route, placed in the scene so the route can be edited by dragging.
    /// The stops of a route are the children of one object, in order; the last one loops back to the first.
    /// </summary>
    public sealed class RouteStopMarker : MonoBehaviour
    {
        [Tooltip("Seconds standing here before running to the next stop.")]
        [SerializeField, Min(0f)] float wait = 1f;

        public float Wait => wait;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, 0.5f);

            var parent = transform.parent;
            if (parent == null || parent.childCount < 2) return;
            var next = parent.GetChild((transform.GetSiblingIndex() + 1) % parent.childCount);
            Gizmos.DrawLine(transform.position, next.position);
        }
    }
}
