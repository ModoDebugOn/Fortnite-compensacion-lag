using System.Numerics;
using LagCompensation.Application;
using LagCompensation.Application.UseCases;
using LagCompensation.Domain.Movement;
using LagCompensation.Domain.Timing;
using NUnit.Framework;

namespace LagCompensation.Tests
{
    /// <summary>
    /// The numbers from the explanations: Raven runs at 5 m/s, both players have a ping of 80 ms
    /// (40 ms each way).
    /// - Task 1, no interpolation: Peely sees her 40 + 40 = 80 ms late, 40 cm behind.
    /// - Task 2, with 100 ms of interpolation: 40 + 40 + 100 = 180 ms late, 90 cm behind.
    ///
    /// The tick rate is very high here (1000 per second) so the only delays left are the ping and
    /// the interpolation: with a real tick rate the gap also grows by up to one tick.
    /// </summary>
    public class AdvanceSimulationTests
    {
        const double Frame = 0.001;
        const double Ping = 0.08;

        ServerWorld world;
        AdvanceSimulation advance;
        GameClient peely, raven;

        [SetUp]
        public void SetUp()
        {
            world = new ServerWorld(new SimClock(), tickRate: 1000);
            advance = new AdvanceSimulation(world);

            peely = world.Connect("Peely", new Pose(new Vector3(0f, 1f, -15f), Vector3.UnitZ), Ping);
            raven = world.Connect("Raven", new Pose(Vector3.Zero, Vector3.UnitX), Ping);
            peely.SetInterpolation(false, 0.0);
            // Runs 50 m to the right at 5 m/s and waits there 5 s: plenty of time to measure.
            raven.SetRoute(new Route(new[]
            {
                new RouteStop(new Vector3(0f, 1f, 0f), 0.0),
                new RouteStop(new Vector3(50f, 1f, 0f), 5.0),
            }, speed: 5.0), world.Clock.Now);
        }

        void RunFor(double seconds)
        {
            for (double t = 0; t < seconds - 1e-9; t += Frame) advance.Execute(Frame);
        }

        float SeenByPeelyX()
        {
            Assert.IsTrue(peely.TryGetSeen("Raven", world.Clock.Now, out var pose), "Peely has not heard of Raven yet");
            return pose.Position.X;
        }

        [Test]
        public void Each_player_sees_itself_without_delay()
        {
            RunFor(2.0);

            Assert.AreEqual(10f, raven.LocalPose.Position.X, 0.01f);   // 2 s at 5 m/s
        }

        [Test]
        public void The_server_knows_Raven_half_a_ping_late()
        {
            RunFor(2.0);

            float behind = raven.LocalPose.Position.X - world.FindPlayer("Raven").Pose.Position.X;
            Assert.AreEqual(5f * 0.04f, behind, 0.01f);   // 40 ms at 5 m/s = 20 cm
        }

        [Test]
        public void Peely_sees_Raven_both_halves_late()
        {
            RunFor(2.0);

            float behind = raven.LocalPose.Position.X - SeenByPeelyX();
            Assert.AreEqual(5f * 0.08f, behind, 0.01f);   // 80 ms at 5 m/s = 40 cm
        }

        [Test]
        public void Interpolation_adds_its_own_delay_on_top_of_the_ping()
        {
            peely.SetInterpolation(true, 0.1);
            RunFor(2.0);

            float behind = raven.LocalPose.Position.X - SeenByPeelyX();
            Assert.AreEqual(5f * 0.18f, behind, 0.01f);   // 180 ms at 5 m/s = 90 cm
        }

        [Test]
        public void With_few_snapshots_interpolation_moves_every_frame_instead_of_jumping()
        {
            world.Tick.SetRate(5);   // one photo every 200 ms
            RunFor(2.0);

            // Without interpolation: frozen between photos.
            float before = SeenByPeelyX();
            advance.Execute(0.05);
            Assert.AreEqual(before, SeenByPeelyX(), 1e-4f, "it should only move when a photo arrives");

            // With interpolation (two photos' worth): a little further every frame.
            peely.SetInterpolation(true, 0.4);
            float a = SeenByPeelyX();
            advance.Execute(0.05);
            float b = SeenByPeelyX();
            Assert.AreEqual(5f * 0.05f, b - a, 0.01f);   // 50 ms at 5 m/s, smooth
        }

        [Test]
        public void Standing_still_the_copy_catches_up()
        {
            RunFor(10.5);   // arrived at 10 s; 0.5 s later the late news has arrived too

            Assert.AreEqual(raven.LocalPose.Position.X, SeenByPeelyX(), 0.001f);
        }

        [Test]
        public void Before_the_first_snapshot_arrives_Peely_does_not_know_where_Raven_is()
        {
            RunFor(0.03);   // less than the 80 ms the first news takes

            Assert.IsFalse(peely.TryGetSeen("Raven", world.Clock.Now, out _));
        }

        [Test]
        public void Paused_nothing_moves_until_a_step()
        {
            world.Clock.Pause();
            advance.Execute(1.0);
            Assert.AreEqual(0f, raven.LocalPose.Position.X, 1e-4f);

            advance.Step(0.2);
            Assert.AreEqual(1f, raven.LocalPose.Position.X, 1e-4f);
        }
    }
}
