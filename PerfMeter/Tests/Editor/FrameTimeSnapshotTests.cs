// SPDX-License-Identifier: MIT

using NUnit.Framework;
using PerfMeter;

namespace PerfMeter.Editor.Tests
{
    [TestFixture]
    public sealed class FrameTimeSnapshotTests
    {
        [Test]
        public void IdenticalSequences_ProduceEqualSnapshots()
        {
            FrameTimeSnapshot first;
            FrameTimeSnapshot second;
            using (CreateGoldenSampler(out first))
            {
            }

            using (CreateGoldenSampler(out second))
            {
            }

            Assert.That(first.Equals(second), Is.True);
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
        }

        [Test]
        public void AnyFieldDifference_BreaksEquality()
        {
            var baseline = new FrameTimeSnapshot(0.016d, 0.02d, 0.01d, 0.1d, 0.018d, 0.004d, 11, 50d);
            var variants = new[]
            {
                new FrameTimeSnapshot(0.099d, 0.02d, 0.01d, 0.1d, 0.018d, 0.004d, 11, 50d),
                new FrameTimeSnapshot(0.016d, 0.021d, 0.01d, 0.1d, 0.018d, 0.004d, 11, 50d),
                new FrameTimeSnapshot(0.016d, 0.02d, 0.011d, 0.1d, 0.018d, 0.004d, 11, 50d),
                new FrameTimeSnapshot(0.016d, 0.02d, 0.01d, 0.101d, 0.018d, 0.004d, 11, 50d),
                new FrameTimeSnapshot(0.016d, 0.02d, 0.01d, 0.1d, 0.019d, 0.004d, 11, 50d),
                new FrameTimeSnapshot(0.016d, 0.02d, 0.01d, 0.1d, 0.018d, 0.005d, 11, 50d),
                new FrameTimeSnapshot(0.016d, 0.02d, 0.01d, 0.1d, 0.018d, 0.004d, 10, 50d),
                new FrameTimeSnapshot(0.016d, 0.02d, 0.01d, 0.1d, 0.018d, 0.004d, 11, 51d)
            };

            foreach (var variant in variants)
            {
                Assert.That(baseline.Equals(variant), Is.False);
                Assert.That(baseline == variant, Is.False);
                Assert.That(baseline != variant, Is.True);
            }
        }

        [Test]
        public void BoxedComparison_WorksThroughObjectEquals()
        {
            object boxed = new FrameTimeSnapshot(0.016d, 0.02d, 0.01d, 0.1d, 0.018d, 0.004d, 11, 50d);
            var same = new FrameTimeSnapshot(0.016d, 0.02d, 0.01d, 0.1d, 0.018d, 0.004d, 11, 50d);

            Assert.That(same.Equals(boxed), Is.True);
            Assert.That(same.Equals("different type"), Is.False);
        }

        [Test]
        public void DifferentSampleCounts_ProduceUnequalSnapshots()
        {
            FrameTimeSnapshot golden;
            FrameTimeSnapshot extended;
            using (CreateGoldenSampler(out golden))
            {
            }

            using (var sampler = CreateGoldenSampler(out _))
            {
                Assert.That(sampler.AddFrame(0.2d, out _), Is.True);
                extended = sampler.CreateSnapshot();
            }

            Assert.That(golden, Is.Not.EqualTo(extended));
            Assert.That(extended.SampleCount, Is.EqualTo(12));
        }

        private static FrameTimeSampler CreateGoldenSampler(out FrameTimeSnapshot snapshot)
        {
            var sampler = new FrameTimeSampler(16);
            for (var i = 0; i < 10; i++) sampler.AddFrame(1d / 60d, out _);
            sampler.AddFrame(0.1d, out _);
            snapshot = sampler.CreateSnapshot();
            return sampler;
        }
    }
}
