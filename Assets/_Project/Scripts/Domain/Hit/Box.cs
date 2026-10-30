using System;
using System.Numerics;

namespace LagCompensation.Domain.Hit
{
    /// <summary>
    /// Something solid that stops shots: a wall, a crate. A box standing on the ground, which can be
    /// turned around the vertical axis (walls are built at any angle), but never tilted.
    /// </summary>
    public readonly struct Box
    {
        public readonly Vector3 Center;
        public readonly Vector3 HalfSize;

        /// <summary>Turn around the vertical axis, in degrees (Unity's Y rotation).</summary>
        public readonly float Yaw;

        public Box(Vector3 center, Vector3 size, float yaw)
        {
            Center = center;
            HalfSize = size / 2f;
            Yaw = yaw;
        }

        /// <summary>A point in the box's own space: centre at 0, axes along its sides.</summary>
        public Vector3 ToLocal(Vector3 point) => Rotate(point - Center, -Yaw);

        public Vector3 DirectionToLocal(Vector3 direction) => Rotate(direction, -Yaw);

        /// <summary>Rotates around the vertical axis like Unity does (clockwise seen from above).</summary>
        static Vector3 Rotate(Vector3 v, float degrees)
        {
            float r = degrees * MathF.PI / 180f;
            float cos = MathF.Cos(r), sin = MathF.Sin(r);
            return new Vector3(v.X * cos + v.Z * sin, v.Y, -v.X * sin + v.Z * cos);
        }
    }
}
