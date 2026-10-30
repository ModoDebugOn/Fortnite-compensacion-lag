using System;
using System.Numerics;
using LagCompensation.Domain.Movement;
using NUnit.Framework;

namespace LagCompensation.Tests
{
    /// <summary>The example from the explanation of task 2: one photo every 100 ms, Raven at 5 m/s.</summary>
    public class PoseHistoryTests
    {
        static Pose At(float x) => new Pose(new Vector3(x, 1f, 0f), Vector3.UnitX);

        static PoseHistory ThreePhotos()
        {
            var history = new PoseHistory();
            history.Add(1.0, At(0f));
            history.Add(1.1, At(0.5f));
            history.Add(1.2, At(1.0f));
            return history;
        }

        static float XAt(PoseHistory history, double time)
        {
            Assert.IsTrue(history.TrySample(time, out var pose));
            return pose.Position.X;
        }

        [Test]
        public void Between_two_photos_it_is_in_proportion()
        {
            var history = ThreePhotos();

            Assert.AreEqual(0.75f, XAt(history, 1.15), 1e-4f);   // half way between 1.1 and 1.2
            Assert.AreEqual(0.85f, XAt(history, 1.17), 1e-4f);
            Assert.AreEqual(0.5f, XAt(history, 1.1), 1e-4f);     // exactly on a photo
        }

        [Test]
        public void After_the_newest_photo_it_stays_there_and_before_the_oldest_too()
        {
            var history = ThreePhotos();

            Assert.AreEqual(1.0f, XAt(history, 1.5), 1e-4f);   // nobody knows yet where it went next
            Assert.AreEqual(0f, XAt(history, 0.5), 1e-4f);
        }

        [Test]
        public void Photos_that_arrive_out_of_order_are_put_in_their_place()
        {
            var history = new PoseHistory();
            history.Add(1.2, At(1.0f));
            history.Add(1.0, At(0f));

            Assert.AreEqual(0.5f, XAt(history, 1.1), 1e-4f);
            Assert.IsTrue(history.TryGetLatest(out var latest));
            Assert.AreEqual(1.2, latest.Time);
        }

        [Test]
        public void Only_keeps_the_recent_past()
        {
            var history = new PoseHistory(length: 0.5);
            for (int i = 0; i <= 20; i++) history.Add(i * 0.1, At(i));   // 0 to 2 s

            Assert.LessOrEqual(history.Samples[0].Time, 2.0 - 0.5 + 1e-9);
            Assert.GreaterOrEqual(history.Samples[0].Time, 2.0 - 0.6);
        }

        [Test]
        public void Empty_it_knows_nothing()
        {
            var history = new PoseHistory();

            Assert.IsFalse(history.TrySample(1.0, out _));
            Assert.IsFalse(history.TryGetLatest(out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PoseHistory(0.0));
        }
    }
}
