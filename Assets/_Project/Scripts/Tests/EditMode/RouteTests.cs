using System;
using System.Numerics;
using LagCompensation.Domain.Movement;
using NUnit.Framework;

namespace LagCompensation.Tests
{
    public class RouteTests
    {
        // Two stops 10 m apart along X, run at 5 m/s: 2 s each way, plus the waits.
        static readonly Vector3 A = new Vector3(0f, 1f, 0f);
        static readonly Vector3 B = new Vector3(10f, 1f, 0f);

        static Route OutAndBack(double waitAtA = 1.0, double waitAtB = 3.0) =>
            new Route(new[] { new RouteStop(A, waitAtA), new RouteStop(B, waitAtB) }, speed: 5.0);

        static void AssertNear(Vector3 expected, Vector3 actual) =>
            Assert.Less(Vector3.Distance(expected, actual), 1e-4f, $"expected {expected}, got {actual}");

        [Test]
        public void A_lap_is_the_runs_plus_the_waits()
        {
            Assert.AreEqual(1.0 + 2.0 + 3.0 + 2.0, OutAndBack().Duration, 1e-9);
        }

        [Test]
        public void Waits_at_the_first_stop_then_runs_to_the_next()
        {
            var route = OutAndBack();

            AssertNear(A, route.PoseAt(0.0).Position);
            AssertNear(A, route.PoseAt(0.9).Position);                        // still waiting
            AssertNear(new Vector3(5f, 1f, 0f), route.PoseAt(2.0).Position);  // half way (1 s of running)
            AssertNear(B, route.PoseAt(3.5).Position);                        // waiting at B
        }

        [Test]
        public void Faces_where_it_runs_and_keeps_facing_that_way_while_waiting()
        {
            var route = OutAndBack();

            AssertNear(Vector3.UnitX, route.PoseAt(2.0).Forward);    // running to B
            AssertNear(Vector3.UnitX, route.PoseAt(4.0).Forward);    // waiting at B, as it arrived
            AssertNear(-Vector3.UnitX, route.PoseAt(7.0).Forward);   // running back to A
        }

        [Test]
        public void Loops_forever_and_the_same_time_is_always_the_same_place()
        {
            var route = OutAndBack();

            AssertNear(route.PoseAt(2.0).Position, route.PoseAt(2.0 + route.Duration).Position);
            AssertNear(route.PoseAt(2.0).Position, route.PoseAt(2.0 - route.Duration).Position);
        }

        [Test]
        public void Rejects_routes_that_make_no_sense()
        {
            Assert.Throws<ArgumentException>(() => new Route(new[] { new RouteStop(A, 1.0) }, 5.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => OutAndBack(waitAtA: -1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Route(new[] { new RouteStop(A, 0), new RouteStop(B, 0) }, 0.0));
        }
    }
}
