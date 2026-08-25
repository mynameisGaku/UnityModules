// SPDX-License-Identifier: MIT

using System;
using NUnit.Framework;
using PerfMeter;

namespace PerfMeter.Editor.Tests
{
    [TestFixture]
    public sealed class FrameTimeSamplerTests
    {
        private const double Tolerance = 1e-9;

        [TestCase(FrameTimeSampler.MinimumCapacity)]
        [TestCase(FrameTimeSampler.MaximumCapacity)]
        public void Constructor_BoundaryCapacity_CreatesEmptySampler(int capacity)
        {
            using (var sampler = new FrameTimeSampler(capacity))
            {
                Assert.That(sampler.Capacity, Is.EqualTo(capacity));
                Assert.That(sampler.SampleCount, Is.Zero);
                Assert.That(sampler.SpikeThresholdSeconds, Is.Zero);
                Assert.That(sampler.TotalSpikes, Is.Zero);
                Assert.That(sampler.Last, Is.Zero);
            }
        }

        [TestCase(int.MinValue)]
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(FrameTimeSampler.MaximumCapacity + 1)]
        public void Constructor_CapacityOutOfRange_ThrowsArgumentOutOfRange(int capacity)
        {
            Assert.That(() => new FrameTimeSampler(capacity), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void KnownSequence_MatchesClosedFormStatisticsWithinTolerance()
        {
            using (var sampler = new FrameTimeSampler(16))
            {
                for (var i = 0; i < 10; i++) Assert.That(sampler.AddFrame(1d / 60d, out var error), Is.True);
                Assert.That(sampler.AddFrame(0.1d, out var spikeError), Is.True);
                Assert.That(spikeError, Is.EqualTo(PerfMeterError.None));

                var expectedAverage = (10d / 60d + 0.1d) / 11d;
                var expectedStandardDeviation = Math.Sqrt(10d) / 132d;
                var expectedPercentile95 = (1d / 60d + 0.1d) * 0.5d;

                Assert.That(sampler.SampleCount, Is.EqualTo(11));
                Assert.That(sampler.Last, Is.EqualTo(0.1d).Within(Tolerance));
                Assert.That(sampler.Minimum, Is.EqualTo(1d / 60d).Within(Tolerance));
                Assert.That(sampler.Maximum, Is.EqualTo(0.1d).Within(Tolerance));
                Assert.That(sampler.Average, Is.EqualTo(expectedAverage).Within(Tolerance));
                Assert.That(sampler.Median, Is.EqualTo(1d / 60d).Within(Tolerance));
                Assert.That(sampler.StandardDeviation, Is.EqualTo(expectedStandardDeviation).Within(Tolerance));
                Assert.That(sampler.AverageFps, Is.EqualTo(41.25d).Within(Tolerance));

                Assert.That(sampler.TryGetPercentile(95d, out var percentile95, out var percentileError), Is.True);
                Assert.That(percentileError, Is.EqualTo(PerfMeterError.None));
                Assert.That(percentile95, Is.EqualTo(expectedPercentile95).Within(Tolerance));
                Assert.That(percentile95, Is.EqualTo(0.0583333333333333d).Within(Tolerance));
            }
        }

        [Test]
        public void CapacityOverflow_OverwritesOldest_VisibleThroughStatistics()
        {
            using (var sampler = new FrameTimeSampler(4))
            {
                foreach (var deltaTime in new[] { 0.01d, 0.02d, 0.03d, 0.04d })
                {
                    Assert.That(sampler.AddFrame(deltaTime, out _), Is.True);
                }

                Assert.That(sampler.AddFrame(0.05d, out _), Is.True);

                Assert.That(sampler.SampleCount, Is.EqualTo(sampler.Capacity));
                Assert.That(sampler.Last, Is.EqualTo(0.05d).Within(Tolerance));
                Assert.That(sampler.Minimum, Is.EqualTo(0.02d).Within(Tolerance));
                Assert.That(sampler.Maximum, Is.EqualTo(0.05d).Within(Tolerance));
                Assert.That(sampler.Average, Is.EqualTo(0.035d).Within(Tolerance));
                Assert.That(sampler.Median, Is.EqualTo(0.035d).Within(Tolerance));
                Assert.That(sampler.AverageFps, Is.EqualTo(200d / 7d).Within(Tolerance));
            }
        }

        [Test]
        public void AddFrame_ZeroDelta_IsAcceptedAndCounted()
        {
            using (var sampler = new FrameTimeSampler(4))
            {
                Assert.That(sampler.AddFrame(0d, out var error), Is.True);
                Assert.That(error, Is.EqualTo(PerfMeterError.None));
                Assert.That(sampler.AddFrame(-0d, out error), Is.True);
                Assert.That(error, Is.EqualTo(PerfMeterError.None));

                Assert.That(sampler.SampleCount, Is.EqualTo(2));
                Assert.That(sampler.Average, Is.Zero);
                Assert.That(sampler.AverageFps, Is.Zero);
            }
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void AddFrame_NonFinite_FailsWithoutMutation(double deltaTime)
        {
            using (var sampler = Filled(4, 0.01d, 0.02d))
            {
                var before = sampler.CreateSnapshot();

                Assert.That(sampler.AddFrame(deltaTime, out var error), Is.False);
                Assert.That(error, Is.EqualTo(PerfMeterError.NonFiniteValue));
                Assert.That(sampler.CreateSnapshot(), Is.EqualTo(before));
                Assert.That(sampler.SampleCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void AddFrame_Negative_FailsWithoutMutation()
        {
            using (var sampler = Filled(4, 0.01d, 0.02d))
            {
                var before = sampler.CreateSnapshot();

                Assert.That(sampler.AddFrame(-0.001d, out var error), Is.False);
                Assert.That(error, Is.EqualTo(PerfMeterError.NegativeValue));
                Assert.That(sampler.CreateSnapshot(), Is.EqualTo(before));
                Assert.That(sampler.SampleCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void EmptySampler_StatisticsReturnCanonicalZero()
        {
            using (var sampler = new FrameTimeSampler(8))
            {
                Assert.That(sampler.Last, Is.Zero);
                Assert.That(sampler.Minimum, Is.Zero);
                Assert.That(sampler.Maximum, Is.Zero);
                Assert.That(sampler.Average, Is.Zero);
                Assert.That(sampler.StandardDeviation, Is.Zero);
                Assert.That(sampler.Median, Is.Zero);
                Assert.That(sampler.AverageFps, Is.Zero);

                Assert.That(sampler.TryGetPercentile(95d, out var value, out var error), Is.True);
                Assert.That(error, Is.EqualTo(PerfMeterError.None));
                Assert.That(value, Is.Zero);
            }
        }

        [TestCase(0d)]
        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(100.0001d)]
        [TestCase(101d)]
        public void TryGetPercentile_OutOfRange_ReturnsInvalidPercentile(double percentile)
        {
            using (var sampler = Filled(8, 0.01d, 0.02d, 0.03d, 0.04d))
            {
                Assert.That(sampler.TryGetPercentile(percentile, out var value, out var error), Is.False);
                Assert.That(value, Is.Zero);
                Assert.That(error, Is.EqualTo(PerfMeterError.InvalidPercentile));
            }
        }

        [Test]
        public void TryGetPercentile_ValidBounds_MatchMinimumMedianAndMaximum()
        {
            using (var sampler = Filled(8, 0.01d, 0.02d, 0.03d, 0.04d, 0.05d))
            {
                Assert.That(sampler.TryGetPercentile(50d, out var percentile50, out var error50), Is.True);
                Assert.That(error50, Is.EqualTo(PerfMeterError.None));
                Assert.That(percentile50, Is.EqualTo(sampler.Median).Within(Tolerance));

                Assert.That(sampler.TryGetPercentile(100d, out var percentile100, out var error100), Is.True);
                Assert.That(error100, Is.EqualTo(PerfMeterError.None));
                Assert.That(percentile100, Is.EqualTo(sampler.Maximum).Within(Tolerance));
            }
        }

        [Test]
        public void SpikesSinceLastCheck_LatchesUntilRead()
        {
            using (var sampler = new FrameTimeSampler(16))
            {
                Assert.That(sampler.SetSpikeThreshold(0.05d, out var setError), Is.True);
                Assert.That(setError, Is.EqualTo(PerfMeterError.None));
                Assert.That(sampler.SpikeThresholdSeconds, Is.EqualTo(0.05d).Within(Tolerance));

                Assert.That(sampler.AddFrame(0.01d, out _), Is.True);
                Assert.That(sampler.SpikesSinceLastCheck(), Is.Zero);

                Assert.That(sampler.AddFrame(0.06d, out _), Is.True);
                Assert.That(sampler.AddFrame(0.07d, out _), Is.True);
                Assert.That(sampler.TotalSpikes, Is.EqualTo(2));
                Assert.That(sampler.SpikesSinceLastCheck(), Is.EqualTo(2));
                Assert.That(sampler.SpikesSinceLastCheck(), Is.Zero);
                Assert.That(sampler.TotalSpikes, Is.EqualTo(2));
            }
        }

        [Test]
        public void SpikesSinceLastCheck_DisabledThreshold_AlwaysReturnsZero()
        {
            using (var sampler = new FrameTimeSampler(16))
            {
                Assert.That(sampler.SetSpikeThreshold(0d, out _), Is.True);
                Assert.That(sampler.AddFrame(0.5d, out _), Is.True);

                Assert.That(sampler.SpikesSinceLastCheck(), Is.Zero);
                Assert.That(sampler.TotalSpikes, Is.Zero);
            }
        }

        [Test]
        public void SetSpikeThreshold_InvalidValues_ReturnExplicitErrorsAndKeepPreviousThreshold()
        {
            using (var sampler = Filled(4, 0.01d))
            {
                Assert.That(sampler.SetSpikeThreshold(double.NaN, out var nanError), Is.False);
                Assert.That(nanError, Is.EqualTo(PerfMeterError.NonFiniteValue));
                Assert.That(sampler.SetSpikeThreshold(double.PositiveInfinity, out var infinityError), Is.False);
                Assert.That(infinityError, Is.EqualTo(PerfMeterError.NonFiniteValue));
                Assert.That(sampler.SetSpikeThreshold(-0.001d, out var negativeError), Is.False);
                Assert.That(negativeError, Is.EqualTo(PerfMeterError.InvalidThreshold));
                Assert.That(sampler.SpikeThresholdSeconds, Is.Zero);
            }
        }

        [Test]
        public void Dispose_RejectsSubsequentOperations_AndCanonicalizesStatistics()
        {
            var sampler = Filled(4, 0.01d, 0.06d);
            sampler.Dispose();

            Assert.That(sampler.AddFrame(0.01d, out var addError), Is.False);
            Assert.That(addError, Is.EqualTo(PerfMeterError.SamplerDisposed));
            Assert.That(sampler.SetSpikeThreshold(0.05d, out var thresholdError), Is.False);
            Assert.That(thresholdError, Is.EqualTo(PerfMeterError.SamplerDisposed));
            Assert.That(sampler.TryGetPercentile(95d, out var percentile, out var percentileError), Is.False);
            Assert.That(percentileError, Is.EqualTo(PerfMeterError.SamplerDisposed));
            Assert.That(percentile, Is.Zero);

            Assert.That(sampler.SampleCount, Is.Zero);
            Assert.That(sampler.Last, Is.Zero);
            Assert.That(sampler.Minimum, Is.Zero);
            Assert.That(sampler.AverageFps, Is.Zero);
            Assert.That(sampler.TotalSpikes, Is.Zero);
            Assert.That(sampler.SpikesSinceLastCheck(), Is.Zero);

            Assert.DoesNotThrow(() => sampler.Reset());
            Assert.DoesNotThrow(() => sampler.Dispose());
        }

        [Test]
        public void Reset_ClearsBufferStatisticsAndSpikes_KeepsConfiguration()
        {
            using (var sampler = new FrameTimeSampler(4))
            {
                Assert.That(sampler.SetSpikeThreshold(0.02d, out _), Is.True);
                Assert.That(sampler.AddFrame(0.01d, out _), Is.True);
                Assert.That(sampler.AddFrame(0.03d, out _), Is.True);
                Assert.That(sampler.TotalSpikes, Is.EqualTo(1));

                sampler.Reset();

                Assert.That(sampler.SampleCount, Is.Zero);
                Assert.That(sampler.TotalSpikes, Is.Zero);
                Assert.That(sampler.SpikesSinceLastCheck(), Is.Zero);
                Assert.That(sampler.SpikeThresholdSeconds, Is.EqualTo(0.02d).Within(Tolerance));

                Assert.That(sampler.AddFrame(0.04d, out _), Is.True);
                Assert.That(sampler.SampleCount, Is.EqualTo(1));
                Assert.That(sampler.Last, Is.EqualTo(0.04d).Within(Tolerance));
            }
        }

        [Test]
        public void CreateSnapshot_MatchesLiveStatistics()
        {
            using (var sampler = Filled(8, 0.01d, 0.02d, 0.03d, 0.1d))
            {
                var snapshot = sampler.CreateSnapshot();

                Assert.That(snapshot.Last, Is.EqualTo(sampler.Last).Within(Tolerance));
                Assert.That(snapshot.Average, Is.EqualTo(sampler.Average).Within(Tolerance));
                Assert.That(snapshot.Minimum, Is.EqualTo(sampler.Minimum).Within(Tolerance));
                Assert.That(snapshot.Maximum, Is.EqualTo(sampler.Maximum).Within(Tolerance));
                Assert.That(snapshot.Median, Is.EqualTo(sampler.Median).Within(Tolerance));
                Assert.That(snapshot.StandardDeviation, Is.EqualTo(sampler.StandardDeviation).Within(Tolerance));
                Assert.That(snapshot.SampleCount, Is.EqualTo(sampler.SampleCount));
                Assert.That(snapshot.AverageFps, Is.EqualTo(sampler.AverageFps).Within(Tolerance));
            }
        }

        [Test]
        public void AddFrame_PathAllocatesNoMemory()
        {
            using (var sampler = new FrameTimeSampler(64))
            {
                Assert.That(sampler.SetSpikeThreshold(0.033d, out _), Is.True);
                WarmUp(sampler);

                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < 1000; i++)
                {
                    Assert.That(sampler.AddFrame(0.016d, out _), Is.True);
                    Assert.That(sampler.CreateSnapshot().SampleCount, Is.GreaterThan(0));
                    sampler.TryGetPercentile(95d, out _, out _);
                    sampler.SpikesSinceLastCheck();
                }

                var after = GC.GetAllocatedBytesForCurrentThread();
                Assert.That(after - before, Is.Zero);
            }
        }

        private static void WarmUp(FrameTimeSampler sampler)
        {
            sampler.AddFrame(0.016d, out _);
            _ = sampler.Last;
            _ = sampler.Minimum;
            _ = sampler.Maximum;
            _ = sampler.Average;
            _ = sampler.Median;
            _ = sampler.StandardDeviation;
            _ = sampler.AverageFps;
            sampler.TryGetPercentile(95d, out _, out _);
            sampler.SpikesSinceLastCheck();
            sampler.CreateSnapshot();
        }

        private static FrameTimeSampler Filled(int capacity, params double[] deltas)
        {
            var sampler = new FrameTimeSampler(capacity);
            foreach (var delta in deltas) Assert.That(sampler.AddFrame(delta, out _), Is.True);
            return sampler;
        }
    }
}
