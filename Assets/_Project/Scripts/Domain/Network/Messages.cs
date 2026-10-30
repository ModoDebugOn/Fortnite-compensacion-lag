using System.Collections.Generic;
using LagCompensation.Domain.Hit;
using LagCompensation.Domain.Movement;

namespace LagCompensation.Domain.Network
{
    /// <summary>Client → server: "this is where I am", stamped with the time it was true.</summary>
    public readonly struct PoseUpdate
    {
        public readonly string Player;
        public readonly double Time;
        public readonly Pose Pose;

        public PoseUpdate(string player, double time, Pose pose)
        {
            Player = player;
            Time = time;
            Pose = pose;
        }
    }

    /// <summary>
    /// Client → server: "I fired, from here, in this direction", stamped with when, and with the
    /// moment of the server's game the shooter was *seeing* when it fired. That second time is what
    /// lets the server rewind to exactly what the shooter saw.
    /// </summary>
    public readonly struct ShotCommand
    {
        public readonly string Shooter;
        public readonly double FiredAt;
        public readonly double ViewTime;
        public readonly Ray Ray;

        public ShotCommand(string shooter, double firedAt, double viewTime, Ray ray)
        {
            Shooter = shooter;
            FiredAt = firedAt;
            ViewTime = viewTime;
            Ray = ray;
        }
    }

    /// <summary>Server → clients: a photo of where everybody is, as the server knows it at <see cref="Time"/>.</summary>
    public sealed class Snapshot
    {
        public double Time { get; }
        public IReadOnlyDictionary<string, Pose> Poses { get; }

        public Snapshot(double time, IReadOnlyDictionary<string, Pose> poses)
        {
            Time = time;
            Poses = poses;
        }
    }
}
