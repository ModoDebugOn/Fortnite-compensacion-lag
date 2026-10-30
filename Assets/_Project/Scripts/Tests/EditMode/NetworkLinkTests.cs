using System;
using LagCompensation.Domain.Network;
using NUnit.Framework;

namespace LagCompensation.Tests
{
    public class NetworkLinkTests
    {
        [Test]
        public void A_message_arrives_exactly_latency_seconds_later()
        {
            var link = new NetworkLink<string>(latency: 0.04);   // 40 ms one way = 80 ms ping
            link.Send("hello", now: 1.0);

            CollectionAssert.IsEmpty(link.Receive(1.039));
            CollectionAssert.AreEqual(new[] { "hello" }, link.Receive(1.04));
            CollectionAssert.IsEmpty(link.Receive(2.0));   // taken out: it does not arrive twice
        }

        [Test]
        public void Messages_in_flight_can_be_watched_travelling()
        {
            var link = new NetworkLink<string>(latency: 0.1);
            link.Send("a", now: 0.0);

            Assert.AreEqual(1, link.InFlight.Count);
            Assert.AreEqual(0.5, link.InFlight[0].Progress(0.05), 1e-9);
        }

        [Test]
        public void Changing_the_latency_only_affects_new_messages()
        {
            var link = new NetworkLink<string>(latency: 0.2);
            link.Send("slow", now: 0.0);   // arrives at 0.2
            link.SetLatency(0.05);
            link.Send("fast", now: 0.1);   // arrives at 0.15: overtakes the first one

            CollectionAssert.AreEqual(new[] { "fast", "slow" }, link.Receive(0.3));
        }

        [Test]
        public void Zero_latency_is_instant_and_negative_makes_no_sense()
        {
            var link = new NetworkLink<string>();
            link.Send("now", now: 3.0);

            CollectionAssert.AreEqual(new[] { "now" }, link.Receive(3.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => link.SetLatency(-0.01));
        }
    }
}
