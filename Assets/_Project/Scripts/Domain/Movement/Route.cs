using System;
using System.Collections.Generic;
using System.Numerics;

namespace LagCompensation.Domain.Movement
{
    /// <summary>A point of a <see cref="Route"/> and how long to stand there before moving on.</summary>
    public readonly struct RouteStop
    {
        public readonly Vector3 Position;
        public readonly double Wait;

        public RouteStop(Vector3 position, double wait)
        {
            Position = position;
            Wait = wait;
        }
    }

    /// <summary>
    /// A closed loop of stops walked at a constant speed: stop 0, wait, run to stop 1, wait...
    /// and from the last stop back to the first.
    ///
    /// The position depends only on the time: <see cref="PoseAt"/>(3.2) is always the same spot.
    /// That makes every take of the demo repeatable, and the tests trivial.
    /// </summary>
    public sealed class Route
    {
        /// <summary>One piece of the loop: standing at a stop, or running from one stop to the next.</summary>
        readonly struct Segment
        {
            public readonly double Start, Duration;
            public readonly Vector3 From, To, Forward;

            public Segment(double start, double duration, Vector3 from, Vector3 to, Vector3 forward)
            {
                Start = start;
                Duration = duration;
                From = from;
                To = to;
                Forward = forward;
            }
        }

        readonly Segment[] segments;

        /// <summary>Metres per second while running.</summary>
        public double Speed { get; }

        /// <summary>Seconds for a whole lap, waits included.</summary>
        public double Duration { get; }

        public Route(IReadOnlyList<RouteStop> stops, double speed)
        {
            if (stops == null || stops.Count < 2) throw new ArgumentException("A route needs at least two stops.", nameof(stops));
            if (speed <= 0.0) throw new ArgumentOutOfRangeException(nameof(speed), "Speed must be positive.");
            Speed = speed;

            var list = new List<Segment>();
            double time = 0.0;
            for (int i = 0; i < stops.Count; i++)
            {
                var here = stops[i];
                var next = stops[(i + 1) % stops.Count];
                var previous = stops[(i - 1 + stops.Count) % stops.Count];
                if (here.Wait < 0.0) throw new ArgumentOutOfRangeException(nameof(stops), "Waits cannot be negative.");

                // While waiting it keeps facing the way it arrived.
                if (here.Wait > 0.0)
                    list.Add(new Segment(time, here.Wait, here.Position, here.Position, Flat(here.Position - previous.Position)));
                time += here.Wait;

                double run = Vector3.Distance(here.Position, next.Position) / speed;
                if (run > 0.0)
                    list.Add(new Segment(time, run, here.Position, next.Position, Flat(next.Position - here.Position)));
                time += run;
            }

            if (time <= 0.0) throw new ArgumentException("The route has no length and no waits.", nameof(stops));
            segments = list.ToArray();
            Duration = time;
        }

        /// <summary>Where the runner is at simulation time <paramref name="time"/> (it loops forever, also backwards).</summary>
        public Pose PoseAt(double time)
        {
            double t = time % Duration;
            if (t < 0.0) t += Duration;

            foreach (var segment in segments)
            {
                if (t > segment.Start + segment.Duration) continue;
                float progress = (float)((t - segment.Start) / segment.Duration);
                return new Pose(Vector3.Lerp(segment.From, segment.To, progress), segment.Forward);
            }
            var last = segments[segments.Length - 1];   // only reached through rounding at the very end
            return new Pose(last.To, last.Forward);
        }

        /// <summary>The direction on the ground (Y = 0), of length 1. Straight ahead (+Z) if there is none.</summary>
        static Vector3 Flat(Vector3 direction)
        {
            var flat = new Vector3(direction.X, 0f, direction.Z);
            return flat.LengthSquared() < 1e-8f ? Vector3.UnitZ : Vector3.Normalize(flat);
        }
    }
}
