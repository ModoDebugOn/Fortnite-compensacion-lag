using System;
using System.Collections.Generic;
using System.Numerics;

namespace LagCompensation.Domain.Movement
{
    /// <summary>A pose and the time it was true.</summary>
    public readonly struct TimedPose
    {
        public readonly double Time;
        public readonly Pose Pose;

        public TimedPose(double time, Pose pose)
        {
            Time = time;
            Pose = pose;
        }
    }

    /// <summary>
    /// Where a player was at several moments, and where it was *between* them.
    ///
    /// The same tool does two jobs in the demo:
    /// - On a client, it keeps the last snapshots of another player, to draw it moving smoothly
    ///   between them (interpolation) instead of jumping from photo to photo.
    /// - On the server, it keeps the recent past of every player, to rewind time when a shot arrives.
    /// </summary>
    public sealed class PoseHistory
    {
        readonly List<TimedPose> samples = new List<TimedPose>();

        /// <summary>Seconds of past kept; older samples are dropped.</summary>
        public double Length { get; }

        /// <summary>The samples, oldest first. For the ghost views.</summary>
        public IReadOnlyList<TimedPose> Samples => samples;

        public int Count => samples.Count;

        public PoseHistory(double length = 2.0)
        {
            if (length <= 0.0) throw new ArgumentOutOfRangeException(nameof(length), "The history must keep some time.");
            Length = length;
        }

        /// <summary>Adds a sample. They may arrive out of order; they are kept sorted by time.</summary>
        public void Add(double time, Pose pose)
        {
            int index = samples.Count;
            while (index > 0 && samples[index - 1].Time > time) index--;
            samples.Insert(index, new TimedPose(time, pose));

            double oldest = samples[samples.Count - 1].Time - Length;
            while (samples.Count > 2 && samples[0].Time < oldest) samples.RemoveAt(0);
        }

        /// <summary>The newest sample, if there is any.</summary>
        public bool TryGetLatest(out TimedPose latest)
        {
            latest = samples.Count > 0 ? samples[samples.Count - 1] : default;
            return samples.Count > 0;
        }

        /// <summary>
        /// Where it was at <paramref name="time"/>: between the two samples around that time,
        /// in proportion. Before the first sample it is at the first; after the last, it stays at
        /// the last (nobody knows yet where it went next).
        /// </summary>
        public bool TrySample(double time, out Pose pose)
        {
            pose = default;
            if (samples.Count == 0) return false;

            var first = samples[0];
            var last = samples[samples.Count - 1];
            if (time <= first.Time) { pose = first.Pose; return true; }
            if (time >= last.Time) { pose = last.Pose; return true; }

            int after = 1;
            while (samples[after].Time < time) after++;
            var a = samples[after - 1];
            var b = samples[after];

            float t = (float)((time - a.Time) / (b.Time - a.Time));
            var forward = Vector3.Lerp(a.Pose.Forward, b.Pose.Forward, t);
            if (forward.LengthSquared() < 1e-6f) forward = b.Pose.Forward;   // turned around: pick one
            pose = new Pose(Vector3.Lerp(a.Pose.Position, b.Pose.Position, t), Vector3.Normalize(forward));
            return true;
        }
    }
}
