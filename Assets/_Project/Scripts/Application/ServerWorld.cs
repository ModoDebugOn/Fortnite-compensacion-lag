using System;
using System.Collections.Generic;
using LagCompensation.Domain.Hit;
using LagCompensation.Domain.Movement;
using LagCompensation.Domain.Network;
using LagCompensation.Domain.Timing;

namespace LagCompensation.Application
{
    /// <summary>
    /// The match as the server runs it: the version of the game that counts. It hears from each
    /// client late and tells everybody, late again, where everybody is.
    ///
    /// It also owns the clock. In the demo everything shares it; in a real game each machine has its
    /// own clock, kept in sync with the server's.
    /// </summary>
    public sealed class ServerWorld
    {
        readonly List<ServerPlayer> players = new List<ServerPlayer>();
        readonly List<GameClient> clients = new List<GameClient>();
        readonly List<ShotResult> shots = new List<ShotResult>();
        readonly List<Box> walls = new List<Box>();

        /// <summary>How many past shots are remembered (for the views).</summary>
        const int ShotsKept = 20;

        public SimClock Clock { get; }

        /// <summary>When clients send their position and the server sends its snapshots.</summary>
        public TickTimer Tick { get; }

        public IReadOnlyList<ServerPlayer> Players => players;
        public IReadOnlyList<GameClient> Clients => clients;

        /// <summary>The last shots the server has decided on, oldest first.</summary>
        public IReadOnlyList<ShotResult> Shots => shots;

        /// <summary>
        /// Lag compensation on: each shot is checked against the moment the shooter was seeing
        /// (rewinding the others). Off: against where the others are when the shot arrives.
        /// </summary>
        public bool LagCompensation { get; set; } = true;

        /// <summary>
        /// The furthest back the server agrees to rewind, in seconds. A shooter with a huge ping would
        /// otherwise make everybody die far behind walls; past this limit the server only rewinds this
        /// much, which is not enough, and the shot misses.
        /// </summary>
        public double MaxRewind { get; set; } = 1.0;

        public ServerWorld(SimClock clock, double tickRate = 30.0)
        {
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Tick = new TickTimer(tickRate);
        }

        /// <summary>A player joins: their own client, and the server's record of them.</summary>
        public GameClient Connect(string name, Pose pose, double ping)
        {
            var client = new GameClient(name, pose);
            client.SetPing(ping);
            clients.Add(client);
            players.Add(new ServerPlayer(name, pose));
            return client;
        }

        public ServerPlayer FindPlayer(string name) => players.Find(p => p.Name == name);

        /// <summary>Something solid in the map (wall, crate) that stops shots.</summary>
        public void AddWall(Box wall) => walls.Add(wall);

        public IReadOnlyList<Box> Walls => walls;

        /// <summary>Takes in the positions that have arrived from the clients.</summary>
        internal void ReceiveUpdates(double now)
        {
            foreach (var client in clients)
            foreach (var update in client.ToServer.Receive(now))
            {
                var player = FindPlayer(update.Player);
                if (player == null || update.Time < player.PoseTime) continue;   // late and outdated
                player.Pose = update.Pose;
                player.PoseTime = update.Time;
            }
        }

        /// <summary>Decides the shots that have arrived.</summary>
        internal void ResolveShots(double now)
        {
            foreach (var client in clients)
            foreach (var shot in client.ShotsToServer.Receive(now))
                Record(Resolve(shot, now));
        }

        ShotResult Resolve(ShotCommand shot, double now)
        {
            float wall = DistanceToFirstWall(shot.Ray);
            double? rewindTo = RewindTime(shot, now, out bool limited);
            ShotResult best = null;
            foreach (var player in players)
            {
                if (player.Name == shot.Shooter) continue;
                var pose = WhereToCheck(player, rewindTo);
                bool touches = HitTest.RayCapsule(shot.Ray, Capsule.Of(pose), out float miss, out float distance);
                bool blocked = touches && wall < distance;   // a wall in between stops the bullet
                var result = new ShotResult(shot.Shooter, shot.Ray, shot.FiredAt, now, player.Name, pose,
                                            touches && !blocked, miss, rewindTo, blocked, limited);
                if (best == null || (result.Hit && !best.Hit) || (result.Hit == best.Hit && miss < best.Miss)) best = result;
            }
            return best;
        }

        /// <summary>How far the shot travels before hitting a wall (infinity if it hits none).
        /// Walls do not move, so there is nothing to rewind for them.</summary>
        float DistanceToFirstWall(Ray ray)
        {
            float nearest = float.PositiveInfinity;
            foreach (var wall in walls)
                if (HitTest.RayBox(ray, wall, out float distance) && distance < nearest) nearest = distance;
            return nearest;
        }

        /// <summary>True if nothing solid stands between <paramref name="from"/> and <paramref name="to"/>.</summary>
        public bool CanSee(System.Numerics.Vector3 from, System.Numerics.Vector3 to)
        {
            float length = System.Numerics.Vector3.Distance(from, to);
            return length < 1e-4f || DistanceToFirstWall(new Ray(from, to - from, length)) > length;
        }

        /// <summary>
        /// The moment to rewind to: the one the shooter was seeing, but never further back than
        /// <see cref="MaxRewind"/> (then <paramref name="limited"/> is true). Null: no rewinding.
        /// </summary>
        double? RewindTime(ShotCommand shot, double now, out bool limited)
        {
            limited = false;
            if (!LagCompensation) return null;
            double oldestAllowed = now - MaxRewind;
            limited = shot.ViewTime < oldestAllowed;
            return limited ? oldestAllowed : shot.ViewTime;
        }

        /// <summary>
        /// The heart of lag compensation. Without it, the target is where the server has it now.
        /// With it, the server opens its album at the moment the shooter was seeing, and puts the
        /// target back where it was then: exactly what the shooter aimed at.
        /// </summary>
        Pose WhereToCheck(ServerPlayer target, double? rewindTo)
        {
            if (rewindTo.HasValue && target.History.TrySample(rewindTo.Value, out var rewound)) return rewound;
            return target.Pose;
        }

        void Record(ShotResult result)
        {
            if (result == null) return;   // nobody else in the match
            shots.Add(result);
            if (shots.Count > ShotsKept) shots.RemoveAt(0);
        }

        /// <summary>
        /// Sends every client a photo of where everybody is now, as far as the server knows,
        /// and keeps a copy in its album (each player's history) to be able to rewind later.
        /// </summary>
        internal void BroadcastSnapshot(double now)
        {
            var poses = new Dictionary<string, Pose>();
            foreach (var player in players)
            {
                poses[player.Name] = player.Pose;
                player.History.Add(now, player.Pose);
            }
            var snapshot = new Snapshot(now, poses);
            foreach (var client in clients) client.FromServer.Send(snapshot, now);
        }
    }
}
