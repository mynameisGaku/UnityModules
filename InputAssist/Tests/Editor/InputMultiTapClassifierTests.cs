using NUnit.Framework;

namespace InputMultiTapping.Tests
{
    public sealed class InputMultiTapClassifierTests
    {
        [Test]
        public void TryCreate_ZeroGap_IsRejected()
        {
            Assert.That(InputMultiTapClassifier.TryCreate(0, 3, 100, out var classifier, out var error), Is.False);
            Assert.That(classifier, Is.Null);
            Assert.That(error, Is.EqualTo(InputMultiTapError.InvalidMaximumGapTicks));
        }

        [TestCase(1)]
        [TestCase(9)]
        public void TryCreate_UnsupportedMaximumTapCount_IsRejected(int maximumTapCount)
        {
            Assert.That(InputMultiTapClassifier.TryCreate(3, maximumTapCount, 100, out var classifier, out var error), Is.False);
            Assert.That(classifier, Is.Null);
            Assert.That(error, Is.EqualTo(InputMultiTapError.InvalidMaximumTapCount));
        }

        [TestCase(InputMultiTapClassifier.MinimumMaximumTapCount)]
        [TestCase(InputMultiTapClassifier.MaximumMaximumTapCount)]
        public void TryCreate_BoundaryTapCount_IsAccepted(int maximumTapCount)
        {
            Assert.That(InputMultiTapClassifier.TryCreate(3, maximumTapCount, 100, out var classifier, out var error), Is.True);
            Assert.That(classifier, Is.Not.Null);
            Assert.That(classifier.MaximumTapCount, Is.EqualTo(maximumTapCount));
            Assert.That(error, Is.EqualTo(InputMultiTapError.None));
        }

        [Test]
        public void Snapshot_InitialState_IsNeutral()
        {
            var classifier = Create(3, 3, 100);
            var status = classifier.Snapshot();
            Assert.That(status.CurrentTick, Is.EqualTo(100));
            Assert.That(status.HasPendingTaps, Is.False);
            Assert.That(status.PendingTapCount, Is.Zero);
            Assert.That(status.PendingDeadlineTick, Is.Zero);
            Assert.That(status.CompletedThisSample, Is.False);
        }

        [Test]
        public void TrySample_FirstTap_StartsPendingWindow()
        {
            var status = Sample(Create(3, 3, 100), 100, true);
            Assert.That(status.PendingTapCount, Is.EqualTo(1));
            Assert.That(status.PendingDeadlineTick, Is.EqualTo(103));
            Assert.That(status.TapAcceptedThisSample, Is.True);
            Assert.That(status.CompletedThisSample, Is.False);
        }

        [Test]
        public void TrySample_NoTapWithinWindow_KeepsPending()
        {
            var classifier = Create(3, 3, 100);
            Sample(classifier, 100, true);
            var status = Sample(classifier, 102, false);
            Assert.That(status.PendingTapCount, Is.EqualTo(1));
            Assert.That(status.CompletedThisSample, Is.False);
        }

        [Test]
        public void TrySample_TapAtInclusiveDeadline_JoinsBurst()
        {
            var classifier = Create(3, 3, 100);
            Sample(classifier, 100, true);
            var status = Sample(classifier, 103, true);
            Assert.That(status.PendingTapCount, Is.EqualTo(2));
            Assert.That(status.PendingDeadlineTick, Is.EqualTo(106));
            Assert.That(status.CompletedThisSample, Is.False);
        }

        [Test]
        public void TrySample_NoTapAtInclusiveDeadline_DoesNotExpire()
        {
            var classifier = Create(3, 3, 100);
            Sample(classifier, 100, true);
            var status = Sample(classifier, 103, false);
            Assert.That(status.PendingTapCount, Is.EqualTo(1));
            Assert.That(status.CompletedThisSample, Is.False);
        }

        [Test]
        public void TrySample_TickAfterDeadline_CompletesByGap()
        {
            var classifier = Create(3, 3, 100);
            Sample(classifier, 100, true);
            var status = Sample(classifier, 104, false);
            Assert.That(status.PendingTapCount, Is.Zero);
            Assert.That(status.CompletedTapCount, Is.EqualTo(1));
            Assert.That(status.CompletionReason, Is.EqualTo(InputMultiTapCompletionReason.GapExpired));
        }

        [Test]
        public void TrySample_TapAfterDeadline_CompletesOldAndStartsNewBurst()
        {
            var classifier = Create(3, 3, 100);
            Sample(classifier, 100, true);
            var status = Sample(classifier, 104, true);
            Assert.That(status.CompletedTapCount, Is.EqualTo(1));
            Assert.That(status.CompletionReason, Is.EqualTo(InputMultiTapCompletionReason.GapExpired));
            Assert.That(status.PendingTapCount, Is.EqualTo(1));
            Assert.That(status.PendingDeadlineTick, Is.EqualTo(107));
            Assert.That(status.TapAcceptedThisSample, Is.True);
        }

        [Test]
        public void TrySample_MaximumTapCount_CompletesImmediately()
        {
            var classifier = Create(3, 3, 100);
            Sample(classifier, 100, true);
            Sample(classifier, 101, true);
            var status = Sample(classifier, 102, true);
            Assert.That(status.PendingTapCount, Is.Zero);
            Assert.That(status.CompletedTapCount, Is.EqualTo(3));
            Assert.That(status.CompletionReason, Is.EqualTo(InputMultiTapCompletionReason.MaximumReached));
        }

