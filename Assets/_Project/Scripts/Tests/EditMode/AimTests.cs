using System.Numerics;
using LagCompensation.Application;
using LagCompensation.Application.UseCases;
using LagCompensation.Domain.Movement;
using LagCompensation.Domain.Timing;
using NUnit.Framework;

namespace LagCompensation.Tests
{
    /// <summary>Peely turns to keep aiming at Raven: at the Raven *he sees*, not the real one.</summary>
    public class AimTests
    {
        [Test]
        public void The_shooter_keeps_facing_the_target_as_it_sees_it()
        {
            var world = new ServerWorld(new SimClock(), tickRate: 30);
            var advance = new AdvanceSimulation(world);
            var peely = world.Connect("Peely", new Pose(new Vector3(0f, 1f, -15f), Vector3.UnitZ), ping: 0.2);
            var raven = world.Connect("Raven", new Pose(new Vector3(-10f, 1f, 8f), Vector3.UnitX), ping: 0.2);
            raven.SetRoute(new Route(new[]
            {
                new RouteStop(new Vector3(-10f, 1f, 8f), 0.0),
                new RouteStop(new Vector3(40f, 1f, 8f), 0.0),
            }, speed: 5.0), world.Clock.Now);
            peely.SetAimTarget("Raven");

            for (int i = 0; i < 120; i++) advance.Execute(1.0 / 60.0);

            Assert.IsTrue(peely.TryGetSeen("Raven", world.Clock.Now, out var seen));
            var expected = peely.LocalPose.FacingTowards(seen.Position).Forward;
            Assert.Less(Vector3.Distance(expected, peely.LocalPose.Forward), 0.02f, "facing the Raven he sees");
            Assert.Less(peely.LocalPose.Forward.X, 0f, "Raven is still to his left, so he turned left");
        }
    }
}
