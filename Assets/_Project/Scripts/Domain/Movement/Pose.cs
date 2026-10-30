using System.Numerics;

namespace LagCompensation.Domain.Movement
{
    /// <summary>Where a player is and which way it faces (on the ground plane, Y = 0).</summary>
    public readonly struct Pose
    {
        /// <summary>Centre of the player's capsule, in metres.</summary>
        public readonly Vector3 Position;

        /// <summary>Unit direction it faces, flat on the ground.</summary>
        public readonly Vector3 Forward;

        public Pose(Vector3 position, Vector3 forward)
        {
            Position = position;
            Forward = forward;
        }

        /// <summary>The same position, turned to face <paramref name="point"/> (on the ground plane).
        /// If the point is right on top, it keeps facing the same way.</summary>
        public Pose FacingTowards(Vector3 point)
        {
            var flat = new Vector3(point.X - Position.X, 0f, point.Z - Position.Z);
            return flat.LengthSquared() < 1e-8f ? this : new Pose(Position, Vector3.Normalize(flat));
        }

        public override string ToString() => $"{Position} facing {Forward}";
    }
}