        [Test]
        public void TrySample_MaximumTwo_CompletesDoubleTap()
        {
            var classifier = Create(3, 2, 100);
            Sample(classifier, 100, true);
            var status = Sample(classifier, 102, true);
            Assert.That(status.CompletedTapCount, Is.EqualTo(2));
            Assert.That(status.CompletionReason, Is.EqualTo(InputMultiTapCompletionReason.MaximumReached));
        }

        [Test]
        public void TrySample_SameTickTaps_AreDeterministic()
        {
            var classifier = Create(3, 3, 100);
            Sample(classifier, 100, true);
            Sample(classifier, 100, true);
            var status = Sample(classifier, 100, true);
            Assert.That(status.CompletedTapCount, Is.EqualTo(3));
            Assert.That(status.CompletionReason, Is.EqualTo(InputMultiTapCompletionReason.MaximumReached));
        }

        [Test]
        public void TrySample_DoubleTapExpiresWithCountTwo()
        {
            var classifier = Create(3, 3, 100);
            Sample(classifier, 100, true);
            Sample(classifier, 102, true);
            var status = Sample(classifier, 106, false);
            Assert.That(status.CompletedTapCount, Is.EqualTo(2));
            Assert.That(status.CompletionReason, Is.EqualTo(InputMultiTapCompletionReason.GapExpired));
        }

        [Test]
        public void TrySample_BackwardTick_IsMutationFree()
        {
            var classifier = Create(3, 3, 100);
            Sample(classifier, 102, true);
            var before = classifier.Snapshot();
            Assert.That(classifier.TrySample(101, true, out var rejected, out var error), Is.False);
            Assert.That(error, Is.EqualTo(InputMultiTapError.TickMovedBackward));
            Assert.That(rejected.CurrentTick, Is.EqualTo(before.CurrentTick));
            Assert.That(rejected.PendingTapCount, Is.EqualTo(before.PendingTapCount));
            Assert.That(rejected.PendingDeadlineTick, Is.EqualTo(before.PendingDeadlineTick));
        }

        [Test]
        public void TrySample_DeadlineOverflow_Saturates()
        {
            var classifier = Create(10, 3, ulong.MaxValue - 5);
            var status = Sample(classifier, ulong.MaxValue - 5, true);
            Assert.That(status.PendingDeadlineTick, Is.EqualTo(ulong.MaxValue));
            status = Sample(classifier, ulong.MaxValue, false);
            Assert.That(status.CompletedThisSample, Is.False);
            Assert.That(status.PendingTapCount, Is.EqualTo(1));
        }

        [Test]
        public void Snapshot_ClearsSampleEvents()
        {
            var classifier = Create(3, 2, 100);
            Sample(classifier, 100, true);
            Sample(classifier, 101, true);
            var status = classifier.Snapshot();
            Assert.That(status.TapAcceptedThisSample, Is.False);
            Assert.That(status.CompletedThisSample, Is.False);
            Assert.That(status.CompletedTapCount, Is.Zero);
            Assert.That(status.CompletionReason, Is.EqualTo(InputMultiTapCompletionReason.None));
        }

        [Test]
        public void TrySample_SampleEvents_DoNotStick()
        {
            var classifier = Create(3, 2, 100);
            Sample(classifier, 100, true);
            Sample(classifier, 101, true);
            var status = Sample(classifier, 101, false);
            Assert.That(status.TapAcceptedThisSample, Is.False);
            Assert.That(status.CompletedThisSample, Is.False);
        }

        [Test]
        public void Reset_ClearsPendingAndChangesTimeline()
        {
            var classifier = Create(3, 3, 100);
            Sample(classifier, 102, true);
            classifier.Reset(500);
            var status = classifier.Snapshot();
            Assert.That(status.CurrentTick, Is.EqualTo(500));
            Assert.That(status.PendingTapCount, Is.Zero);
            Assert.That(status.PendingDeadlineTick, Is.Zero);
        }

        [Test]
        public void FiveStepScenario_ClassifiesDoubleThenTriple()
        {
            var classifier = Create(3, 3, 100);
            Assert.That(Sample(classifier, 100, true).PendingTapCount, Is.EqualTo(1));
            Assert.That(Sample(classifier, 102, true).PendingTapCount, Is.EqualTo(2));
            var doubleTap = Sample(classifier, 106, false);
            Assert.That(doubleTap.CompletedTapCount, Is.EqualTo(2));
            Assert.That(doubleTap.CompletionReason, Is.EqualTo(InputMultiTapCompletionReason.GapExpired));
            Assert.That(Sample(classifier, 107, true).PendingTapCount, Is.EqualTo(1));
            Sample(classifier, 108, true);
            var tripleTap = Sample(classifier, 109, true);
            Assert.That(tripleTap.CompletedTapCount, Is.EqualTo(3));
            Assert.That(tripleTap.CompletionReason, Is.EqualTo(InputMultiTapCompletionReason.MaximumReached));
            Assert.That(tripleTap.PendingTapCount, Is.Zero);
        }

        private static InputMultiTapClassifier Create(ulong gap, int maximumTapCount, ulong initialTick)
        {
            Assert.That(InputMultiTapClassifier.TryCreate(gap, maximumTapCount, initialTick, out var classifier, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputMultiTapError.None));
            return classifier;
        }

        private static InputMultiTapStatus Sample(InputMultiTapClassifier classifier, ulong tick, bool tapOccurred)
        {
            Assert.That(classifier.TrySample(tick, tapOccurred, out var status, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputMultiTapError.None));
            return status;
        }
    }
}
