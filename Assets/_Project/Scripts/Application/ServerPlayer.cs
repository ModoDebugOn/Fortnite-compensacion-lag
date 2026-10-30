using LagCompensation.Domain.Movement;

namespace LagCompensation.Application
{
    /// <summary>
    /// A player as the server knows it: the last position its client reported, which already
    /// arrived late (half the player's ping). This is the version that counts for the rules.
    /// </summary>
    public sealed class ServerPlayer
    {
        public string Name { get; }

        public Pose Pose { get; internal set; }

        /// <summary>When that pose was true on the player's machine.</summary>
        public double PoseTime { get; internal set; } = double.NegativeInfinity;

        /// <summary>
        /// The server's album: where this player was in every snapshot the server sent, with the
        /// snapshot's time. Exactly what the clients received, so the server can rewind to any of it.
        /// </summary>
        public PoseHistory History { get; } = new PoseHistory(length: 2.0);

        public ServerPlayer(string name, Pose pose)
        {
            Name = name;
            Pose = pose;
        }

        public override string ToString() => $"{Name} at {Pose}";
    }
}
