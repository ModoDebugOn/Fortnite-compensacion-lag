using System;

namespace LagCompensation.Domain.Timing
{
    /// <summary>
    /// The simulation's own clock, in seconds. Everything in the demo (movement, packet delays,
    /// position history, rewinding) reads the time from here, never from Unity.
    ///
    /// That is what lets us pause or play in slow motion without breaking anything: a 100 ms ping
    /// is 100 ms of *simulation* time, so in slow motion the packets travel slowly too.
    /// </summary>
    public sealed class SimClock
    {
        /// <summary>Seconds of simulation since the start.</summary>
        public double Now { get; private set; }

        /// <summary>How fast simulation time runs compared to real time (1 = normal, 0.25 = slow motion).</summary>
        public double TimeScale { get; private set; } = 1.0;

        public bool Paused { get; private set; }

        /// <summary>Moves the clock forward by a real-time delta (one frame). Returns the simulation seconds that passed.</summary>
        public double Advance(double realDeltaSeconds)
        {
            if (Paused || realDeltaSeconds <= 0.0) return 0.0;
            return Step(realDeltaSeconds * TimeScale);
        }

        /// <summary>Moves the clock forward by simulation seconds, even when paused (frame by frame).</summary>
        public double Step(double simSeconds)
        {
            if (simSeconds < 0.0) throw new ArgumentOutOfRangeException(nameof(simSeconds), "Time only moves forward.");
            Now += simSeconds;
            return simSeconds;
        }

        public void SetTimeScale(double scale)
        {
            if (scale <= 0.0) throw new ArgumentOutOfRangeException(nameof(scale), "Use Pause to stop the clock.");
            TimeScale = scale;
        }

        public void Pause() => Paused = true;
        public void Resume() => Paused = false;
        public void TogglePause() => Paused = !Paused;
    }
}
