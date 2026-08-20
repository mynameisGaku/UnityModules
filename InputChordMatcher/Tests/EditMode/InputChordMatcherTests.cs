using System.Collections.Generic;
using NUnit.Framework;

namespace InputChording.Tests
{
    public sealed class InputChordMatcherTests
    {
        [Test]
        public void TryCreate_ValidConfigurationStartsIncomplete()
        {
            Assert.That(InputChordMatcher.TryCreate(new[] { 3, 1, 2 }, 2, 100, out var matcher, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputChordError.None));
            Assert.That(matcher.RequiredCommandCount, Is.EqualTo(3));
            Assert.That(matcher.MaximumSpanTicks, Is.EqualTo(2));
            Assert.That(matcher.CurrentTick, Is.EqualTo(100));
            Assert.That(matcher.IsComplete, Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(17)]
        public void TryCreate_InvalidRequiredCountIsRejected(int count)
        {
            var ids = new int[count];
            for (var index = 0; index < ids.Length; index++) ids[index] = index + 1;
            Assert.That(InputChordMatcher.TryCreate(ids, 2, 0, out var matcher, out var error), Is.False);
            Assert.That(matcher, Is.Null);
            Assert.That(error, Is.EqualTo(InputChordError.InvalidRequiredCommandCount));
        }

        [Test]
        public void TryCreate_NullRequiredCommandsIsRejected()
        {
            Assert.That(InputChordMatcher.TryCreate(null, 2, 0, out var matcher, out var error), Is.False);
            Assert.That(matcher, Is.Null);
            Assert.That(error, Is.EqualTo(InputChordError.InvalidRequiredCommandCount));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void TryCreate_NonPositiveRequiredCommandIsRejected(int commandId)
        {
            Assert.That(InputChordMatcher.TryCreate(new[] { 1, commandId }, 2, 0, out var matcher, out var error), Is.False);
            Assert.That(matcher, Is.Null);
            Assert.That(error, Is.EqualTo(InputChordError.InvalidRequiredCommandId));
        }

        [Test]
        public void TryCreate_DuplicateRequiredCommandIsRejected()
        {
            Assert.That(InputChordMatcher.TryCreate(new[] { 2, 1, 2 }, 2, 0, out var matcher, out var error), Is.False);
            Assert.That(matcher, Is.Null);
            Assert.That(error, Is.EqualTo(InputChordError.DuplicateRequiredCommandId));
        }

        [Test]
        public void TryCreate_ClonesRequiredCommands()
        {
            var ids = new[] { 1, 2, 3 };
            var matcher = Create(ids);
            ids[0] = 99;
            var status = Sample(matcher, 100, 1, 2, 3);
            Assert.That(status.Triggered, Is.True);
        }

        [Test]
        public void EmptySnapshot_LeavesChordIncomplete()
        {
            var status = Sample(Create(), 100);
            Assert.That(status.PressedRequiredCommandCount, Is.Zero);
            Assert.That(status.IsComplete, Is.False);
            Assert.That(status.Triggered, Is.False);
        }

        [Test]
        public void PartialSnapshot_ReportsRequiredPressedCount()
        {
            var status = Sample(Create(), 100, 1, 3);
            Assert.That(status.PressedRequiredCommandCount, Is.EqualTo(2));
            Assert.That(status.IsComplete, Is.False);
        }

        [Test]
        public void CompletionWithinSpan_TriggersOnce()
        {
            var matcher = Create();
            Sample(matcher, 100, 1);
            Sample(matcher, 101, 1, 2);
            var status = Sample(matcher, 102, 1, 2, 3);
            Assert.That(status.IsComplete, Is.True);
            Assert.That(status.Triggered, Is.True);
            Assert.That(status.SpanExceeded, Is.False);
            Assert.That(status.PressSpanTicks, Is.EqualTo(2));
        }

        [Test]
        public void MaximumSpan_IsInclusive()
        {
            var matcher = Create(maximumSpan: 0);
            var status = Sample(matcher, 100, 1, 2, 3);
            Assert.That(status.Triggered, Is.True);
            Assert.That(status.PressSpanTicks, Is.Zero);
        }

        [Test]
        public void CompletionBeyondSpan_IsRejected()
        {
            var matcher = Create();
            Sample(matcher, 100, 1);
            Sample(matcher, 101, 1, 2);
            var status = Sample(matcher, 103, 1, 2, 3);
            Assert.That(status.Triggered, Is.False);
            Assert.That(status.SpanExceeded, Is.True);
            Assert.That(status.PressSpanTicks, Is.EqualTo(3));
        }

        [Test]
        public void HeldCompleteSnapshot_DoesNotRetrigger()
        {
            var matcher = Create();
            Assert.That(Sample(matcher, 100, 1, 2, 3).Triggered, Is.True);
            var status = Sample(matcher, 101, 1, 2, 3);
            Assert.That(status.IsComplete, Is.True);
            Assert.That(status.Triggered, Is.False);
            Assert.That(status.SpanExceeded, Is.False);
        }

        [Test]
        public void ExtraCommands_DoNotAffectChord()
        {
            var matcher = Create();
            var status = Sample(matcher, 100, 1, 2, 3, 9, 12);
            Assert.That(status.Triggered, Is.True);
            Assert.That(status.PressedRequiredCommandCount, Is.EqualTo(3));
        }

        [Test]
        public void LeavingComplete_RearmsExactlyOnce()
        {
            var matcher = Create();
            Sample(matcher, 100, 1, 2, 3);
            Assert.That(Sample(matcher, 101, 2, 3).Rearmed, Is.True);
            Assert.That(Sample(matcher, 102, 2, 3).Rearmed, Is.False);
        }

        [Test]
        public void RearmedChord_CanTriggerAgain()
        {
            var matcher = Create();
            Sample(matcher, 100, 1, 2, 3);
            Sample(matcher, 101);
            var status = Sample(matcher, 102, 1, 2, 3);
            Assert.That(status.Triggered, Is.True);
        }

        [Test]
        public void LateReentry_UsesHeldCommandEdgeTicks()
        {
            var matcher = Create();
            Sample(matcher, 100, 1);
            Sample(matcher, 101, 1, 2);
            Sample(matcher, 102, 1, 2, 3);
            Sample(matcher, 103, 2, 3);
            var status = Sample(matcher, 106, 1, 2, 3);
            Assert.That(status.SpanExceeded, Is.True);
            Assert.That(status.PressSpanTicks, Is.EqualTo(5));
        }

        [Test]
        public void InvalidPressedSnapshot_IsRejectedWithoutMutation()
        {
            var matcher = Create();
            Sample(matcher, 100, 1);
            Assert.That(matcher.TrySample(101, null, out var status, out var error), Is.False);
            Assert.That(error, Is.EqualTo(InputChordError.InvalidPressedSnapshot));
            Assert.That(status.CurrentTick, Is.EqualTo(100));
            Assert.That(status.PressedRequiredCommandCount, Is.EqualTo(1));
            Assert.That(matcher.CurrentTick, Is.EqualTo(100));
        }

        [TestCaseSource(nameof(InvalidSnapshots))]
        public void NonCanonicalPressedSnapshot_IsRejected(int[] snapshot)
        {
            var matcher = Create();
            Assert.That(matcher.TrySample(100, snapshot, out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(InputChordError.InvalidPressedSnapshot));
        }

        [Test]
        public void TooManyPressedCommands_AreRejected()
        {
            var snapshot = new int[InputChordMatcher.MaximumPressedCommandCount + 1];
            for (var index = 0; index < snapshot.Length; index++) snapshot[index] = index + 1;
            Assert.That(Create().TrySample(100, snapshot, out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(InputChordError.InvalidPressedSnapshot));
        }

        [Test]
        public void BackwardTick_IsRejectedWithoutMutation()
        {
            var matcher = Create();
            Sample(matcher, 102, 1, 2);
            Assert.That(matcher.TrySample(101, new[] { 1, 2, 3 }, out var status, out var error), Is.False);
            Assert.That(error, Is.EqualTo(InputChordError.TickMovedBackward));
            Assert.That(status.CurrentTick, Is.EqualTo(102));
            Assert.That(status.PressedRequiredCommandCount, Is.EqualTo(2));
            Assert.That(matcher.IsComplete, Is.False);
        }

        [Test]
        public void Snapshot_ClearsTerminalFlags()
        {
            var matcher = Create();
            Sample(matcher, 100, 1, 2, 3);
            var status = matcher.Snapshot();
            Assert.That(status.IsComplete, Is.True);
            Assert.That(status.Triggered, Is.False);
            Assert.That(status.SpanExceeded, Is.False);
            Assert.That(status.Rearmed, Is.False);
        }

        [Test]
        public void Reset_ClearsChordAndChangesTimeline()
        {
            var matcher = Create();
            Sample(matcher, 100, 1, 2, 3);
            matcher.Reset(7);
            var status = matcher.Snapshot();
            Assert.That(status.CurrentTick, Is.EqualTo(7));
            Assert.That(status.PressedRequiredCommandCount, Is.Zero);
            Assert.That(status.IsComplete, Is.False);
            Assert.That(Sample(matcher, 7, 1, 2, 3).Triggered, Is.True);
        }

        [Test]
        public void StatusEquality_UsesEveryField()
        {
            var matcher = Create();
            var first = Sample(matcher, 100, 1, 2, 3);
            matcher.Reset(100);
            var same = Sample(matcher, 100, 1, 2, 3);
            var different = matcher.Snapshot();
            Assert.That(first, Is.EqualTo(same));
            Assert.That(first == same, Is.True);
            Assert.That(first != different, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
        }

        private static IEnumerable<int[]> InvalidSnapshots()
        {
            yield return new[] { 2, 1 };
            yield return new[] { 1, 1 };
            yield return new[] { 0, 1 };
            yield return new[] { -1, 2 };
        }

        private static InputChordMatcher Create(IReadOnlyList<int> ids = null, ulong maximumSpan = 2, ulong tick = 100)
        {
            Assert.That(InputChordMatcher.TryCreate(ids ?? new[] { 1, 2, 3 }, maximumSpan, tick, out var matcher, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputChordError.None));
            return matcher;
        }

        private static InputChordStatus Sample(InputChordMatcher matcher, ulong tick, params int[] pressed)
        {
            Assert.That(matcher.TrySample(tick, pressed, out var status, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputChordError.None));
            return status;
        }
    }
}
