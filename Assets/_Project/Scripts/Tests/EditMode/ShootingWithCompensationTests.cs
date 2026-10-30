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
    /// Task 5: the same shot that missed in task 4 (lots of lag, Raven running, perfect aim), now
    /// with the server rewinding to the moment Peely was seeing.
    /// </summary>
    public class ShootingWithCompensationTests
    {
        const double Frame = 1.0 / 60.0;

        ServerWorld world;
        AdvanceSimulation advance;
        FireShot fire;
        GameClient peely, raven;

        [SetUp]
        public void SetUp()
        {
            world = new ServerWorld(new SimClock(), tickRate: 30) { LagCompensation = true };
            advance = new AdvanceSimulation(world);
            fire = new FireShot(world);

            peely = world.Connect("Peely", new Pose(new Vector3(0f, 1f, -15f), Vector3.UnitZ), ping: 0.2);
            raven = world.Connect("Raven", new Pose(new Vector3(-10f, 1f, 8f), Vector3.UnitX), ping: 0.2);
            peely.SetInterpolation(true, 0.1);
            raven.SetRoute(new Route(new[]
            {
                new RouteStop(new Vector3(-10f, 1f, 8f), 0.0),
                new RouteStop(new Vector3(40f, 1f, 8f), 0.0),
            }, speed: 5.0), world.Clock.Now);

            for (int i = 0; i < 60; i++) advance.Execute(Frame);   // one second: everybody knows everybody
        }

        ShotResult WaitForVerdict()
        {
            for (int i = 0; i < 60; i++) advance.Execute(Frame);
            Assert.AreEqual(1, world.Shots.Count);
            return world.Shots.Last();
        }

        [Test]
        public void The_server_keeps_an_album_of_where_everybody_was()
        {
            var history = world.FindPlayer("Raven").History;

            Assert.Greater(history.Count, 20);   // about 30 snapshots in a second
        }

        [Test]
        public void Rewinding_the_shot_that_used_to_miss_now_hits()
        {
            fire.Execute(peely, "Raven");

            var shot = WaitForVerdict();
            Assert.IsTrue(shot.Rewound);
            Assert.IsTrue(shot.Hit);
        }

        [Test]
        public void The_server_checks_against_the_very_Raven_that_Peely_was_seeing()
        {
            Assert.IsTrue(peely.TryGetSeen("Raven", world.Clock.Now, out var seenByPeely));
            fire.Execute(peely, "Raven");

            var shot = WaitForVerdict();
            Assert.Less(Vector3.Distance(seenByPeely.Position, shot.TargetPose.Position), 0.01f);
        }

        [Test]
        public void The_rewound_Raven_is_behind_where_the_server_has_her_now()
        {
            fire.Execute(peely, "Raven");

            var shot = WaitForVerdict();
            float now = world.FindPlayer("Raven").Pose.Position.X;
            Assert.Less(shot.TargetPose.Position.X, now - 1f, "the server went back in time");
        }

        [Test]
        public void Switching_compensation_off_brings_back_the_miss()
        {
            world.LagCompensation = false;
            fire.Execute(peely, "Raven");

            var shot = WaitForVerdict();
            Assert.IsFalse(shot.Rewound);
            Assert.IsFalse(shot.Hit);
        }
    }
}
