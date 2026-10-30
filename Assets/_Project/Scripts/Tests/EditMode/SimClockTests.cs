using System;
using LagCompensation.Domain.Timing;
using NUnit.Framework;

namespace LagCompensation.Tests
{
    public class SimClockTests
    {
        [Test]
        public void Starts_at_zero_and_follows_real_time()
        {
            var clock = new SimClock();
            Assert.AreEqual(0.0, clock.Now);

            clock.Advance(0.1);
            clock.Advance(0.1);

            Assert.AreEqual(0.2, clock.Now, 1e-9);
        }

        [Test]
        public void Slow_motion_makes_simulation_time_pass_slower()
        {
            var clock = new SimClock();
            clock.SetTimeScale(0.25);

            double passed = clock.Advance(1.0);   // one real second...

            Assert.AreEqual(0.25, passed, 1e-9);  // ...is a quarter of a simulated second
            Assert.AreEqual(0.25, clock.Now, 1e-9);
        }

        [Test]
        public void Paused_clock_does_not_move_but_can_step_frame_by_frame()
        {
            var clock = new SimClock();
            clock.Pause();

            clock.Advance(1.0);
            Assert.AreEqual(0.0, clock.Now);

            clock.Step(0.02);
            Assert.AreEqual(0.02, clock.Now, 1e-9);

            clock.Resume();
            clock.Advance(1.0);
            Assert.AreEqual(1.02, clock.Now, 1e-9);
        }

        [Test]
        public void Time_never_goes_backwards()
        {
            var clock = new SimClock();
            clock.Advance(-1.0);

            Assert.AreEqual(0.0, clock.Now);
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Step(-0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.SetTimeScale(0.0));
        }
    }
}
