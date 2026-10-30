using System;

namespace LagCompensation.Domain.Timing
{
    /// <summary>
    /// Says when it is time for the next network tick. Games do not send something every frame:
    /// they send a fixed number of updates per second (the tick rate), e.g. 30.
    /// </summary>
    public sealed class TickTimer
    {
        double next;

        /// <summary>Seconds between ticks.</summary>
        public double Interval { get; private set; }

        public TickTimer(double ticksPerSecond)
        {
            SetRate(ticksPerSecond);
        }

        public void SetRate(double ticksPerSecond)
        {
            if (ticksPerSecond <= 0.0) throw new ArgumentOutOfRangeException(nameof(ticksPerSecond), "The tick rate must be positive.");
            Interval = 1.0 / ticksPerSecond;
        }

        /// <summary>True if a tick is due at <paramref name="now"/>. Ticks missed in a long frame
        /// are skipped: at most one per call.</summary>
        public bool Due(double now)
        {
            if (now < next) return false;
            next += Interval;
            if (next <= now) next = now + Interval;
            return true;
        }
    }
}
