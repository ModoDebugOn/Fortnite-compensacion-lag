using System.Linq;
using System.Numerics;
using LagCompensation.Application;
using LagCompensation.Application.UseCases;
using LagCompensation.Domain.Hit;
using LagCompensation.Domain.Movement;
using LagCompensation.Domain.Timing;
using NUnit.Framework;

namespace LagCompensation.Tests
{
    /// <summary>
    /// Task 6, the scene of the title, with the demo's layout: Peely at the bottom, Raven running
    /// along the lane until she gets behind the cover wall. Peely fires the very moment Raven is
    /// hidden on her own screen.
    /// </summary>
    public class DeathBehindTheWallTests
    {
        const double Frame = 1.0 / 60.0;
        static readonly Vector3 PeelyAt = new Vector3(0f, 1f, -15f);
        static readonly Vector3 LaneStart = new Vector3(-10f, 1f, 8f);
        static readonly Vector3 HidingSpot = new Vector3(7f, 1f, 9.5f);

        ServerWorld world;
        AdvanceSimulation advance;
        FireShot fire;
        GameClient peely, raven;

        void Setup(double ping, bool compensation, bool ravenRuns)
        {
            world = new ServerWorld(new SimClock(), tickRate: 30) { LagCompensation = compensation };
            world.AddWall(new Box(new Vector3(7f, 1.8f, 6.5f), new Vector3(4f, 3.6f, 0.2f), 0f));   // the cover wall
            advance = new AdvanceSimulation(world);
            fire = new FireShot(world);

            peely = world.Connect("Peely", new Pose(PeelyAt, Vector3.UnitZ), ping);
            raven = world.Connect("Raven", new Pose(ravenRuns ? LaneStart : HidingSpot, Vector3.UnitX), ping);
            peely.SetInterpolation(true, 0.1);
            if (ravenRuns)
                raven.SetRoute(new Route(new[] { new RouteStop(LaneStart, 1.0), new RouteStop(HidingSpot, 5.0) }, speed: 5.0),
                               world.Clock.Now);
        }

        bool PeelyCouldSee(Vector3 target) => world.CanSee(PeelyAt + FireShot.EyeHeight, target);

        /// <summary>Runs until Raven, on her own screen, has just got behind the wall, and fires.</summary>
        void FireTheMomentRavenHides(out Pose peelyWasSeeing)
        {
            for (int i = 0; i < 60; i++) advance.Execute(Frame);   // one second: everybody knows everybody
            for (int i = 0; i < 600 && PeelyCouldSee(raven.LocalPose.Position); i++) advance.Execute(Frame);
            Assert.IsFalse(PeelyCouldSee(raven.LocalPose.Position), "Raven should be behind the wall by now");
            Assert.IsTrue(peely.TryGetSeen("Raven", world.Clock.Now, out peelyWasSeeing));
            Assert.IsTrue(fire.Execute(peely, "Raven"));
        }

        ShotResult Verdict()
        {
            for (int i = 0; i < 60; i++) advance.Execute(Frame);
            return world.Shots.Last();
        }

        [Test]
        public void Shooting_someone_standing_behind_the_wall_hits_the_wall()
        {
            Setup(ping: 0.0, compensation: true, ravenRuns: false);
            for (int i = 0; i < 60; i++) advance.Execute(Frame);

            fire.Execute(peely, "Raven");

            var shot = Verdict();
            Assert.IsFalse(shot.Hit);
            Assert.IsTrue(shot.BlockedByWall);
        }

        [Test]
        public void With_lag_and_rewinding_Raven_dies_behind_the_wall()
        {
            Setup(ping: 0.2, compensation: true, ravenRuns: true);

            FireTheMomentRavenHides(out var peelyWasSeeing);

            Assert.IsTrue(PeelyCouldSee(peelyWasSeeing.Position), "on Peely's screen she was still out in the open");
            var shot = Verdict();
            Assert.IsTrue(shot.Hit, "the server rewound to what Peely saw: a hit");
            Assert.IsFalse(PeelyCouldSee(raven.LocalPose.Position), "...while Raven is behind the wall");
        }

        [Test]
        public void Without_rewinding_the_same_shot_does_not_count()
        {
            Setup(ping: 0.2, compensation: false, ravenRuns: true);

            FireTheMomentRavenHides(out _);

            Assert.IsFalse(Verdict().Hit);
        }

        [Test]
        public void Without_lag_there_is_no_death_behind_the_wall()
        {
            Setup(ping: 0.0, compensation: true, ravenRuns: true);
            peely.SetInterpolation(false, 0.0);

            FireTheMomentRavenHides(out _);

            Assert.IsFalse(Verdict().Hit, "Peely sees exactly what is true: she is already behind the wall");
        }
    }
}
