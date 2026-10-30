using System;
using System.Numerics;
using LagCompensation.Domain.Hit;

namespace LagCompensation.Application.UseCases
{
    /// <summary>
    /// A player fires at another one, aiming *perfectly* at where it sees it on its own screen.
    /// The shot then travels to the server like any other message (half a ping).
    ///
    /// Perfect aim is the point: if the shot still misses, it is the lag's fault, not the player's.
    /// </summary>
    public sealed class FireShot
    {
        /// <summary>Shots leave from eye height, half a metre above the capsule's centre.</summary>
        public static readonly Vector3 EyeHeight = new Vector3(0f, 0.5f, 0f);

        readonly ServerWorld world;

        public FireShot(ServerWorld world)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
        }

        /// <summary>False if the shooter does not know where the target is yet (nothing to aim at).</summary>
        public bool Execute(GameClient shooter, string target)
        {
            double now = world.Clock.Now;
            if (!shooter.TryGetSeen(target, now, out var seen, out double viewTime)) return false;

            var origin = shooter.LocalPose.Position + EyeHeight;
            var ray = new Ray(origin, seen.Position - origin);
            shooter.SendShot(ray, viewTime, now);   // "I fired at what I was seeing of moment viewTime"
            return true;
        }
    }
}
