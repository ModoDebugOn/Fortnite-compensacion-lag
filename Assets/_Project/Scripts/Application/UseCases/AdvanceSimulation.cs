using System;

namespace LagCompensation.Application.UseCases
{
    /// <summary>
    /// One frame of the whole simulation, in the order things happen on a real network:
    /// 1. Each player moves on their own machine (instantly, on their own screen).
    /// 2. On each tick, each client tells the server where it is.
    /// 3. The server takes in whatever has arrived (half a ping late): positions and shots,
    ///    and decides the shots...
    /// 4. ...and on each tick sends everybody a snapshot.
    /// 5. Each client takes in the snapshots that have arrived (another half ping late).
    /// </summary>
    public sealed class AdvanceSimulation
    {
        readonly ServerWorld world;

        public AdvanceSimulation(ServerWorld world)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
        }

        /// <summary>A normal frame: <paramref name="realDeltaSeconds"/> of real time, scaled by the clock.</summary>
        public void Execute(double realDeltaSeconds)
        {
            if (world.Clock.Advance(realDeltaSeconds) > 0.0) Simulate();
        }

        /// <summary>Frame by frame while paused: moves exactly <paramref name="simSeconds"/> of simulation time.</summary>
        public void Step(double simSeconds)
        {
            world.Clock.Step(simSeconds);
            Simulate();
        }

        void Simulate()
        {
            double now = world.Clock.Now;
            bool tick = world.Tick.Due(now);

            foreach (var client in world.Clients)
            {
                client.MoveLocalPlayer(now);
                if (tick) client.SendPose(now);
            }

            world.ReceiveUpdates(now);
            world.ResolveShots(now);
            if (tick) world.BroadcastSnapshot(now);

            foreach (var client in world.Clients)
                client.ReceiveSnapshots(now);
        }
    }
}
