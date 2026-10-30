using LagCompensation.Domain.Timing;
using NUnit.Framework;

namespace LagCompensation.Tests
{
    public class TickTimerTests
    {
        [Test]
        public void Ticks_at_the_given_rate()
        {
            var timer = new TickTimer(ticksPerSecond: 10);   // every 0.1 s
            int ticks = 0;
            for (int frame = 0; frame <= 100; frame++)       // one second at 100 frames per second
                if (timer.Due(frame * 0.01)) ticks++;

            Assert.AreEqual(11, ticks);   // at 0.0, 0.1 ... 1.0
        }

        [Test]
        public void A_long_frame_gives_one_tick_not_a_burst()
        {
            var timer = new TickTimer(ticksPerSecond: 10);
            Assert.IsTrue(timer.Due(0.0));
            Assert.IsTrue(timer.Due(1.0));    // a whole second later
            Assert.IsFalse(timer.Due(1.05));  // the missed ones are not sent all at once
            Assert.IsTrue(timer.Due(1.1));
        }
    }
}
