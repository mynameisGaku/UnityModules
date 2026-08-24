using System;
using System.Linq;
using NUnit.Framework;

namespace GameplayAnalysis.Tests
{
    [TestFixture]
    public sealed class LinearTrendEstimatorTests
    {
        [Test]
        public void TryEstimate_NullWholeArray_ReturnsExplicitFailure()
        {
            Assert.That(LinearTrendEstimator.TryEstimate(null, out var estimate, out var error), Is.False);
            Assert.That(estimate, Is.EqualTo(default(LinearTrendEstimate)));
            Assert.That(error, Is.EqualTo(LinearTrendError.NullSamples));
        }

        [Test]
        public void TryEstimate_NullRange_ReturnsNullBeforeOtherValidation()
        {
            Assert.That(LinearTrendEstimator.TryEstimate(null, -1, 1, out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(LinearTrendError.NullSamples));
        }

        [TestCase(-2)]
        [TestCase(-1)]
        public void TryEstimate_NegativeStart_ReturnsExplicitFailure(int startIndex)
        {
            Assert.That(LinearTrendEstimator.TryEstimate(new[] { 1d, 2d }, startIndex, 2, out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(LinearTrendError.InvalidStartIndex));
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(LinearTrendEstimator.MaximumSampleCount + 1)]
        public void TryEstimate_InvalidCount_ReturnsExplicitFailure(int count)
        {
            Assert.That(LinearTrendEstimator.TryEstimate(new double[40], 0, count, out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(LinearTrendError.InvalidSampleCount));
        }

        [TestCase(1, 2)]
        [TestCase(2, 2)]
        [TestCase(10, 2)]
        public void TryEstimate_RangeOutsideArray_ReturnsExplicitFailure(int startIndex, int count)
        {
            Assert.That(LinearTrendEstimator.TryEstimate(new[] { 1d, 2d }, startIndex, count, out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(LinearTrendError.RangeOutOfBounds));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void TryEstimate_NonFiniteSelectedSample_ReturnsExplicitFailure(double invalid)
        {
            Assert.That(LinearTrendEstimator.TryEstimate(new[] { 1d, invalid, 3d }, out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(LinearTrendError.NonFiniteSample));
        }

        [Test]
        public void TryEstimate_NonFiniteOutsideSelectedRange_IsIgnored()
        {
            var samples = new[] { double.NaN, 10d, 20d, 30d, double.PositiveInfinity };
            Assert.That(LinearTrendEstimator.TryEstimate(samples, 1, 3, out var estimate, out var error), Is.True);
            Assert.That(error, Is.EqualTo(LinearTrendError.None));
            AssertEstimate(estimate, 3, 10d, 30d, 20d, 10d, 10d, 40d);
        }

        [Test]
        public void TryEstimate_TwoIncreasingSamples_ReturnsExactLine()
        {
            var estimate = Estimate(10d, 20d);
            AssertEstimate(estimate, 2, 10d, 20d, 15d, 10d, 10d, 30d);
        }

        [Test]
        public void TryEstimate_FourIncreasingSamples_ReturnsExactLine()
        {
            var estimate = Estimate(10d, 20d, 30d, 40d);
            AssertEstimate(estimate, 4, 10d, 40d, 25d, 10d, 10d, 50d);
        }

        [Test]
        public void TryEstimate_FallingSamples_ReturnsNegativeSlope()
        {
            var estimate = Estimate(40d, 30d, 20d, 10d);
            AssertEstimate(estimate, 4, 40d, 10d, 25d, -10d, 40d, 0d);
        }

        [TestCase(0d)]
        [TestCase(20d)]
        [TestCase(-20d)]
        public void TryEstimate_FlatSamples_ReturnZeroSlope(double value)
        {
            var estimate = Estimate(value, value, value, value);
            AssertEstimate(estimate, 4, value, value, value, 0d, value, value);
        }

        [Test]
        public void TryEstimate_NoisySamples_ReturnsLeastSquaresFit()
        {
            var estimate = Estimate(10d, 30d, 20d, 40d);
            AssertEstimate(estimate, 4, 10d, 40d, 25d, 8d, 13d, 45d);
        }

        [Test]
        public void TryEstimate_OffsetRange_UsesLocalIndexZero()
        {
            var samples = new[] { 999d, 5d, 10d, 15d, -999d };
            Assert.That(LinearTrendEstimator.TryEstimate(samples, 1, 3, out var estimate, out var error), Is.True);
            Assert.That(error, Is.EqualTo(LinearTrendError.None));
            AssertEstimate(estimate, 3, 5d, 15d, 10d, 5d, 5d, 20d);
        }

        [Test]
        public void TryEstimate_MaximumCount_ReturnsExpectedLine()
        {
            var samples = Enumerable.Range(0, LinearTrendEstimator.MaximumSampleCount).Select(index => 3d + (2d * index)).ToArray();
            var estimate = Estimate(samples);
            AssertEstimate(estimate, 32, 3d, 65d, 34d, 2d, 3d, 67d);
        }

        [Test]
        public void TryEstimate_IdenticalMaximumValues_RemainsFiniteAndExact()
        {
            var estimate = Estimate(double.MaxValue, double.MaxValue, double.MaxValue);
            AssertEstimate(estimate, 3, double.MaxValue, double.MaxValue, double.MaxValue, 0d, double.MaxValue, double.MaxValue);
        }

        [Test]
        public void TryEstimate_ExtremeOpposites_ReturnsResultOutOfRange()
        {
            Assert.That(LinearTrendEstimator.TryEstimate(new[] { -double.MaxValue, double.MaxValue }, out var estimate, out var error), Is.False);
            Assert.That(estimate, Is.EqualTo(default(LinearTrendEstimate)));
            Assert.That(error, Is.EqualTo(LinearTrendError.ResultOutOfRange));
        }

        [Test]
        public void TryEstimate_LargeRepresentableLine_RemainsFinite()
        {
            var quarter = double.MaxValue * 0.25d;
            var estimate = Estimate(-quarter, quarter);
            Assert.That(estimate.SlopePerSample, Is.EqualTo(double.MaxValue * 0.5d));
            Assert.That(estimate.PredictedNextSample, Is.EqualTo(double.MaxValue * 0.75d));
            Assert.That(double.IsInfinity(estimate.PredictedNextSample), Is.False);
        }

        [Test]
        public void TryEstimate_DoesNotMutateInputArray()
        {
            var samples = new[] { 10d, 30d, 20d, 40d };
            var before = samples.ToArray();
            Assert.That(LinearTrendEstimator.TryEstimate(samples, out _, out _), Is.True);
            Assert.That(samples, Is.EqualTo(before));
        }

        [Test]
        public void EstimateEquality_UsesAllFields()
        {
            var left = Estimate(10d, 20d, 30d, 40d);
            var right = Estimate(10d, 20d, 30d, 40d);
            var different = Estimate(10d, 30d, 20d, 40d);
            Assert.That(left, Is.EqualTo(right));
            Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
            Assert.That(left == right, Is.True);
            Assert.That(left != different, Is.True);
        }

        [Test]
        public void PublicRuntimeSurface_ContainsExactlySixGameplayAnalysisTypes()
        {
            var publicTypes = typeof(LinearTrendEstimator).Assembly.GetExportedTypes().Where(type => string.Equals(type.Namespace, "GameplayAnalysis", StringComparison.Ordinal)).OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
            Assert.That(publicTypes, Is.EqualTo(new[]
            {
                typeof(LinearTrendError),
                typeof(LinearTrendEstimate),
                typeof(LinearTrendEstimator),
                typeof(SampleStatistics),
                typeof(SampleStatisticsError),
                typeof(SampleStatisticsResult)
            }.OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray()));
        }

        private static LinearTrendEstimate Estimate(params double[] samples)
        {
            Assert.That(LinearTrendEstimator.TryEstimate(samples, out var estimate, out var error), Is.True);
            Assert.That(error, Is.EqualTo(LinearTrendError.None));
            return estimate;
        }

        private static void AssertEstimate(LinearTrendEstimate estimate, int count, double first, double last, double mean, double slope, double intercept, double prediction)
        {
            Assert.That(estimate.SampleCount, Is.EqualTo(count));
            Assert.That(estimate.FirstSample, Is.EqualTo(first));
            Assert.That(estimate.LastSample, Is.EqualTo(last));
            Assert.That(estimate.Mean, Is.EqualTo(mean).Within(Math.Max(1e-12d, Math.Abs(mean) * 1e-14d)));
            Assert.That(estimate.SlopePerSample, Is.EqualTo(slope).Within(Math.Max(1e-12d, Math.Abs(slope) * 1e-14d)));
            Assert.That(estimate.InterceptAtIndexZero, Is.EqualTo(intercept).Within(Math.Max(1e-12d, Math.Abs(intercept) * 1e-14d)));
            Assert.That(estimate.PredictedNextSample, Is.EqualTo(prediction).Within(Math.Max(1e-12d, Math.Abs(prediction) * 1e-14d)));
        }
    }
}
