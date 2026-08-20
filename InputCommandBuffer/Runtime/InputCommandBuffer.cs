using System;

namespace InputBuffering
{
    /// <summary>明示simulation tickでcommand入力の有効期限とFIFO消費を管理する固定容量buffer。</summary>
    public sealed class InputCommandBuffer
    {
        /// <summary>1 instanceが保持できる最大command数。</summary>
        public const int MaximumCapacity = 1024;

        private readonly BufferedInputCommand[] _entries;
        private readonly ulong _retentionTicks;
        private int _count;
        private ulong _currentTick;
        private ulong _nextSequence;

        /// <summary>保持できるcommand数。</summary>
        public int Capacity => _entries.Length;

        /// <summary>記録tickを含めてcommandを有効とする追加tick数。</summary>
        public ulong RetentionTicks => _retentionTicks;

        /// <summary>最後に明示されたsimulation tick。</summary>
        public ulong CurrentTick => _currentTick;

        /// <summary>現在保持している期限内command数。</summary>
        public int Count => _count;

        private InputCommandBuffer(int capacity, ulong retentionTicks, ulong initialTick)
        {
            _entries = new BufferedInputCommand[capacity];
            _retentionTicks = retentionTicks;
            _currentTick = initialTick;
        }

        /// <summary>固定容量と有効tick数を検証してbufferを作る。</summary>
        /// <param name="capacity">1からMaximumCapacityまでの固定容量。</param>
        /// <param name="retentionTicks">記録tick後も有効とする追加tick数。0は記録tickだけ有効。</param>
        /// <param name="initialTick">最初のsimulation tick。</param>
        /// <param name="buffer">成功時に作られたbuffer。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>作成できた場合true。</returns>
        public static bool TryCreate(int capacity, ulong retentionTicks, ulong initialTick, out InputCommandBuffer buffer, out InputCommandBufferError error)
        {
            if (capacity < 1 || capacity > MaximumCapacity)
            {
                buffer = null;
                error = InputCommandBufferError.InvalidCapacity;
                return false;
            }

            buffer = new InputCommandBuffer(capacity, retentionTicks, initialTick);
            error = InputCommandBufferError.None;
            return true;
        }

        /// <summary>simulation tickを前進させ、期限を超えたcommandを削除する。</summary>
        /// <param name="tick">現在tick以上の新しいtick。</param>
        /// <param name="expiredCount">削除したcommand数。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>tickを受理した場合true。</returns>
        public bool TryAdvanceTo(ulong tick, out int expiredCount, out InputCommandBufferError error)
        {
            if (tick < _currentTick)
            {
                expiredCount = 0;
                error = InputCommandBufferError.TickMovedBackward;
                return false;
            }

            _currentTick = tick;
            expiredCount = RemoveExpired();
            error = InputCommandBufferError.None;
            return true;
        }

        /// <summary>現在tickへcommandを1回記録する。</summary>
        /// <param name="commandId">利用側が定義した正のcommand id。</param>
        /// <param name="command">成功時に記録したimmutable command。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>記録できた場合true。</returns>
        public bool TryRecord(int commandId, out BufferedInputCommand command, out InputCommandBufferError error)
        {
            if (commandId <= 0)
            {
                command = default;
                error = InputCommandBufferError.InvalidCommandId;
                return false;
            }

            if (_count >= _entries.Length)
            {
                command = default;
                error = InputCommandBufferError.CapacityExceeded;
                return false;
            }

            if (_nextSequence == ulong.MaxValue)
            {
                command = default;
                error = InputCommandBufferError.SequenceExhausted;
                return false;
            }

            command = new BufferedInputCommand(commandId, _currentTick, _nextSequence);
            _nextSequence++;
            _entries[_count] = command;
            _count++;
            error = InputCommandBufferError.None;
            return true;
        }

        /// <summary>指定idで最も古いcommandを削除して返す。</summary>
        /// <param name="commandId">検索する正のcommand id。</param>
        /// <param name="command">成功時に消費したcommand。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>commandを消費した場合true。</returns>
        public bool TryConsume(int commandId, out BufferedInputCommand command, out InputCommandBufferError error)
        {
            if (commandId <= 0)
            {
                command = default;
                error = InputCommandBufferError.InvalidCommandId;
                return false;
            }

            for (var index = 0; index < _count; index++)
            {
                if (_entries[index].CommandId != commandId) continue;
                command = _entries[index];
                RemoveAt(index);
                error = InputCommandBufferError.None;
                return true;
            }

            command = default;
            error = InputCommandBufferError.NotFound;
            return false;
        }

        /// <summary>指定idで最も古いcommandを削除せずに返す。</summary>
        /// <param name="commandId">検索する正のcommand id。</param>
        /// <param name="command">成功時に見つかったcommand。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>commandが見つかった場合true。</returns>
        public bool TryPeek(int commandId, out BufferedInputCommand command, out InputCommandBufferError error)
        {
            if (commandId <= 0)
            {
                command = default;
                error = InputCommandBufferError.InvalidCommandId;
                return false;
            }

            for (var index = 0; index < _count; index++)
            {
                if (_entries[index].CommandId != commandId) continue;
                command = _entries[index];
                error = InputCommandBufferError.None;
                return true;
            }

            command = default;
            error = InputCommandBufferError.NotFound;
            return false;
        }

        /// <summary>現在tickを維持したまま全commandを削除し、削除数を返す。</summary>
        /// <returns>削除したcommand数。</returns>
        public int Clear()
        {
            var removed = _count;
            Array.Clear(_entries, 0, _count);
            _count = 0;
            return removed;
        }

        /// <summary>全commandと順序番号を破棄し、新しいsimulation tickへ初期化する。</summary>
        /// <param name="tick">新しいtimelineの初期tick。</param>
        public void Reset(ulong tick)
        {
            Array.Clear(_entries, 0, _count);
            _count = 0;
            _currentTick = tick;
            _nextSequence = 0;
        }

        private int RemoveExpired()
        {
            var write = 0;
            for (var read = 0; read < _count; read++)
            {
                var age = _currentTick - _entries[read].RecordedTick;
                if (age > _retentionTicks) continue;
                if (write != read) _entries[write] = _entries[read];
                write++;
            }

            var removed = _count - write;
            if (removed > 0) Array.Clear(_entries, write, removed);
            _count = write;
            return removed;
        }

        private void RemoveAt(int index)
        {
            var moveCount = _count - index - 1;
            if (moveCount > 0) Array.Copy(_entries, index + 1, _entries, index, moveCount);
            _count--;
            _entries[_count] = default;
        }
    }
}
