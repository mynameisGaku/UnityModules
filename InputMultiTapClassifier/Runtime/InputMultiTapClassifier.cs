namespace InputMultiTapping
{
    /// <summary>明示tickのtap edgeを有界burstへ分類するEngine非依存state machine。</summary>
    public sealed class InputMultiTapClassifier
    {
        /// <summary>設定できる最小の最大tap数。</summary>
        public const int MinimumMaximumTapCount = 2;

        /// <summary>設定できる最大の最大tap数。</summary>
        public const int MaximumMaximumTapCount = 8;

        private readonly ulong _maximumGapTicks;
        private readonly int _maximumTapCount;
        private ulong _currentTick;
        private ulong _lastTapTick;
        private int _pendingTapCount;

        /// <summary>同じburstへ含めるtap間の最大tick差。</summary>
        public ulong MaximumGapTicks => _maximumGapTicks;

        /// <summary>到達時に待たず確定する最大tap数。</summary>
        public int MaximumTapCount => _maximumTapCount;

        /// <summary>最後に受理したsimulation tick。</summary>
        public ulong CurrentTick => _currentTick;

        private InputMultiTapClassifier(ulong maximumGapTicks, int maximumTapCount, ulong initialTick)
        {
            _maximumGapTicks = maximumGapTicks;
            _maximumTapCount = maximumTapCount;
            _currentTick = initialTick;
            _lastTapTick = initialTick;
        }

        /// <summary>tap gap・最大tap数・初期tickからneutral状態のclassifierを作る。</summary>
        /// <param name="maximumGapTicks">同じburstへ含める正の最大tick差。</param>
        /// <param name="maximumTapCount">到達時に即時確定する2以上8以下のtap数。</param>
        /// <param name="initialTick">最初に受理済みとして扱うsimulation tick。</param>
        /// <param name="classifier">成功時に作成したclassifier。</param>
        /// <param name="error">失敗理由。成功時はNone。</param>
        /// <returns>classifierを作成できた場合はtrue。</returns>
        public static bool TryCreate(ulong maximumGapTicks, int maximumTapCount, ulong initialTick, out InputMultiTapClassifier classifier, out InputMultiTapError error)
        {
            if (maximumGapTicks == 0)
            {
                classifier = null;
                error = InputMultiTapError.InvalidMaximumGapTicks;
                return false;
            }

            if (maximumTapCount < MinimumMaximumTapCount || maximumTapCount > MaximumMaximumTapCount)
            {
                classifier = null;
                error = InputMultiTapError.InvalidMaximumTapCount;
                return false;
            }

            classifier = new InputMultiTapClassifier(maximumGapTicks, maximumTapCount, initialTick);
            error = InputMultiTapError.None;
            return true;
        }

        /// <summary>指定tickでtap edgeの有無を処理し、pending状態と確定eventを返す。</summary>
        /// <param name="tick">前回以上のsimulation tick。</param>
        /// <param name="tapOccurred">このtickでtap edgeが届いた場合はtrue。</param>
        /// <param name="status">受理後のpending状態と今回の確定event。</param>
        /// <param name="error">失敗理由。成功時はNone。</param>
        /// <returns>sampleを受理できた場合はtrue。</returns>
        public bool TrySample(ulong tick, bool tapOccurred, out InputMultiTapStatus status, out InputMultiTapError error)
        {
            if (tick < _currentTick)
            {
                status = Snapshot();
                error = InputMultiTapError.TickMovedBackward;
                return false;
            }

            var completedTapCount = 0;
            var completionReason = InputMultiTapCompletionReason.None;
            if (_pendingTapCount > 0 && tick > Deadline())
            {
                completedTapCount = _pendingTapCount;
                completionReason = InputMultiTapCompletionReason.GapExpired;
                _pendingTapCount = 0;
            }

            if (tapOccurred)
            {
                if (_pendingTapCount == 0) _pendingTapCount = 1;
                else _pendingTapCount++;
                _lastTapTick = tick;
                if (_pendingTapCount == _maximumTapCount)
                {
                    completedTapCount = _pendingTapCount;
                    completionReason = InputMultiTapCompletionReason.MaximumReached;
                    _pendingTapCount = 0;
                }
            }

            _currentTick = tick;
            status = new InputMultiTapStatus(_currentTick, _pendingTapCount, _pendingTapCount > 0 ? Deadline() : 0, tapOccurred, completedTapCount, completionReason);
            error = InputMultiTapError.None;
            return true;
        }

        /// <summary>状態を進めず今回だけのtap・確定eventを持たない現在statusを返す。</summary>
        /// <returns>現在状態のimmutable snapshot。</returns>
        public InputMultiTapStatus Snapshot() => new InputMultiTapStatus(_currentTick, _pendingTapCount, _pendingTapCount > 0 ? Deadline() : 0, false, 0, InputMultiTapCompletionReason.None);

        /// <summary>pending burstを破棄し、指定tickのneutral状態へ初期化する。</summary>
        /// <param name="tick">reset後に受理済みとして扱うsimulation tick。</param>
        public void Reset(ulong tick)
        {
            _currentTick = tick;
            _lastTapTick = tick;
            _pendingTapCount = 0;
        }

        private ulong Deadline() => ulong.MaxValue - _lastTapTick < _maximumGapTicks ? ulong.MaxValue : _lastTapTick + _maximumGapTicks;
    }
}
