using System;
using System.Collections.Generic;

namespace LagCompensation.Domain.Network
{
    /// <summary>A message on its way: what it carries, when it left and when it will arrive.</summary>
    public readonly struct Packet<T>
    {
        public readonly T Payload;
        public readonly double SentAt;
        public readonly double ArrivesAt;

        public Packet(T payload, double sentAt, double arrivesAt)
        {
            Payload = payload;
            SentAt = sentAt;
            ArrivesAt = arrivesAt;
        }

        /// <summary>How far along the trip it is at <paramref name="now"/>: 0 just sent, 1 arrived.</summary>
        public double Progress(double now) =>
            ArrivesAt <= SentAt ? 1.0 : Math.Clamp((now - SentAt) / (ArrivesAt - SentAt), 0.0, 1.0);
    }

    /// <summary>
    /// A one-way connection over a fake internet: whatever is sent arrives exactly
    /// <see cref="Latency"/> seconds later (simulation time). There is no real network in the demo;
    /// this is all the "internet" we need to show what lag does.
    ///
    /// Ping is the round trip, so one way is usually half the ping.
    /// </summary>
    public sealed class NetworkLink<T>
    {
        readonly List<Packet<T>> inFlight = new List<Packet<T>>();

        /// <summary>Seconds a message takes to arrive. Changing it only affects messages sent afterwards.</summary>
        public double Latency { get; private set; }

        /// <summary>The messages still travelling, oldest first. For the timeline view.</summary>
        public IReadOnlyList<Packet<T>> InFlight => inFlight;

        public NetworkLink(double latency = 0.0)
        {
            SetLatency(latency);
        }

        public void SetLatency(double seconds)
        {
            if (seconds < 0.0) throw new ArgumentOutOfRangeException(nameof(seconds), "Latency cannot be negative.");
            Latency = seconds;
        }

        public void Send(T payload, double now) => inFlight.Add(new Packet<T>(payload, now, now + Latency));

        /// <summary>Takes out every message that has arrived by <paramref name="now"/>, in arrival order.</summary>
        public List<T> Receive(double now)
        {
            var arrived = new List<Packet<T>>();
            inFlight.RemoveAll(packet =>
            {
                if (packet.ArrivesAt > now) return false;
                arrived.Add(packet);
                return true;
            });
            // If the latency went down, a newer message can overtake an older one, as on the real internet.
            arrived.Sort((a, b) => a.ArrivesAt.CompareTo(b.ArrivesAt));
            return arrived.ConvertAll(packet => packet.Payload);
        }
    }
}
