using NUnit.Framework;

namespace InputSequencing.Tests
{
    [TestFixture]
    public sealed class InputSequenceMatcherTests
    {
        [Test]
        public void TryCreate_CapturesConfigurationAndClonesPattern()
        {
            var pattern = new[] { 1, 1, 2 };
            Assert.That(InputSequenceMatcher.TryCreate(pattern, 2, 100, out var matcher, out var error), Is.True);
            pattern[0] = 9;
            Assert.That(error, Is.EqualTo(InputSequenceError.None));
            Assert.That(matcher.PatternLength, Is.EqualTo(3));
            Assert.That(matcher.MaximumGapTicks, Is.EqualTo(2));
            Assert.That(matcher.CurrentTick, Is.EqualTo(100));
            Assert.That(matcher.ExpectedCommandId, Is.EqualTo(1));
        }

        [Test]
        public void TryCreate_NullPattern_IsRejected()
        {
            Assert.That(InputSequenceMatcher.TryCreate(null, 2, 0, out var matcher, out var error), Is.False);
            Assert.That(matcher, Is.Null);
            Assert.That(error, Is.EqualTo(InputSequenceError.PatternNull));
        }

        [TestCase(0)]
        [TestCase(InputSequenceMatcher.MaximumPatternLength + 1)]
        public void TryCreate_LengthBoundary_IsRejected(int length)
        {
            Assert.That(InputSequenceMatcher.TryCreate(new int[length], 2, 0, out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(InputSequenceError.PatternLengthOutOfRange));
        }

        [Test]
        public void TryCreate_MaximumPatternLength_IsAccepted()
        {
            var pattern = new int[InputSequenceMatcher.MaximumPatternLength];
            for (var index = 0; index < pattern.Length; index++) pattern[index] = index + 1;
            Assert.That(InputSequenceMatcher.TryCreate(pattern, 2, 0, out var matcher, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputSequenceError.None));
            Assert.That(matcher.PatternLength, Is.EqualTo(InputSequenceMatcher.MaximumPatternLength));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void TryCreate_InvalidPatternCommand_IsRejected(int commandId)
        {
            Assert.That(InputSequenceMatcher.TryCreate(new[] { 1, commandId, 2 }, 2, 0, out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(InputSequenceError.InvalidPatternCommandId));
        }

        [Test]
        public void Push_GoldenSequence_MatchesAndResetsProgress()
        {
            var matcher = Create(new[] { 1, 1, 2 }, 2, 100);
            AssertPush(matcher, 100, 1, 1, false);
            AssertPush(matcher, 101, 1, 2, false);
            var status = Push(matcher, 102, 2);
            Assert.That(status.Matched, Is.True);
            Assert.That(status.Progress, Is.Zero);
            Assert.That(status.ExpectedCommandId, Is.EqualTo(1));
            Assert.That(matcher.Progress, Is.Zero);
        }

        [Test]
        public void Push_SingleCommandPattern_MatchesEveryInput()
        {
            var matcher = Create(new[] { 7 }, 0, 10);
            Assert.That(Push(matcher, 10, 7).Matched, Is.True);
            Assert.That(Push(matcher, 10, 7).Matched, Is.True);
        }

        [Test]
        public void Push_GapAtLimit_IsAccepted()
        {
            var matcher = Create(new[] { 1, 2 }, 2, 10);
            AssertPush(matcher, 10, 1, 1, false);
            var status = Push(matcher, 12, 2);
            Assert.That(status.Matched, Is.True);
            Assert.That(status.TimedOut, Is.False);
        }

        [Test]
        public void Push_GapBeyondLimit_TimesOutThenRestartsWithFirstCommand()
        {
            var matcher = Create(new[] { 1, 2 }, 2, 10);
            AssertPush(matcher, 10, 1, 1, false);
            var status = Push(matcher, 13, 1);
            Assert.That(status.TimedOut, Is.True);
            Assert.That(status.Progress, Is.EqualTo(1));
            Assert.That(status.Matched, Is.False);
        }

        [Test]
        public void Push_GapBeyondLimitWithNonFirstCommand_ResetsToZero()
        {
            var matcher = Create(new[] { 1, 2 }, 2, 10);
            AssertPush(matcher, 10, 1, 1, false);
            var status = Push(matcher, 13, 2);
            Assert.That(status.TimedOut, Is.True);
            Assert.That(status.Progress, Is.Zero);
        }

        [Test]
        public void Push_MismatchThatIsFirstCommand_RestartsAtOne()
        {
            var matcher = Create(new[] { 1, 2, 3 }, 3, 10);
            AssertPush(matcher, 10, 1, 1, false);
            AssertPush(matcher, 11, 2, 2, false);
            var status = Push(matcher, 12, 1);
            Assert.That(status.Restarted, Is.True);
            Assert.That(status.Progress, Is.EqualTo(1));
        }

        [Test]
        public void Push_MismatchThatIsNotFirstCommand_ResetsToZero()
        {
            var matcher = Create(new[] { 1, 2, 3 }, 3, 10);
            AssertPush(matcher, 10, 1, 1, false);
            var status = Push(matcher, 11, 9);
            Assert.That(status.Restarted, Is.True);
            Assert.That(status.Progress, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(-4)]
        public void Push_InvalidCommand_IsMutationFree(int commandId)
        {
            var matcher = Create(new[] { 1, 2 }, 2, 10);
            AssertPush(matcher, 10, 1, 1, false);
            Assert.That(matcher.TryPush(11, commandId, out var status, out var error), Is.False);
            Assert.That(error, Is.EqualTo(InputSequenceError.InvalidCommandId));
            Assert.That(status.Progress, Is.EqualTo(1));
            Assert.That(matcher.CurrentTick, Is.EqualTo(10));
        }

        [Test]
        public void Push_BackwardTick_IsMutationFree()
        {
            var matcher = Create(new[] { 1, 2 }, 2, 10);
            AssertPush(matcher, 10, 1, 1, false);
            Assert.That(matcher.TryPush(9, 2, out var status, out var error), Is.False);
            Assert.That(error, Is.EqualTo(InputSequenceError.TickMovedBackward));
            Assert.That(status.Progress, Is.EqualTo(1));
            Assert.That(matcher.CurrentTick, Is.EqualTo(10));
        }

        [Test]
        public void Push_SameTick_IsAcceptedWhenMaximumGapIsZero()
        {
            var matcher = Create(new[] { 1, 2 }, 0, 10);
            AssertPush(matcher, 10, 1, 1, false);
            Assert.That(Push(matcher, 10, 2).Matched, Is.True);
        }

        [Test]
        public void Push_NextTick_TimesOutWhenMaximumGapIsZero()
        {
            var matcher = Create(new[] { 1, 2 }, 0, 10);
            AssertPush(matcher, 10, 1, 1, false);
            var status = Push(matcher, 11, 2);
            Assert.That(status.TimedOut, Is.True);
            Assert.That(status.Matched, Is.False);
        }

        [Test]
        public void MaximumTickDifference_DoesNotOverflow()
        {
            var matcher = Create(new[] { 1, 2 }, ulong.MaxValue, 0);
            AssertPush(matcher, 0, 1, 1, false);
            Assert.That(Push(matcher, ulong.MaxValue, 2).Matched, Is.True);
        }

        [Test]
        public void Snapshot_DoesNotAdvanceOrEmitTerminalFlags()
        {
            var matcher = Create(new[] { 1, 2 }, 2, 10);
            AssertPush(matcher, 10, 1, 1, false);
            var status = matcher.Snapshot();
            Assert.That(status.Progress, Is.EqualTo(1));
            Assert.That(status.Matched, Is.False);
            Assert.That(status.TimedOut, Is.False);
            Assert.That(status.Restarted, Is.False);
        }

        [Test]
        public void Reset_ClearsProgressAndChangesTimeline()
        {
            var matcher = Create(new[] { 1, 2 }, 2, 10);
            AssertPush(matcher, 10, 1, 1, false);
            matcher.Reset(3);
            Assert.That(matcher.CurrentTick, Is.EqualTo(3));
            Assert.That(matcher.Progress, Is.Zero);
            Assert.That(matcher.ExpectedCommandId, Is.EqualTo(1));
        }

        [Test]
        public void Status_EqualityHashAndOperators_Agree()
        {
            var matcher = Create(new[] { 1, 2 }, 2, 10);
            var first = matcher.Snapshot();
            var second = matcher.Snapshot();
            Assert.That(first.Equals(second), Is.True);
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        private static InputSequenceMatcher Create(int[] pattern, ulong gap, ulong tick)
        {
            Assert.That(InputSequenceMatcher.TryCreate(pattern, gap, tick, out var matcher, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputSequenceError.None));
            return matcher;
        }

        private static InputSequenceStatus Push(InputSequenceMatcher matcher, ulong tick, int commandId)
        {
            Assert.That(matcher.TryPush(tick, commandId, out var status, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputSequenceError.None));
            return status;
        }

        private static void AssertPush(InputSequenceMatcher matcher, ulong tick, int commandId, int progress, bool matched)
        {
            var status = Push(matcher, tick, commandId);
            Assert.That(status.Progress, Is.EqualTo(progress));
            Assert.That(status.Matched, Is.EqualTo(matched));
        }
    }
}
