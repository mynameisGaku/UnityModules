using NUnit.Framework;

namespace InputRepeating.Tests
{
    [TestFixture]
    public sealed class InputRepeatTrackerTests
    {
        [Test]
        public void TryCreate_CapturesConfigurationAndInitialState()
        {
            Assert.That(InputRepeatTracker.TryCreate(3, 2, 100, out var tracker, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputRepeatError.None));
            Assert.That(tracker.InitialDelayTicks, Is.EqualTo(3));
            Assert.That(tracker.RepeatIntervalTicks, Is.EqualTo(2));
            Assert.That(tracker.CurrentTick, Is.EqualTo(100));
            Assert.That(tracker.IsPressed, Is.False);
        }

        [Test]
        public void TryCreate_ZeroInitialDelay_IsRejected()
        {
            Assert.That(InputRepeatTracker.TryCreate(0, 2, 0, out var tracker, out var error), Is.False);
            Assert.That(tracker, Is.Null);
            Assert.That(error, Is.EqualTo(InputRepeatError.InvalidInitialDelay));
        }

        [Test]
        public void TryCreate_ZeroRepeatInterval_IsRejected()
        {
            Assert.That(InputRepeatTracker.TryCreate(3, 0, 0, out var tracker, out var error), Is.False);
            Assert.That(tracker, Is.Null);
            Assert.That(error, Is.EqualTo(InputRepeatError.InvalidRepeatInterval));
        }

        [Test]
        public void PressEdge_TriggersImmediately()
        {
            var status = Push(Create(), 100, true);
            Assert.That(status.IsPressed, Is.True);
            Assert.That(status.InitialTriggered, Is.True);
            Assert.That(status.RepeatTriggerCount, Is.Zero);
            Assert.That(status.TriggerCount, Is.EqualTo(1));
            Assert.That(status.Triggered, Is.True);
            Assert.That(status.Released, Is.False);
        }

        [Test]
        public void HoldBeforeDelay_DoesNotTrigger()
        {
            var tracker = Create();
            Push(tracker, 100, true);
            var status = Push(tracker, 102, true);
            Assert.That(status.TriggerCount, Is.Zero);
            Assert.That(status.Triggered, Is.False);
        }

        [Test]
        public void HoldAtDelay_TriggersFirstRepeatInclusively()
        {
            var tracker = Create();
            Push(tracker, 100, true);
            var status = Push(tracker, 103, true);
            Assert.That(status.InitialTriggered, Is.False);
            Assert.That(status.RepeatTriggerCount, Is.EqualTo(1));
            Assert.That(status.TriggerCount, Is.EqualTo(1));
        }

        [Test]
        public void HoldAtIntervals_TriggersOnlyNewRepeat()
        {
            var tracker = Create();
            Push(tracker, 100, true);
            Assert.That(Push(tracker, 103, true).RepeatTriggerCount, Is.EqualTo(1));
            Assert.That(Push(tracker, 104, true).RepeatTriggerCount, Is.Zero);
            Assert.That(Push(tracker, 105, true).RepeatTriggerCount, Is.EqualTo(1));
            Assert.That(Push(tracker, 107, true).RepeatTriggerCount, Is.EqualTo(1));
        }

        [Test]
        public void SameTick_IsIdempotentAfterPress()
        {
            var tracker = Create();
            Push(tracker, 100, true);
            Assert.That(Push(tracker, 100, true).TriggerCount, Is.Zero);
        }

        [Test]
        public void TickJump_ReturnsAllNewlyDueRepeats()
        {
            var tracker = Create();
            Push(tracker, 100, true);
            var status = Push(tracker, 110, true);
            Assert.That(status.RepeatTriggerCount, Is.EqualTo(4));
            Assert.That(status.TriggerCount, Is.EqualTo(4));
        }

        [Test]
        public void TickJump_AfterPartialProgressReturnsOnlyNewRepeats()
        {
            var tracker = Create();
            Push(tracker, 100, true);
            Assert.That(Push(tracker, 105, true).RepeatTriggerCount, Is.EqualTo(2));
            Assert.That(Push(tracker, 110, true).RepeatTriggerCount, Is.EqualTo(2));
        }

