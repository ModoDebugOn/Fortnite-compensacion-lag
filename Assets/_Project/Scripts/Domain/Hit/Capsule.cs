using System.Numerics;
using LagCompensation.Domain.Movement;

namespace LagCompensation.Domain.Hit
{
    /// <summary>
    /// The shape a shot can hit: a cylinder with round caps, standing up. It is exactly Unity's
    /// capsule (2 m tall, 0.5 m radius), the same one the characters are made of.
    /// </summary>
    public readonly struct Capsule
    {
        public const float StandardHeight = 2f;
        public const float StandardRadius = 0.5f;

        /// <summary>Centres of the bottom and top caps (the capsule's "bone").</summary>
        public readonly Vector3 Bottom, Top;
        public readonly float Radius;

        public Capsule(Vector3 bottom, Vector3 top, float radius)
        {
            Bottom = bottom;
            Top = top;
            Radius = radius;
        }

        /// <summary>The capsule of a player standing at <paramref name="pose"/> (its centre).</summary>
        public static Capsule Of(Pose pose, float height = StandardHeight, float radius = StandardRadius)
        {
            var halfBone = new Vector3(0f, height / 2f - radius, 0f);
            return new Capsule(pose.Position - halfBone, pose.Position + halfBone, radius);
        }
    }

    /// <summary>A shot: from a point, in a direction, up to a maximum range.</summary>
    public readonly struct Ray
    {
        public readonly Vector3 Origin;
        public readonly Vector3 Direction;   // unit length
        public readonly float Range;

        public Ray(Vector3 origin, Vector3 direction, float range = 200f)
        {
            Origin = origin;
            Direction = Vector3.Normalize(direction);
            Range = range;
        }

        public Vector3 PointAt(float distance) => Origin + Direction * distance;
    }
}
