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
    /// Task 7: the server does not rewind as far as anybody asks. Peely, with lots of lag, needs the
    /// server to go back about 0.3 s to see what he saw.
    /// </summary>
    public class RewindLimitTests
    {
        const double Frame = 1.0 / 60.0;

        ShotResult FireWithLimit(double maxRewind)
        {
            var world = new ServerWorld(new SimClock(), tickRate: 30) { LagCompensation = true, MaxRewind = maxRewind };
            var advance = new AdvanceSimulation(world);
            var fire = new FireShot(world);

            var peely = world.Connect("Peely", new Pose(new Vector3(0f, 1f, -15f), Vector3.UnitZ), ping: 0.2);
            var raven = world.Connect("Raven", new Pose(new Vector3(-10f, 1f, 8f), Vector3.UnitX), ping: 0.2);
            peely.SetInterpolation(true, 0.1);
            raven.SetRoute(new Route(new[]
            {
                new RouteStop(new Vector3(-10f, 1f, 8f), 0.0),
                new RouteStop(new Vector3(40f, 1f, 8f), 0.0),
            }, speed: 5.0), world.Clock.Now);

            for (int i = 0; i < 60; i++) advance.Execute(Frame);
            fire.Execute(peely, "Raven");
            for (int i = 0; i < 60; i++) advance.Execute(Frame);
            return world.Shots.Last();
        }

        [Test]
        public void Within_the_limit_the_server_rewinds_all_the_way_and_it_hits()
        {
            var shot = FireWithLimit(maxRewind: 1.0);

            Assert.IsFalse(shot.RewindLimited);
            Assert.IsTrue(shot.Hit);
        }

        [Test]
        public void Past_the_limit_it_only_rewinds_part_of_the_way_and_misses()
        {
            var shot = FireWithLimit(maxRewind: 0.15);

            Assert.IsTrue(shot.RewindLimited);
            Assert.AreEqual(0.15, shot.ResolvedAt - shot.RewoundTo.Value, 1e-6);
            Assert.IsFalse(shot.Hit);
        }
    }
}
