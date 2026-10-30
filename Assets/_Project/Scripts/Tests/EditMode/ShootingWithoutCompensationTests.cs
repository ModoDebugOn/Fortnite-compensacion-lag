using System.Linq;
using System.Numerics;
using LagCompensation.Application;
using LagCompensation.Application.UseCases;
using LagCompensation.Domain.Movement;
using LagCompensation.Domain.Timing;
using NUnit.Framework;

namespace LagCompensation.Tests
{
    /// <summary>
    /// Task 4: Peely aims *perfectly* at the Raven he sees, and the server checks the shot against
    /// where it has Raven when the shot arrives. No lag compensation yet.
    /// </summary>
    public class ShootingWithoutCompensationTests
    {
        const double Frame = 1.0 / 60.0;

        ServerWorld world;
        AdvanceSimulation advance;
        FireShot fire;
        GameClient peely, raven;

        void Setup(double ping, double interpolation, bool ravenRuns)
        {
            world = new ServerWorld(new SimClock(), tickRate: 30) { LagCompensation = false };
            advance = new AdvanceSimulation(world);
            fire = new FireShot(world);

            peely = world.Connect("Peely", new Pose(new Vector3(0f, 1f, -15f), Vector3.UnitZ), ping);
            raven = world.Connect("Raven", new Pose(new Vector3(-10f, 1f, 8f), Vector3.UnitX), ping);
            peely.SetInterpolation(interpolation > 0.0, interpolation);
            if (ravenRuns)
                raven.SetRoute(new Route(new[]
                {
                    new RouteStop(new Vector3(-10f, 1f, 8f), 0.0),
                    new RouteStop(new Vector3(40f, 1f, 8f), 0.0),
                }, speed: 5.0), world.Clock.Now);
        }

        ShotResult FireAndWait()
        {
            for (int i = 0; i < 60; i++) advance.Execute(Frame);   // one second: everybody knows everybody
            Assert.IsTrue(fire.Execute(peely, "Raven"), "Peely should see Raven by now");
            for (int i = 0; i < 60; i++) advance.Execute(Frame);   // time for the shot to reach the server
            Assert.AreEqual(1, world.Shots.Count);
            return world.Shots.Last();
        }

        [Test]
        public void Without_lag_aiming_at_what_you_see_hits()
        {
            Setup(ping: 0.0, interpolation: 0.0, ravenRuns: true);

            Assert.IsTrue(FireAndWait().Hit);
        }

        [Test]
        public void With_lag_aiming_perfectly_at_a_runner_misses()
        {
            Setup(ping: 0.2, interpolation: 0.1, ravenRuns: true);

            var shot = FireAndWait();
            Assert.IsFalse(shot.Hit);
            Assert.Greater(shot.Miss, 1f, "the server had Raven well ahead of where Peely aimed");
        }

        [Test]
        public void With_lag_a_target_standing_still_is_still_hit()
        {
            Setup(ping: 0.2, interpolation: 0.1, ravenRuns: false);

            Assert.IsTrue(FireAndWait().Hit);
        }
    }
}
