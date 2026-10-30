using System.Numerics;
using LagCompensation.Domain.Hit;
using LagCompensation.Domain.Movement;
using NUnit.Framework;

namespace LagCompensation.Tests
{
    /// <summary>A capsule standing at the origin (2 m tall, 0.5 m radius), shot at from 10 m away.</summary>
    public class HitTestTests
    {
        static readonly Capsule Target = Capsule.Of(new Pose(new Vector3(0f, 1f, 0f), Vector3.UnitZ));

        static bool Shoot(Vector3 from, Vector3 at, float range = 200f) =>
            HitTest.RayCapsule(new Ray(from, at - from, range), Target, out _, out _);

        [Test]
        public void Straight_at_the_middle_hits()
        {
            Assert.IsTrue(Shoot(new Vector3(0f, 1f, -10f), new Vector3(0f, 1f, 0f)));
        }

        [Test]
        public void Just_inside_the_edge_hits_and_just_outside_misses()
        {
            Assert.IsTrue(Shoot(new Vector3(0.45f, 1f, -10f), new Vector3(0.45f, 1f, 0f)));
            Assert.IsFalse(Shoot(new Vector3(0.55f, 1f, -10f), new Vector3(0.55f, 1f, 0f)));
        }

        [Test]
        public void The_round_head_counts_but_not_the_air_above_it()
        {
            Assert.IsTrue(Shoot(new Vector3(0f, 1.9f, -10f), new Vector3(0f, 1.9f, 0f)));
            Assert.IsFalse(Shoot(new Vector3(0f, 2.1f, -10f), new Vector3(0f, 2.1f, 0f)));
        }

        [Test]
        public void Out_of_range_or_aiming_away_misses()
        {
            Assert.IsFalse(Shoot(new Vector3(0f, 1f, -10f), new Vector3(0f, 1f, 0f), range: 5f));
            Assert.IsFalse(Shoot(new Vector3(0f, 1f, -10f), new Vector3(0f, 1f, -20f)));
        }

        [Test]
        public void Reports_how_far_from_the_centre_it_passed()
        {
            HitTest.RayCapsule(new Ray(new Vector3(0.3f, 1f, -10f), Vector3.UnitZ), Target, out float miss, out float distance);

            Assert.AreEqual(0.3f, miss, 1e-4f);
            Assert.AreEqual(10f, distance, 1e-3f);
        }
    }
}
