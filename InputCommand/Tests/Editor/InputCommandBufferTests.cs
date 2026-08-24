using NUnit.Framework;

namespace InputBuffering.Tests
{
    [TestFixture]
    public sealed class InputCommandBufferTests
    {
        [Test]
        public void TryCreate_CapturesConfiguration()
        {
            Assert.That(InputCommandBuffer.TryCreate(3, 2, 100, out var buffer, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputCommandBufferError.None));
            Assert.That(buffer.Capacity, Is.EqualTo(3));
            Assert.That(buffer.RetentionTicks, Is.EqualTo(2));
            Assert.That(buffer.CurrentTick, Is.EqualTo(100));
            Assert.That(buffer.Count, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(InputCommandBuffer.MaximumCapacity + 1)]
        public void TryCreate_InvalidCapacity_IsRejected(int capacity)
        {
            Assert.That(InputCommandBuffer.TryCreate(capacity, 2, 0, out var buffer, out var error), Is.False);
            Assert.That(buffer, Is.Null);
            Assert.That(error, Is.EqualTo(InputCommandBufferError.InvalidCapacity));
        }

        [TestCase(1)]
        [TestCase(InputCommandBuffer.MaximumCapacity)]
        public void TryCreate_CapacityBoundary_IsAccepted(int capacity)
        {
            Assert.That(InputCommandBuffer.TryCreate(capacity, ulong.MaxValue, ulong.MaxValue, out var buffer, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputCommandBufferError.None));
            Assert.That(buffer.Capacity, Is.EqualTo(capacity));
        }

        [Test]
        public void Record_StoresCurrentTickAndSequence()
        {
            var buffer = Create(3, 2, 10);
            Assert.That(buffer.TryRecord(7, out var command, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputCommandBufferError.None));
            Assert.That(command.CommandId, Is.EqualTo(7));
            Assert.That(command.RecordedTick, Is.EqualTo(10));
            Assert.That(command.Sequence, Is.Zero);
            Assert.That(buffer.Count, Is.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(-4)]
        public void Record_InvalidCommandId_IsMutationFree(int commandId)
        {
            var buffer = Create(2, 2, 10);
            Assert.That(buffer.TryRecord(commandId, out var command, out var error), Is.False);
            Assert.That(command, Is.EqualTo(default(BufferedInputCommand)));
            Assert.That(error, Is.EqualTo(InputCommandBufferError.InvalidCommandId));
            Assert.That(buffer.Count, Is.Zero);
        }

        [Test]
        public void Record_FullCapacity_IsRejectedWithoutRemovingEntries()
        {
            var buffer = Create(2, 2, 10);
            Assert.That(buffer.TryRecord(1, out _, out _), Is.True);
            Assert.That(buffer.TryRecord(2, out _, out _), Is.True);
            Assert.That(buffer.TryRecord(3, out var command, out var error), Is.False);
            Assert.That(command, Is.EqualTo(default(BufferedInputCommand)));
            Assert.That(error, Is.EqualTo(InputCommandBufferError.CapacityExceeded));
            Assert.That(buffer.Count, Is.EqualTo(2));
        }

        [Test]
        public void Advance_SameTick_PreservesCommands()
        {
            var buffer = Create(2, 0, 10);
            Assert.That(buffer.TryRecord(1, out _, out _), Is.True);
            Assert.That(buffer.TryAdvanceTo(10, out var expired, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputCommandBufferError.None));
            Assert.That(expired, Is.Zero);
            Assert.That(buffer.Count, Is.EqualTo(1));
        }

        [Test]
        public void Advance_ZeroRetention_ExpiresOnNextTick()
        {
            var buffer = Create(2, 0, 10);
            Assert.That(buffer.TryRecord(1, out _, out _), Is.True);
            Assert.That(buffer.TryAdvanceTo(11, out var expired, out _), Is.True);
            Assert.That(expired, Is.EqualTo(1));
            Assert.That(buffer.Count, Is.Zero);
        }

        [Test]
        public void Advance_InclusiveRetention_ExpiresOnlyAfterWindow()
        {
            var buffer = Create(2, 2, 10);
            Assert.That(buffer.TryRecord(1, out _, out _), Is.True);
            Assert.That(buffer.TryAdvanceTo(12, out var withinWindow, out _), Is.True);
            Assert.That(withinWindow, Is.Zero);
            Assert.That(buffer.TryAdvanceTo(13, out var expired, out _), Is.True);
            Assert.That(expired, Is.EqualTo(1));
        }

        [Test]
        public void Advance_BackwardTick_IsMutationFree()
        {
            var buffer = Create(2, 2, 10);
            Assert.That(buffer.TryRecord(1, out _, out _), Is.True);
            Assert.That(buffer.TryAdvanceTo(9, out var expired, out var error), Is.False);
            Assert.That(expired, Is.Zero);
            Assert.That(error, Is.EqualTo(InputCommandBufferError.TickMovedBackward));
            Assert.That(buffer.CurrentTick, Is.EqualTo(10));
            Assert.That(buffer.Count, Is.EqualTo(1));
        }

        [Test]
        public void Advance_RemovesOnlyCommandsOutsideWindow()
        {
            var buffer = Create(4, 2, 10);
            Assert.That(buffer.TryRecord(1, out _, out _), Is.True);
            Assert.That(buffer.TryAdvanceTo(11, out _, out _), Is.True);
            Assert.That(buffer.TryRecord(2, out _, out _), Is.True);
            Assert.That(buffer.TryAdvanceTo(13, out var expired, out _), Is.True);
            Assert.That(expired, Is.EqualTo(1));
            Assert.That(buffer.Count, Is.EqualTo(1));
            Assert.That(buffer.TryPeek(2, out _, out _), Is.True);
        }

        [Test]
        public void Peek_ReturnsOldestMatchingWithoutMutation()
        {
            var buffer = Create(3, 2, 10);
            Assert.That(buffer.TryRecord(4, out var first, out _), Is.True);
            Assert.That(buffer.TryRecord(4, out _, out _), Is.True);
            Assert.That(buffer.TryPeek(4, out var peeked, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputCommandBufferError.None));
            Assert.That(peeked, Is.EqualTo(first));
            Assert.That(buffer.Count, Is.EqualTo(2));
        }

        [Test]
        public void Consume_DuplicateCommands_UsesFifoOrder()
        {
            var buffer = Create(3, 2, 10);
            Assert.That(buffer.TryRecord(4, out var first, out _), Is.True);
            Assert.That(buffer.TryRecord(4, out var second, out _), Is.True);
            Assert.That(buffer.TryConsume(4, out var consumedFirst, out _), Is.True);
            Assert.That(buffer.TryConsume(4, out var consumedSecond, out _), Is.True);
            Assert.That(consumedFirst, Is.EqualTo(first));
            Assert.That(consumedSecond, Is.EqualTo(second));
            Assert.That(buffer.Count, Is.Zero);
        }

        [Test]
        public void Consume_OnlyRemovesMatchingCommand()
        {
            var buffer = Create(3, 2, 10);
            Assert.That(buffer.TryRecord(1, out _, out _), Is.True);
            Assert.That(buffer.TryRecord(2, out var second, out _), Is.True);
            Assert.That(buffer.TryConsume(1, out _, out _), Is.True);
            Assert.That(buffer.Count, Is.EqualTo(1));
            Assert.That(buffer.TryPeek(2, out var remaining, out _), Is.True);
            Assert.That(remaining, Is.EqualTo(second));
        }

        [Test]
        public void Consume_MissingCommand_ReturnsNotFound()
        {
            var buffer = Create(2, 2, 10);
            Assert.That(buffer.TryConsume(7, out var command, out var error), Is.False);
            Assert.That(command, Is.EqualTo(default(BufferedInputCommand)));
            Assert.That(error, Is.EqualTo(InputCommandBufferError.NotFound));
        }

        [Test]
        public void Consume_InvalidCommandId_ReturnsInvalidCommandId()
        {
            var buffer = Create(2, 2, 10);
            Assert.That(buffer.TryConsume(0, out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(InputCommandBufferError.InvalidCommandId));
        }

        [Test]
        public void ExpiredCapacity_CanBeReusedAfterAdvance()
        {
            var buffer = Create(1, 0, 10);
            Assert.That(buffer.TryRecord(1, out _, out _), Is.True);
            Assert.That(buffer.TryAdvanceTo(11, out var expired, out _), Is.True);
            Assert.That(expired, Is.EqualTo(1));
            Assert.That(buffer.TryRecord(2, out _, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputCommandBufferError.None));
        }

        [Test]
        public void Clear_RemovesAllWithoutChangingTick()
        {
            var buffer = Create(3, 2, 10);
            Assert.That(buffer.TryRecord(1, out _, out _), Is.True);
            Assert.That(buffer.TryRecord(2, out _, out _), Is.True);
            Assert.That(buffer.Clear(), Is.EqualTo(2));
            Assert.That(buffer.Clear(), Is.Zero);
            Assert.That(buffer.Count, Is.Zero);
            Assert.That(buffer.CurrentTick, Is.EqualTo(10));
        }

        [Test]
        public void Reset_StartsNewTimelineAndSequence()
        {
            var buffer = Create(2, 2, 10);
            Assert.That(buffer.TryRecord(1, out _, out _), Is.True);
            buffer.Reset(3);
            Assert.That(buffer.CurrentTick, Is.EqualTo(3));
            Assert.That(buffer.Count, Is.Zero);
            Assert.That(buffer.TryRecord(2, out var command, out _), Is.True);
            Assert.That(command.RecordedTick, Is.EqualTo(3));
            Assert.That(command.Sequence, Is.Zero);
        }

        [Test]
        public void RetentionAtUlongMaximum_DoesNotOverflow()
        {
            var buffer = Create(1, ulong.MaxValue, 0);
            Assert.That(buffer.TryRecord(1, out _, out _), Is.True);
            Assert.That(buffer.TryAdvanceTo(ulong.MaxValue, out var expired, out _), Is.True);
            Assert.That(expired, Is.Zero);
            Assert.That(buffer.Count, Is.EqualTo(1));
        }

        [Test]
        public void TickNearUlongMaximum_UsesSubtractionWithoutOverflow()
        {
            var buffer = Create(1, 1, ulong.MaxValue - 1);
            Assert.That(buffer.TryRecord(1, out _, out _), Is.True);
            Assert.That(buffer.TryAdvanceTo(ulong.MaxValue, out var expired, out _), Is.True);
            Assert.That(expired, Is.Zero);
        }

        [Test]
        public void BufferedCommand_EqualityHashAndOperators_Agree()
        {
            var buffer = Create(2, 2, 10);
            Assert.That(buffer.TryRecord(3, out var command, out _), Is.True);
            Assert.That(buffer.TryPeek(3, out var same, out _), Is.True);
            Assert.That(command.Equals(same), Is.True);
            Assert.That(command == same, Is.True);
            Assert.That(command != same, Is.False);
            Assert.That(command.GetHashCode(), Is.EqualTo(same.GetHashCode()));
            Assert.That(command, Is.Not.EqualTo(default(BufferedInputCommand)));
        }

        private static InputCommandBuffer Create(int capacity, ulong retentionTicks, ulong initialTick)
        {
            Assert.That(InputCommandBuffer.TryCreate(capacity, retentionTicks, initialTick, out var buffer, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputCommandBufferError.None));
            return buffer;
        }
    }
}
