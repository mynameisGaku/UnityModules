using System;

namespace InputSequencing
{
    /// <summary>正のcommand id列を明示tick間隔付きで照合するEngine非依存state machine。</summary>
    public sealed class InputSequenceMatcher
    {
        /// <summary>1 patternに設定できる最大command数。</summary>
        public const int MaximumPatternLength = 64;

        private readonly int[] _pattern;
        private readonly ulong _maximumGapTicks;
        private int _progress;
        private ulong _currentTick;
        private ulong _lastMatchedTick;

        /// <summary>pattern全体のcommand数。</summary>
        public int PatternLength => _pattern.Length;

        /// <summary>隣接する一致command間で許可する最大tick差。</summary>
        public ulong MaximumGapTicks => _maximumGapTicks;

        /// <summary>最後に受理したsimulation tick。</summary>
        public ulong CurrentTick => _currentTick;

        /// <summary>先頭から一致しているcommand数。</summary>
        public int Progress => _progress;

        /// <summary>次に期待するcommand id。</summary>
        public int ExpectedCommandId => _pattern[_progress];

        private InputSequenceMatcher(int[] pattern, ulong maximumGapTicks, ulong initialTick)
        {
            _pattern = pattern;
            _maximumGapTicks = maximumGapTicks;
            _currentTick = initialTick;
        }

        /// <summary>patternを検証・複製してmatcherを作る。</summary>
        /// <param name="pattern">1からMaximumPatternLength個の正のcommand id列。</param>
        /// <param name="maximumGapTicks">隣接する一致command間で許可する最大tick差。</param>
        /// <param name="initialTick">最初のsimulation tick。</param>
        /// <param name="matcher">成功時に作られたmatcher。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>作成できた場合true。</returns>
        public static bool TryCreate(int[] pattern, ulong maximumGapTicks, ulong initialTick, out InputSequenceMatcher matcher, out InputSequenceError error)
        {
            if (pattern == null)
            {
                matcher = null;
                error = InputSequenceError.PatternNull;
                return false;
            }

            if (pattern.Length < 1 || pattern.Length > MaximumPatternLength)
            {
                matcher = null;
                error = InputSequenceError.PatternLengthOutOfRange;
                return false;
            }

            for (var index = 0; index < pattern.Length; index++)
            {
                if (pattern[index] > 0) continue;
                matcher = null;
                error = InputSequenceError.InvalidPatternCommandId;
                return false;
            }

            var copy = new int[pattern.Length];
            Array.Copy(pattern, copy, pattern.Length);
            matcher = new InputSequenceMatcher(copy, maximumGapTicks, initialTick);
            error = InputSequenceError.None;
            return true;
        }

        /// <summary>commandとtickを照合し、処理後statusを返す。</summary>
        /// <param name="tick">現在tick以上のsimulation tick。</param>
        /// <param name="commandId">利用側が定義した正のcommand id。</param>
        /// <param name="status">成功時の処理後status。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>入力を受理した場合true。</returns>
        public bool TryPush(ulong tick, int commandId, out InputSequenceStatus status, out InputSequenceError error)
        {
            if (commandId <= 0)
            {
                status = Snapshot();
                error = InputSequenceError.InvalidCommandId;
                return false;
            }

            if (tick < _currentTick)
            {
                status = Snapshot();
                error = InputSequenceError.TickMovedBackward;
                return false;
            }

            _currentTick = tick;
            var timedOut = false;
            var restarted = false;
            if (_progress > 0 && tick - _lastMatchedTick > _maximumGapTicks)
            {
                _progress = 0;
                timedOut = true;
            }

            if (commandId == _pattern[_progress])
            {
                _progress++;
                _lastMatchedTick = tick;
            }
            else
            {
                restarted = _progress > 0;
                _progress = commandId == _pattern[0] ? 1 : 0;
                if (_progress > 0) _lastMatchedTick = tick;
            }

            var matched = _progress == _pattern.Length;
            if (matched) _progress = 0;
            status = CreateStatus(matched, timedOut, restarted);
            error = InputSequenceError.None;
            return true;
        }

        /// <summary>状態を進めず現在の進捗を返す。</summary>
        /// <returns>terminal flagを持たない現在status。</returns>
        public InputSequenceStatus Snapshot() => CreateStatus(false, false, false);

        /// <summary>進捗を破棄し、新しいsimulation tickへ初期化する。</summary>
        /// <param name="tick">新しいtimelineの開始tick。</param>
        public void Reset(ulong tick)
        {
            _progress = 0;
            _currentTick = tick;
            _lastMatchedTick = 0;
        }

        private InputSequenceStatus CreateStatus(bool matched, bool timedOut, bool restarted) => new InputSequenceStatus(_currentTick, _progress, _pattern.Length, _pattern[_progress], matched, timedOut, restarted);
    }
}
