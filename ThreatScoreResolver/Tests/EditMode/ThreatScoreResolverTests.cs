using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace GameplayThreat.Tests
{
    [TestFixture]
    public sealed class ThreatScoreResolverTests
    {
        [Test]
        public void NoAdjustments_PreservesEntryAndLeader()
        {
            var entries = new[] { new ThreatScoreEntry(7, 12.5d) };

            var succeeded = ThreatScoreResolver.TryResolve(entries, Array.Empty<ThreatScoreAdjustment>(), out var result, out var error, out var failureIndex);

            Assert.That(succeeded, Is.True);
            Assert.That(error, Is.EqualTo(ThreatScoreError.None));
            Assert.That(failureIndex, Is.EqualTo(-1));
            Assert.That(result.EntryCount, Is.EqualTo(1));
            Assert.That(result.StepCount, Is.EqualTo(0));
            Assert.That(result.LeaderTargetId, Is.EqualTo(7));
            Assert.That(result.LeaderScore, Is.EqualTo(12.5d));
        }

        [Test]
        public void Adjustments_AreAppliedInInputOrder()
        {
            var entries = new[] { new ThreatScoreEntry(1, 10d) };
            var adjustments = new[]
            {
                new ThreatScoreAdjustment(1, 5d),
                new ThreatScoreAdjustment(1, -3d),
                new ThreatScoreAdjustment(1, 2d)
            };

            Assert.That(ThreatScoreResolver.TryResolve(entries, adjustments, out var result, out _, out _), Is.True);
            Assert.That(result.TryGetEntry(0, out var finalEntry), Is.True);
            Assert.That(finalEntry.Score, Is.EqualTo(14d));
            AssertStep(result, 0, 10d, 5d, 5d, 15d, false);
            AssertStep(result, 1, 15d, -3d, -3d, 12d, false);
            AssertStep(result, 2, 12d, 2d, 2d, 14d, false);
        }

        [Test]
        public void ExcessiveNegativeDelta_ClampsAtZeroAndRecordsAppliedAmount()
        {
            var entries = new[] { new ThreatScoreEntry(3, 10d) };
            var adjustments = new[] { new ThreatScoreAdjustment(3, -25d) };

            Assert.That(ThreatScoreResolver.TryResolve(entries, adjustments, out var result, out _, out _), Is.True);
            AssertStep(result, 0, 10d, -25d, -10d, 0d, true);
            Assert.That(result.LeaderScore, Is.Zero);
        }

        [Test]
        public void ExactNegativeDelta_ReachesZeroWithoutClampFlag()
        {
            var entries = new[] { new ThreatScoreEntry(3, 10d) };

            Assert.That(ThreatScoreResolver.TryResolve(entries, new[] { new ThreatScoreAdjustment(3, -10d) }, out var result, out _, out _), Is.True);
            AssertStep(result, 0, 10d, -10d, -10d, 0d, false);
        }

        [Test]
        public void Leader_UsesSmallestTargetIdWhenScoresTie()
        {
            var entries = new[]
            {
                new ThreatScoreEntry(9, 20d),
                new ThreatScoreEntry(2, 10d),
                new ThreatScoreEntry(5, 20d)
            };

            Assert.That(ThreatScoreResolver.TryResolve(entries, Array.Empty<ThreatScoreAdjustment>(), out var result, out _, out _), Is.True);
            Assert.That(result.LeaderTargetId, Is.EqualTo(5));
            Assert.That(result.LeaderScore, Is.EqualTo(20d));
        }

        [Test]
        public void FinalEntries_PreserveInitialInputOrder()
        {
            var entries = new[]
            {
                new ThreatScoreEntry(8, 1d),
                new ThreatScoreEntry(3, 2d),
                new ThreatScoreEntry(6, 3d)
            };
            var adjustments = new[] { new ThreatScoreAdjustment(3, 10d) };

            Assert.That(ThreatScoreResolver.TryResolve(entries, adjustments, out var result, out _, out _), Is.True);
            Assert.That(result.TryGetEntry(0, out var first), Is.True);
            Assert.That(result.TryGetEntry(1, out var second), Is.True);
            Assert.That(result.TryGetEntry(2, out var third), Is.True);
            Assert.That(new[] { first.TargetId, second.TargetId, third.TargetId }, Is.EqualTo(new[] { 8, 3, 6 }));
        }

        [Test]
        public void Resolution_DoesNotMutateInputArrays()
        {
            var entries = new[] { new ThreatScoreEntry(1, 4d), new ThreatScoreEntry(2, 5d) };
            var adjustments = new[] { new ThreatScoreAdjustment(1, 6d) };

            Assert.That(ThreatScoreResolver.TryResolve(entries, adjustments, out _, out _, out _), Is.True);
            Assert.That(entries[0].Score, Is.EqualTo(4d));
            Assert.That(entries[1].Score, Is.EqualTo(5d));
            Assert.That(adjustments[0].Delta, Is.EqualTo(6d));
        }

        [Test]
        public void NullEntries_ReturnsExplicitError()
        {
            AssertFailure(null, Array.Empty<ThreatScoreAdjustment>(), ThreatScoreError.NullEntries, -1);
        }

        [Test]
        public void EmptyEntries_ReturnsCountError()
        {
            AssertFailure(Array.Empty<ThreatScoreEntry>(), Array.Empty<ThreatScoreAdjustment>(), ThreatScoreError.EntryCountOutOfRange, -1);
        }

        [Test]
        public void TooManyEntries_ReturnsCountError()
        {
            AssertFailure(new ThreatScoreEntry[33], Array.Empty<ThreatScoreAdjustment>(), ThreatScoreError.EntryCountOutOfRange, -1);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NonPositiveTargetId_ReturnsEntryIndex(int targetId)
        {
            AssertFailure(new[] { new ThreatScoreEntry(1, 0d), new ThreatScoreEntry(targetId, 0d) }, Array.Empty<ThreatScoreAdjustment>(), ThreatScoreError.InvalidTargetId, 1);
        }

        [Test]
        public void DuplicateTargetId_ReturnsSecondIndex()
        {
            AssertFailure(new[] { new ThreatScoreEntry(4, 1d), new ThreatScoreEntry(4, 2d) }, Array.Empty<ThreatScoreAdjustment>(), ThreatScoreError.DuplicateTargetId, 1);
        }

        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void InvalidInitialScore_ReturnsEntryIndex(double score)
        {
            AssertFailure(new[] { new ThreatScoreEntry(1, score) }, Array.Empty<ThreatScoreAdjustment>(), ThreatScoreError.InvalidInitialScore, 0);
        }

        [Test]
        public void NullAdjustments_ReturnsExplicitError()
        {
            AssertFailure(new[] { new ThreatScoreEntry(1, 0d) }, null, ThreatScoreError.NullAdjustments, -1);
        }

        [Test]
        public void TooManyAdjustments_ReturnsCountError()
        {
            AssertFailure(new[] { new ThreatScoreEntry(1, 0d) }, new ThreatScoreAdjustment[65], ThreatScoreError.AdjustmentCountOutOfRange, -1);
        }

        [Test]
        public void UnknownAdjustmentTarget_ReturnsAdjustmentIndex()
        {
            var adjustments = new[] { new ThreatScoreAdjustment(1, 1d), new ThreatScoreAdjustment(99, 1d) };
            AssertFailure(new[] { new ThreatScoreEntry(1, 0d) }, adjustments, ThreatScoreError.UnknownTargetId, 1);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void NonFiniteDelta_ReturnsAdjustmentIndex(double delta)
        {
            AssertFailure(new[] { new ThreatScoreEntry(1, 0d) }, new[] { new ThreatScoreAdjustment(1, delta) }, ThreatScoreError.InvalidAdjustmentDelta, 0);
        }

        [Test]
        public void PositiveOverflow_ReturnsAdjustmentIndexWithoutResult()
        {
            AssertFailure(new[] { new ThreatScoreEntry(1, double.MaxValue) }, new[] { new ThreatScoreAdjustment(1, double.MaxValue) }, ThreatScoreError.ScoreOverflow, 0);
        }

        [Test]
        public void MaximumCounts_AreAccepted()
        {
            var entries = new ThreatScoreEntry[32];
            for (var index = 0; index < entries.Length; index++) entries[index] = new ThreatScoreEntry(index + 1, index);
            var adjustments = new ThreatScoreAdjustment[64];
            for (var index = 0; index < adjustments.Length; index++) adjustments[index] = new ThreatScoreAdjustment(index % 32 + 1, 1d);

            Assert.That(ThreatScoreResolver.TryResolve(entries, adjustments, out var result, out var error, out var failureIndex), Is.True);
            Assert.That(error, Is.EqualTo(ThreatScoreError.None));
            Assert.That(failureIndex, Is.EqualTo(-1));
            Assert.That(result.EntryCount, Is.EqualTo(32));
            Assert.That(result.StepCount, Is.EqualTo(64));
        }

        [Test]
        public void Accessors_ReturnFalseOutsideBounds()
        {
            Assert.That(ThreatScoreResolver.TryResolve(new[] { new ThreatScoreEntry(1, 0d) }, Array.Empty<ThreatScoreAdjustment>(), out var result, out _, out _), Is.True);
            Assert.That(result.TryGetEntry(-1, out _), Is.False);
            Assert.That(result.TryGetEntry(1, out _), Is.False);
            Assert.That(result.TryGetStep(-1, out _), Is.False);
            Assert.That(result.TryGetStep(0, out _), Is.False);
        }

        private static void AssertFailure(
            IReadOnlyList<ThreatScoreEntry> entries,
            IReadOnlyList<ThreatScoreAdjustment> adjustments,
            ThreatScoreError expectedError,
            int expectedIndex)
        {
            var succeeded = ThreatScoreResolver.TryResolve(entries, adjustments, out var result, out var error, out var failureIndex);
            Assert.That(succeeded, Is.False);
            Assert.That(result, Is.Null);
            Assert.That(error, Is.EqualTo(expectedError));
            Assert.That(failureIndex, Is.EqualTo(expectedIndex));
        }

        private static void AssertStep(ThreatScoreResolution result, int index, double input, double requested, double applied, double output, bool clamped)
        {
            Assert.That(result.TryGetStep(index, out var step), Is.True);
            Assert.That(step.AdjustmentIndex, Is.EqualTo(index));
            Assert.That(step.InputScore, Is.EqualTo(input));
            Assert.That(step.RequestedDelta, Is.EqualTo(requested));
            Assert.That(step.AppliedDelta, Is.EqualTo(applied));
            Assert.That(step.OutputScore, Is.EqualTo(output));
            Assert.That(step.WasClamped, Is.EqualTo(clamped));
        }
    }
}