        [Test]
        public void ReleaseEdge_IsReportedWithoutTrigger()
        {
            var tracker = Create();
            Push(tracker, 100, true);
            var status = Push(tracker, 101, false);
            Assert.That(status.IsPressed, Is.False);
            Assert.That(status.Released, Is.True);
            Assert.That(status.TriggerCount, Is.Zero);
        }

        [Test]
        public void RepeatedIdleSample_DoesNotReportReleaseAgain()
        {
            var tracker = Create();
            Push(tracker, 100, true);
            Push(tracker, 101, false);
            Assert.That(Push(tracker, 102, false).Released, Is.False);
        }

        [Test]
        public void Repress_AfterReleaseTriggersNewInitialPulse()
        {
            var tracker = Create();
            Push(tracker, 100, true);
            Push(tracker, 101, false);
            var status = Push(tracker, 102, true);
            Assert.That(status.InitialTriggered, Is.True);
            Assert.That(status.TriggerCount, Is.EqualTo(1));
        }

        [Test]
        public void BackwardTick_IsMutationFree()
        {
            var tracker = Create();
            Push(tracker, 100, true);
            Assert.That(tracker.TryPush(99, false, out var status, out var error), Is.False);
            Assert.That(error, Is.EqualTo(InputRepeatError.TickMovedBackward));
            Assert.That(status.CurrentTick, Is.EqualTo(100));
            Assert.That(status.IsPressed, Is.True);
            Assert.That(status.TriggerCount, Is.Zero);
            Assert.That(tracker.IsPressed, Is.True);
        }

        [Test]
        public void TickZero_PressIsAccepted()
        {
            var tracker = Create(3, 2, 0);
            Assert.That(Push(tracker, 0, true).InitialTriggered, Is.True);
        }

        [Test]
        public void MaximumTick_CatchUpDoesNotOverflow()
        {
            var tracker = Create(ulong.MaxValue, 1, 0);
            Push(tracker, 0, true);
            var status = Push(tracker, ulong.MaxValue, true);
            Assert.That(status.RepeatTriggerCount, Is.EqualTo(1));
        }

        [Test]
        public void MaximumCatchUpCount_IsRepresentable()
        {
            var tracker = Create(1, 1, 0);
            Push(tracker, 0, true);
            var status = Push(tracker, ulong.MaxValue, true);
            Assert.That(status.RepeatTriggerCount, Is.EqualTo(ulong.MaxValue));
        }

        [Test]
        public void Snapshot_DoesNotEmitEdgeOrTrigger()
        {
            var tracker = Create();
            Push(tracker, 100, true);
            var status = tracker.Snapshot();
            Assert.That(status.IsPressed, Is.True);
            Assert.That(status.InitialTriggered, Is.False);
            Assert.That(status.RepeatTriggerCount, Is.Zero);
            Assert.That(status.TriggerCount, Is.Zero);
            Assert.That(status.Released, Is.False);
        }

        [Test]
        public void Reset_ClearsPressedStateAndChangesTimeline()
        {
            var tracker = Create();
            Push(tracker, 100, true);
            tracker.Reset(3);
            Assert.That(tracker.CurrentTick, Is.EqualTo(3));
            Assert.That(tracker.IsPressed, Is.False);
            Assert.That(tracker.Snapshot().Triggered, Is.False);
        }

        [Test]
        public void Reset_AllowsExplicitNewTimelineBeforePreviousTick()
        {
            var tracker = Create();
            Push(tracker, 120, true);
            tracker.Reset(1);
            Assert.That(Push(tracker, 1, true).InitialTriggered, Is.True);
        }

        [Test]
        public void Status_EqualityHashAndOperatorsAgree()
        {
            var tracker = Create();
            var first = tracker.Snapshot();
            var second = tracker.Snapshot();
            Assert.That(first.Equals(second), Is.True);
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        private static InputRepeatTracker Create(ulong delay = 3, ulong interval = 2, ulong tick = 100)
        {
            Assert.That(InputRepeatTracker.TryCreate(delay, interval, tick, out var tracker, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputRepeatError.None));
            return tracker;
        }

        private static InputRepeatStatus Push(InputRepeatTracker tracker, ulong tick, bool pressed)
        {
            Assert.That(tracker.TryPush(tick, pressed, out var status, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputRepeatError.None));
            return status;
        }
    }
}
