using System.Numerics;
using LagCompensation.Domain.Hit;
using NUnit.Framework;

namespace LagCompensation.Tests
{
    /// <summary>A wall like the demo's: 4 m wide, 3.6 m tall, 0.2 m thick, standing on the ground at z = 10.</summary>
    public class RayBoxTests
    {
        static Box Wall(float yaw = 0f) => new Box(new Vector3(0f, 1.8f, 10f), new Vector3(4f, 3.6f, 0.2f), yaw);

        static bool Shoot(Box box, Vector3 from, Vector3 direction, out float distance) =>
            HitTest.RayBox(new Ray(from, direction), box, out distance);

        [Test]
        public void A_shot_straight_at_the_wall_hits_its_front_face()
        {
            Assert.IsTrue(Shoot(Wall(), new Vector3(0f, 1f, 0f), Vector3.UnitZ, out float distance));
            Assert.AreEqual(9.9f, distance, 1e-3f);
        }

        [Test]
        public void Beside_or_above_the_wall_it_misses()
        {
            Assert.IsFalse(Shoot(Wall(), new Vector3(2.5f, 1f, 0f), Vector3.UnitZ, out _));
            Assert.IsFalse(Shoot(Wall(), new Vector3(0f, 4f, 0f), Vector3.UnitZ, out _));
        }

        [Test]
        public void A_wall_turned_90_degrees_is_hit_from_the_side()
        {
            // Turned, it runs along Z: 4 m long from z = 8 to z = 12, only 0.2 m wide in X.
            Assert.IsTrue(Shoot(Wall(90f), new Vector3(-10f, 1f, 11.5f), Vector3.UnitX, out float distance));
            Assert.AreEqual(9.9f, distance, 1e-3f);
            Assert.IsFalse(Shoot(Wall(90f), new Vector3(1f, 1f, 0f), Vector3.UnitZ, out _));
        }

        [Test]
        public void A_wall_behind_the_shooter_or_out_of_range_does_not_count()
        {
            Assert.IsFalse(Shoot(Wall(), new Vector3(0f, 1f, 20f), Vector3.UnitZ, out _));
            Assert.IsFalse(HitTest.RayBox(new Ray(new Vector3(0f, 1f, 0f), Vector3.UnitZ, range: 5f), Wall(), out _));
        }
    }
}
