using System;
using System.Collections.Generic;
using LagCompensation.Domain.Movement;
using LagCompensation.Domain.Network;

namespace LagCompensation.Application
{
    /// <summary>
    /// One player's game, on their own machine: their own character, which moves instantly, and
    /// what they know of everybody else, which is only what the server has sent them (late).
    /// </summary>
    public sealed class GameClient
    {
        readonly Dictionary<string, PoseHistory> others = new Dictionary<string, PoseHistory>();
        Route route;
        string aimTarget;

        public string Name { get; }

        /// <summary>The own character, where it really is: no delay on your own screen.</summary>
        public Pose LocalPose { get; private set; }

        /// <summary>Round trip to the server, in seconds. Each way is half of it.</summary>
        public double Ping { get; private set; }

        /// <summary>Draw the others moving smoothly between snapshots (true) or jump to the newest one (false).</summary>
        public bool Interpolate { get; private set; } = true;

        /// <summary>How far in the past the others are drawn on purpose, so there are always two
        /// snapshots to move between. Usually two snapshots' worth.</summary>
        public double InterpolationDelay { get; private set; } = 0.1;

        /// <summary>The connection to the server and back. Read-only outside: for the timeline view.</summary>
        public NetworkLink<PoseUpdate> ToServer { get; } = new NetworkLink<PoseUpdate>();
        public NetworkLink<ShotCommand> ShotsToServer { get; } = new NetworkLink<ShotCommand>();
        public NetworkLink<Snapshot> FromServer { get; } = new NetworkLink<Snapshot>();

        public GameClient(string name, Pose pose)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            LocalPose = pose;
        }

        public void SetPing(double seconds)
        {
            if (seconds < 0.0) throw new ArgumentOutOfRangeException(nameof(seconds), "Ping cannot be negative.");
            Ping = seconds;
            ToServer.SetLatency(seconds / 2.0);
            ShotsToServer.SetLatency(seconds / 2.0);
            FromServer.SetLatency(seconds / 2.0);
        }

        public void SetInterpolation(bool enabled, double delaySeconds)
        {
            if (delaySeconds < 0.0) throw new ArgumentOutOfRangeException(nameof(delaySeconds), "The delay cannot be negative.");
            Interpolate = enabled;
            InterpolationDelay = delaySeconds;
        }

        /// <summary>The own character walks this route from now on (null: stands still). It jumps
        /// straight to where the route says it is at <paramref name="now"/>.</summary>
        public void SetRoute(Route newRoute, double now)
        {
            route = newRoute;
            MoveLocalPlayer(now);
        }

        /// <summary>
        /// The moment of the server's game this client is drawing the others at. Snapshots arrive
        /// half a ping late, and with interpolation the client looks a little further back still.
        /// </summary>
        public double RenderTime(double now) => now - Ping / 2.0 - (Interpolate ? InterpolationDelay : 0.0);

        /// <summary>Where this client sees another player at <paramref name="now"/>, if the server has told it yet.</summary>
        public bool TryGetSeen(string player, double now, out Pose pose) => TryGetSeen(player, now, out pose, out _);

        /// <summary>
        /// Same, and also <paramref name="viewTime"/>: which moment of the server's game that is.
        /// It is what a shot tells the server, so the server can rewind to exactly this.
        /// </summary>
        public bool TryGetSeen(string player, double now, out Pose pose, out double viewTime)
        {
            pose = default;
            viewTime = 0.0;
            if (!others.TryGetValue(player, out var history)) return false;
            if (Interpolate)
            {
                viewTime = RenderTime(now);
                return history.TrySample(viewTime, out pose);
            }

            // No interpolation: the newest snapshot, as is (it jumps from photo to photo).
            if (!history.TryGetLatest(out var latest)) return false;
            pose = latest.Pose;
            viewTime = latest.Time;
            return true;
        }

        /// <summary>The snapshots received of another player (for the ghost views).</summary>
        public bool TryGetHistory(string player, out PoseHistory history) => others.TryGetValue(player, out history);

        /// <summary>The own character keeps turned towards another player, as this client sees it
        /// (where it aims). Null: keeps facing the same way.</summary>
        public void SetAimTarget(string player) => aimTarget = player;

        internal void MoveLocalPlayer(double now)
        {
            if (route != null) LocalPose = route.PoseAt(now);
            else if (aimTarget != null && TryGetSeen(aimTarget, now, out var target))
                LocalPose = LocalPose.FacingTowards(target.Position);
        }

        internal void SendPose(double now) => ToServer.Send(new PoseUpdate(Name, now, LocalPose), now);

        internal void SendShot(Domain.Hit.Ray ray, double viewTime, double now) =>
            ShotsToServer.Send(new ShotCommand(Name, now, viewTime, ray), now);

        /// <summary>Keeps the snapshots that have arrived, each one at the server time it was taken.</summary>
        internal void ReceiveSnapshots(double now)
        {
            foreach (var snapshot in FromServer.Receive(now))
            foreach (var pair in snapshot.Poses)
            {
                if (pair.Key == Name) continue;
                if (!others.TryGetValue(pair.Key, out var history)) others[pair.Key] = history = new PoseHistory();
                history.Add(snapshot.Time, pair.Value);
            }
        }
    }
}
