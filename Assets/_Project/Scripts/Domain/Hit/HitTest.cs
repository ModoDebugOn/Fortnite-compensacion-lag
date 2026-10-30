using System;
using System.Numerics;

namespace LagCompensation.Domain.Hit
{
    /// <summary>
    /// Does a shot hit a capsule? The capsule is every point within <c>Radius</c> of its bone (the
    /// segment between its two cap centres), so the shot hits if it passes that close to the bone.
    /// </summary>
    public static class HitTest
    {
        /// <summary>
        /// True if <paramref name="ray"/> hits <paramref name="capsule"/>. <paramref name="miss"/> is how
        /// far the shot passed from the bone (0 = dead centre); it hits while that is under the radius.
        /// </summary>
        public static bool RayCapsule(Ray ray, Capsule capsule, out float miss, out float distance)
        {
            ClosestPoints(ray.Origin, ray.PointAt(ray.Range), capsule.Bottom, capsule.Top, out float s, out var onRay, out var onBone);
            miss = Vector3.Distance(onRay, onBone);
            distance = s * ray.Range;
            return miss <= capsule.Radius;
        }

        /// <summary>
        /// True if <paramref name="ray"/> hits <paramref name="box"/> within its range;
        /// <paramref name="distance"/> is how far along the ray it enters the box.
        /// </summary>
        public static bool RayBox(Ray ray, Box box, out float distance)
        {
            // In the box's own space the box is axis-aligned: the classic "slab" test. The ray is
            // inside the box between the moment it has crossed all three pairs of faces and the
            // moment it leaves the first pair.
            var origin = box.ToLocal(ray.Origin);
            var direction = box.DirectionToLocal(ray.Direction);
            float enter = 0f, leave = ray.Range;
            distance = 0f;

            for (int axis = 0; axis < 3; axis++)
            {
                float o = Component(origin, axis), d = Component(direction, axis), half = Component(box.HalfSize, axis);
                if (MathF.Abs(d) < 1e-8f)
                {
                    if (o < -half || o > half) return false;   // parallel to these faces and outside them
                    continue;
                }
                float t1 = (-half - o) / d, t2 = (half - o) / d;
                if (t1 > t2) (t1, t2) = (t2, t1);
                enter = MathF.Max(enter, t1);
                leave = MathF.Min(leave, t2);
                if (enter > leave) return false;
            }
            distance = enter;
            return true;
        }

        static float Component(Vector3 v, int axis) => axis == 0 ? v.X : axis == 1 ? v.Y : v.Z;

        /// <summary>
        /// Closest points between segments p1-q1 and p2-q2 (Ericson, "Real-Time Collision Detection",
        /// 5.1.9). <paramref name="s"/> is how far along the first segment (0-1) its closest point is.
        /// </summary>
        static void ClosestPoints(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2,
                                  out float s, out Vector3 c1, out Vector3 c2)
        {
            const float Epsilon = 1e-8f;
            var d1 = q1 - p1;
            var d2 = q2 - p2;
            var r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float t;

            if (a <= Epsilon && e <= Epsilon) { s = t = 0f; }
            else if (a <= Epsilon) { s = 0f; t = Math.Clamp(f / e, 0f, 1f); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= Epsilon) { t = 0f; s = Math.Clamp(-c / a, 0f, 1f); }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom > Epsilon ? Math.Clamp((b * f - c * e) / denom, 0f, 1f) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = Math.Clamp(-c / a, 0f, 1f); }
                    else if (t > 1f) { t = 1f; s = Math.Clamp((b - c) / a, 0f, 1f); }
                }
            }
            c1 = p1 + d1 * s;
            c2 = p2 + d2 * t;
        }
    }
}
