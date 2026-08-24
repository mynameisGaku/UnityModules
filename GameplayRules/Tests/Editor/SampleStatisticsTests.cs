using System;
using System.Linq;
using NUnit.Framework;

namespace GameplayAnalysis.Tests
{
    [TestFixture]
    public sealed class SampleStatisticsTests
    {
        [Test]
        public void TryAnalyze_NullWholeArray_ReturnsExplicitFailure()
        {
            Assert.That(SampleStatistics.TryAnalyze(null, out var result, out var error), Is.False);
            Assert.That(result, Is.EqualTo(default(SampleStatisticsResult)));
            Assert.That(error, Is.EqualTo(SampleStatisticsError.NullSamples));
        }

        [Test]
        public void TryAnalyze_NullRange_ReturnsNullBeforeOtherValidation()
        {
            Assert.That(SampleStatistics.TryAnalyze(null, -1, 0, out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(SampleStatisticsError.NullSamples));
        }

        [TestCase(-2)]
        [TestCase(-1)]
        public void TryAnalyze_NegativeStart_ReturnsExplicitFailure(int startIndex)
        {
            Assert.That(SampleStatistics.TryAnalyze(new[] { 1d }, startIndex, 1, out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(SampleStatisticsError.InvalidStartIndex));
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(SampleStatistics.MaximumSampleCount + 1)]
        public void TryAnalyze_InvalidCount_ReturnsExplicitFailure(int count)
        {
            Assert.That(SampleStatistics.TryAnalyze(new double[40], 0, count, out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(SampleStatisticsError.InvalidSampleCount));
        }

        [TestCase(1, 1)]
        [TestCase(2, 1)]
        [TestCase(10, 2)]
        public void TryAnalyze_RangeOutsideArray_ReturnsExplicitFailure(int startIndex, int count)
        {
            Assert.That(SampleStatistics.TryAnalyze(new[] { 1d }, startIndex, count, out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(SampleStatisticsError.RangeOutOfBounds));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void TryAnalyze_NonFiniteSelectedSample_ReturnsExplicitFailure(double invalid)
        {
            Assert.That(SampleStatistics.TryAnalyze(new[] { 1d, invalid, 3d }, out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(SampleStatisticsError.NonFiniteSample));
        }

        [Test]
        public void TryAnalyze_NonFiniteOutsideSelectedRange_IsIgnored()
        {
            var samples = new[] { double.NaN, 1d, 2d, 3d, 4d, double.PositiveInfinity };
            Assert.That(SampleStatistics.TryAnalyze(samples, 1, 4, out var result, out var error), Is.True);
            Assert.That(error, Is.EqualTo(SampleStatisticsError.None));
            AssertResult(result, 4, 1d, 4d, 2.5d, 3d, 1.25d, Math.Sqrt(1.25d));
        }

        [Test]
        public void TryAnalyze_SingleSample_ReturnsZeroSpread()
        {
            AssertResult(Analyze(5d), 1, 5d, 5d, 5d, 0d, 0d, 0d);
        }

        [Test]
        public void TryAnalyze_BalancedSequence_ReturnsPopulationMoments()
        {
            AssertResult(Analyze(1d, 2d, 3d, 4d), 4, 1d, 4d, 2.5d, 3d, 1.25d, Math.Sqrt(1.25d));
        }

        [Test]
        public void TryAnalyze_SymmetricSpread_ReturnsExpectedVariance()
        {
            AssertResult(Analyze(-10d, 0d, 10d), 3, -10d, 10d, 0d, 20d, 200d / 3d, Math.Sqrt(200d / 3d));
        }

        [TestCase(0d)]
        [TestCase(20d)]
        [TestCase(-20d)]
        public void TryAnalyze_ConstantSamples_ReturnZeroSpread(double value)
        {
            AssertResult(Analyze(value, value, value, value), 4, value, value, value, 0d, 0d, 0d);
        }

        [Test]
        public void TryAnalyze_OffsetRange_UsesOnlySelectedSamples()
        {
            var samples = new[] { 999d, 2d, 4d, 6d, -999d };
            Assert.That(SampleStatistics.TryAnalyze(samples, 1, 3, out var result, out var error), Is.True);
            Assert.That(error, Is.EqualTo(SampleStatisticsError.None));
            AssertResult(result, 3, 2d, 6d, 4d, 4d, 8d / 3d, Math.Sqrt(8d / 3d));
        }

        [Test]
        public void TryAnalyze_MaximumCount_ReturnsExpectedMoments()
        {
            var samples = Enumerable.Range(1, SampleStatistics.MaximumSampleCount).Select(value => (double)value).ToArray();
            AssertResult(Analyze(samples), 32, 1d, 32d, 16.5d, 31d, 85.25d, Math.Sqrt(85.25d));
        }

        [Test]
        public void TryAnalyze_IdenticalMaximumValues_RemainsFiniteAndExact()
        {
            AssertResult(Analyze(double.MaxValue, double.MaxValue, double.MaxValue), 3, double.MaxValue, double.MaxValue, double.MaxValue, 0d, 0d, 0d);
        }

        [Test]
        public void TryAnalyze_ExtremeOpposites_ReturnsResultOutOfRange()
        {
            Assert.That(SampleStatistics.TryAnalyze(new[] { -double.MaxValue, double.MaxValue }, out var result, out var error), Is.False);
            Assert.That(result, Is.EqualTo(default(SampleStatisticsResult)));
            Assert.That(error, Is.EqualTo(SampleStatisticsError.ResultOutOfRange));
        }

        [Test]
        public void TryAnalyze_LargeRangeOverflow_ReturnsResultOutOfRange()
        {
            var half = double.MaxValue * 0.5d;
            Assert.That(SampleStatistics.TryAnalyze(new[] { -half, half }, out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(SampleStatisticsError.ResultOutOfRange));
        }

        [Test]
        public void TryAnalyze_SameInputOrder_IsBitStable()
        {
            var first = Analyze(0.1d, 0.2d, 0.3d, 0.4d, 0.5d);
            var second = Analyze(0.1d, 0.2d, 0.3d, 0.4d, 0.5d);
            Assert.That(BitConverter.DoubleToInt64Bits(first.Mean), Is.EqualTo(BitConverter.DoubleToInt64Bits(second.Mean)));
            Assert.That(BitConverter.DoubleToInt64Bits(first.PopulationVariance), Is.EqualTo(BitConverter.DoubleToInt64Bits(second.PopulationVariance)));
            Assert.That(BitConverter.DoubleToInt64Bits(first.PopulationStandardDeviation), Is.EqualTo(BitConverter.DoubleToInt64Bits(second.PopulationStandardDeviation)));
        }

        [Test]
        public void TryAnalyze_DoesNotMutateInputArray()
        {
            var samples = new[] { 1d, 4d, 2d, 3d };
            var before = samples.ToArray();
            Assert.That(SampleStatistics.TryAnalyze(samples, out _, out _), Is.True);
            Assert.That(samples, Is.EqualTo(before));
        }

        [Test]
        public void ResultEquality_UsesAllFields()
        {
            var left = Analyze(1d, 2d, 3d, 4d);
            var right = Analyze(1d, 2d, 3d, 4d);
            var different = Analyze(7d, 7d, 7d, 7d);
            Assert.That(left, Is.EqualTo(right));
            Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
            Assert.That(left == right, Is.True);
            Assert.That(left != different, Is.True);
        }

        [Test]
        public void PublicRuntimeSurface_ContainsExactlySixGameplayAnalysisTypes()
        {
            var publicTypes = typeof(SampleStatistics).Assembly.GetExportedTypes().Where(type => string.Equals(type.Namespace, "GameplayAnalysis", StringComparison.Ordinal)).OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
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

        private static SampleStatisticsResult Analyze(params double[] samples)
        {
            Assert.That(SampleStatistics.TryAnalyze(samples, out var result, out var error), Is.True);
            Assert.That(error, Is.EqualTo(SampleStatisticsError.None));
            return result;
        }

        private static void AssertResult(SampleStatisticsResult result, int count, double minimum, double maximum, double mean, double range, double variance, double standardDeviation)
        {
            Assert.That(result.SampleCount, Is.EqualTo(count));
            Assert.That(result.Minimum, Is.EqualTo(minimum));
            Assert.That(result.Maximum, Is.EqualTo(maximum));
            Assert.That(result.Mean, Is.EqualTo(mean).Within(Tolerance(mean)));
            Assert.That(result.Range, Is.EqualTo(range).Within(Tolerance(range)));
            Assert.That(result.PopulationVariance, Is.EqualTo(variance).Within(Tolerance(variance)));
            Assert.That(result.PopulationStandardDeviation, Is.EqualTo(standardDeviation).Within(Tolerance(standardDeviation)));
        }

        private static double Tolerance(double value)
        {
            return Math.Max(1e-12d, Math.Abs(value) * 1e-14d);
        }
    }
}
