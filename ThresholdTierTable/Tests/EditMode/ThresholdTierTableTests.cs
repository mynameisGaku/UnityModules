using System;
using System.Linq;
using NUnit.Framework;

namespace GameplayProgression.Tests
{
    [TestFixture]
    public sealed class ThresholdTierTableTests
    {
        [TestCase(1)]
        [TestCase(ThresholdTierTable.MaximumTierCount)]
        public void TryCreate_BoundaryCapacity_CreatesEmptyTable(int capacity)
        {
            Assert.That(ThresholdTierTable.TryCreate(capacity, out var table, out var error), Is.True);
            Assert.That(error, Is.EqualTo(ThresholdTierError.None));
            Assert.That(table.Capacity, Is.EqualTo(capacity));
            Assert.That(table.Count, Is.Zero);
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(ThresholdTierTable.MaximumTierCount + 1)]
        public void TryCreate_InvalidCapacity_ReturnsExplicitFailure(int capacity)
        {
            Assert.That(ThresholdTierTable.TryCreate(capacity, out var table, out var error), Is.False);
            Assert.That(table, Is.Null);
            Assert.That(error, Is.EqualTo(ThresholdTierError.InvalidCapacity));
        }

        [TestCase(-1)]
        [TestCase(0)]
        public void TryAddTier_InvalidId_DoesNotMutate(int tierId)
        {
            var table = Create(3);
            Assert.That(table.TryAddTier(tierId, 0d, out var error), Is.False);
            Assert.That(error, Is.EqualTo(ThresholdTierError.InvalidTierId));
            Assert.That(table.Count, Is.Zero);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void TryAddTier_NonFiniteThreshold_DoesNotMutate(double value)
        {
            var table = Create(3);
            Assert.That(table.TryAddTier(1, value, out var error), Is.False);
            Assert.That(error, Is.EqualTo(ThresholdTierError.InvalidMinimumValue));
            Assert.That(table.Count, Is.Zero);
        }

        [Test]
        public void TryAddTier_OutOfOrder_KeepsThresholdOrder()
        {
            var table = Create(3);
            Add(table, 3, 300d);
            Add(table, 1, 0d);
            Add(table, 2, 100d);

            AssertTier(table, 0, 1, 0d);
            AssertTier(table, 1, 2, 100d);
            AssertTier(table, 2, 3, 300d);
        }

        [Test]
        public void TryAddTier_DuplicateId_DoesNotMutate()
        {
            var table = Create(3);
            Add(table, 1, 0d);
            Assert.That(table.TryAddTier(1, 100d, out var error), Is.False);
            Assert.That(error, Is.EqualTo(ThresholdTierError.DuplicateTierId));
            Assert.That(table.Count, Is.EqualTo(1));
        }

        [Test]
        public void TryAddTier_DuplicateThreshold_DoesNotMutate()
        {
            var table = Create(3);
            Add(table, 1, 0d);
            Assert.That(table.TryAddTier(2, -0d, out var error), Is.False);
            Assert.That(error, Is.EqualTo(ThresholdTierError.DuplicateMinimumValue));
            Assert.That(table.Count, Is.EqualTo(1));
        }

        [Test]
        public void TryAddTier_AtCapacity_DoesNotMutate()
        {
            var table = Create(1);
            Add(table, 1, 0d);
            Assert.That(table.TryAddTier(2, 100d, out var error), Is.False);
            Assert.That(error, Is.EqualTo(ThresholdTierError.CapacityExceeded));
            AssertTier(table, 0, 1, 0d);
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void TryGetTierAt_InvalidIndex_ReturnsExplicitFailure(int index)
        {
            var table = Create(2);
            Add(table, 1, 0d);
            Assert.That(table.TryGetTierAt(index, out var tier, out var error), Is.False);
            Assert.That(tier, Is.EqualTo(default(ThresholdTier)));
            Assert.That(error, Is.EqualTo(ThresholdTierError.IndexOutOfRange));
        }

        [Test]
        public void TryRemoveTier_InvalidId_ReturnsExplicitFailure()
        {
            var table = Create(2);
            Assert.That(table.TryRemoveTier(0, out var removed, out var error), Is.False);
            Assert.That(removed, Is.EqualTo(default(ThresholdTier)));
            Assert.That(error, Is.EqualTo(ThresholdTierError.InvalidTierId));
        }

        [Test]
        public void TryRemoveTier_MissingId_ReturnsExplicitFailure()
        {
            var table = Create(2);
            Add(table, 1, 0d);
            Assert.That(table.TryRemoveTier(2, out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(ThresholdTierError.TierNotFound));
            Assert.That(table.Count, Is.EqualTo(1));
        }

        [Test]
        public void TryRemoveTier_ExistingTier_CompactsOrder()
        {
            var table = ThreeTiers();
            Assert.That(table.TryRemoveTier(2, out var removed, out var error), Is.True);
            Assert.That(error, Is.EqualTo(ThresholdTierError.None));
            Assert.That(removed.Id, Is.EqualTo(2));
            Assert.That(removed.MinimumValue, Is.EqualTo(100d));
            Assert.That(table.Count, Is.EqualTo(2));
            AssertTier(table, 0, 1, 0d);
            AssertTier(table, 1, 3, 300d);
        }

        [Test]
        public void Clear_RemovesAllTiersAndKeepsCapacity()
        {
            var table = ThreeTiers();
            table.Clear();
            Assert.That(table.Count, Is.Zero);
            Assert.That(table.Capacity, Is.EqualTo(3));
            Add(table, 4, 40d);
            AssertTier(table, 0, 4, 40d);
        }

        [Test]
        public void TryEvaluate_EmptyTable_ReturnsExplicitFailure()
        {
            var table = Create(1);
            Assert.That(table.TryEvaluate(0d, out var evaluation, out var error), Is.False);
            Assert.That(evaluation, Is.EqualTo(default(ThresholdTierEvaluation)));
            Assert.That(error, Is.EqualTo(ThresholdTierError.TableEmpty));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void TryEvaluate_NonFiniteQuery_ReturnsExplicitFailure(double value)
        {
            var table = ThreeTiers();
            Assert.That(table.TryEvaluate(value, out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(ThresholdTierError.InvalidQueryValue));
        }

        [Test]
        public void TryEvaluate_BelowFirstTier_ReportsFirstAsNext()
        {
            var evaluation = Evaluate(ThreeTiers(), -5d);
            Assert.That(evaluation.QueryValue, Is.EqualTo(-5d));
            Assert.That(evaluation.HasCurrentTier, Is.False);
            Assert.That(evaluation.CurrentTierIndex, Is.EqualTo(-1));
            Assert.That(evaluation.HasNextTier, Is.True);
            Assert.That(evaluation.NextTier.Id, Is.EqualTo(1));
            Assert.That(evaluation.ProgressToNext, Is.Zero);
        }

        [Test]
        public void TryEvaluate_ExactFirstThreshold_SelectsFirstTier()
        {
            var evaluation = Evaluate(ThreeTiers(), 0d);
            AssertCurrent(evaluation, 0, 1, 0d);
            Assert.That(evaluation.NextTier.Id, Is.EqualTo(2));
            Assert.That(evaluation.ProgressToNext, Is.Zero);
        }

        [Test]
        public void TryEvaluate_Midpoint_ReturnsNormalizedProgress()
        {
            var evaluation = Evaluate(ThreeTiers(), 50d);
            AssertCurrent(evaluation, 0, 1, 0d);
            Assert.That(evaluation.NextTier.Id, Is.EqualTo(2));
            Assert.That(evaluation.ProgressToNext, Is.EqualTo(0.5d));
        }

        [Test]
        public void TryEvaluate_ExactNextThreshold_SelectsNextTier()
        {
            var evaluation = Evaluate(ThreeTiers(), 100d);
            AssertCurrent(evaluation, 1, 2, 100d);
            Assert.That(evaluation.NextTier.Id, Is.EqualTo(3));
            Assert.That(evaluation.ProgressToNext, Is.Zero);
        }

        [Test]
        public void TryEvaluate_BetweenSecondAndThird_ReturnsSegmentProgress()
        {
            var evaluation = Evaluate(ThreeTiers(), 250d);
            AssertCurrent(evaluation, 1, 2, 100d);
            Assert.That(evaluation.NextTier.Id, Is.EqualTo(3));
            Assert.That(evaluation.ProgressToNext, Is.EqualTo(0.75d));
        }

        [Test]
        public void TryEvaluate_LastTier_ReturnsTerminalProgress()
        {
            var evaluation = Evaluate(ThreeTiers(), 1000d);
            AssertCurrent(evaluation, 2, 3, 300d);
            Assert.That(evaluation.HasNextTier, Is.False);
            Assert.That(evaluation.ProgressToNext, Is.EqualTo(1d));
        }

        [Test]
        public void TryEvaluate_NegativeThresholds_UsesSameInclusiveRule()
        {
            var table = Create(2);
            Add(table, 1, -20d);
            Add(table, 2, -10d);
            var evaluation = Evaluate(table, -15d);
            AssertCurrent(evaluation, 0, 1, -20d);
            Assert.That(evaluation.ProgressToNext, Is.EqualTo(0.5d));
        }

        [Test]
        public void TryEvaluate_ExtremeOppositeThresholds_AvoidsOverflow()
        {
            var table = Create(2);
            Add(table, 1, -double.MaxValue);
            Add(table, 2, double.MaxValue);
            var evaluation = Evaluate(table, 0d);
            Assert.That(double.IsNaN(evaluation.ProgressToNext), Is.False);
            Assert.That(double.IsInfinity(evaluation.ProgressToNext), Is.False);
            Assert.That(evaluation.ProgressToNext, Is.EqualTo(0.5d));
        }

        [Test]
        public void PublicRuntimeSurface_ContainsExactlyFourTypes()
        {
            var assembly = typeof(ThresholdTierTable).Assembly;
            var publicTypes = assembly.GetExportedTypes().OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
            Assert.That(publicTypes, Is.EqualTo(new[]
            {
                typeof(ThresholdTier),
                typeof(ThresholdTierError),
                typeof(ThresholdTierEvaluation),
                typeof(ThresholdTierTable)
            }.OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray()));
        }

        [Test]
        public void ValueTypes_EqualityUsesAllFields()
        {
            var table = ThreeTiers();
            var left = Evaluate(table, 50d);
            var right = Evaluate(table, 50d);
            var different = Evaluate(table, 75d);
            Assert.That(left, Is.EqualTo(right));
            Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
            Assert.That(left == right, Is.True);
            Assert.That(left != different, Is.True);
            Assert.That(left.CurrentTier, Is.EqualTo(right.CurrentTier));
        }

        private static ThresholdTierTable Create(int capacity)
        {
            Assert.That(ThresholdTierTable.TryCreate(capacity, out var table, out var error), Is.True);
            Assert.That(error, Is.EqualTo(ThresholdTierError.None));
            return table;
        }

        private static ThresholdTierTable ThreeTiers()
        {
            var table = Create(3);
            Add(table, 1, 0d);
            Add(table, 2, 100d);
            Add(table, 3, 300d);
            return table;
        }

        private static void Add(ThresholdTierTable table, int id, double threshold)
        {
            Assert.That(table.TryAddTier(id, threshold, out var error), Is.True);
            Assert.That(error, Is.EqualTo(ThresholdTierError.None));
        }

        private static ThresholdTierEvaluation Evaluate(ThresholdTierTable table, double value)
        {
            Assert.That(table.TryEvaluate(value, out var evaluation, out var error), Is.True);
            Assert.That(error, Is.EqualTo(ThresholdTierError.None));
            return evaluation;
        }

        private static void AssertTier(ThresholdTierTable table, int index, int id, double threshold)
        {
            Assert.That(table.TryGetTierAt(index, out var tier, out var error), Is.True);
            Assert.That(error, Is.EqualTo(ThresholdTierError.None));
            Assert.That(tier.Id, Is.EqualTo(id));
            Assert.That(tier.MinimumValue, Is.EqualTo(threshold));
        }

        private static void AssertCurrent(ThresholdTierEvaluation evaluation, int index, int id, double threshold)
        {
            Assert.That(evaluation.HasCurrentTier, Is.True);
            Assert.That(evaluation.CurrentTierIndex, Is.EqualTo(index));
            Assert.That(evaluation.CurrentTier.Id, Is.EqualTo(id));
            Assert.That(evaluation.CurrentTier.MinimumValue, Is.EqualTo(threshold));
            Assert.That(evaluation.HasNextTier || evaluation.ProgressToNext == 1d, Is.True);
        }
    }
}
