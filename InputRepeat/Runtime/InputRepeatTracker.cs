namespace InputRepeating
{
    /// <summary>明示tickとpressed状態から初回・保持repeatの発行数を計算するEngine非依存state machine。</summary>
    public sealed class InputRepeatTracker
    {
        private readonly ulong _initialDelayTicks;
        private readonly ulong _repeatIntervalTicks;
        private ulong _currentTick;
        private bool _isPressed;
        private ulong _pressTick;
        private ulong _emittedRepeatCount;

        /// <summary>押下edgeから最初のrepeatまでに必要なtick差。</summary>
        public ulong InitialDelayTicks => _initialDelayTicks;

        /// <summary>2回目以降のrepeat間隔tick数。</summary>
        public ulong RepeatIntervalTicks => _repeatIntervalTicks;

        /// <summary>最後に受理したsimulation tick。</summary>
        public ulong CurrentTick => _currentTick;

        /// <summary>最後に受理したsampleが押下中か。</summary>
        public bool IsPressed => _isPressed;

        private InputRepeatTracker(ulong initialDelayTicks, ulong repeatIntervalTicks, ulong initialTick)
        {
            _initialDelayTicks = initialDelayTicks;
            _repeatIntervalTicks = repeatIntervalTicks;
            _currentTick = initialTick;
        }

        /// <summary>delay、interval、初期tickを検証してtrackerを作る。</summary>
        /// <param name="initialDelayTicks">押下edgeから最初のrepeatまでの正のtick差。</param>
        /// <param name="repeatIntervalTicks">repeat間の正のtick差。</param>
        /// <param name="initialTick">最初のsimulation tick。</param>
        /// <param name="tracker">成功時に作られたtracker。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>作成できた場合true。</returns>
        public static bool TryCreate(ulong initialDelayTicks, ulong repeatIntervalTicks, ulong initialTick, out InputRepeatTracker tracker, out InputRepeatError error)
        {
            if (initialDelayTicks == 0)
            {
                tracker = null;
                error = InputRepeatError.InvalidInitialDelay;
                return false;
            }

            if (repeatIntervalTicks == 0)
            {
                tracker = null;
                error = InputRepeatError.InvalidRepeatInterval;
                return false;
            }

            tracker = new InputRepeatTracker(initialDelayTicks, repeatIntervalTicks, initialTick);
            error = InputRepeatError.None;
            return true;
        }

        /// <summary>明示tickのpressed状態を処理し、今回発行すべきtrigger数を返す。</summary>
        /// <param name="tick">現在tick以上のsimulation tick。</param>
        /// <param name="pressed">このsampleで押下中ならtrue。</param>
        /// <param name="status">成功時の処理後status。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>sampleを受理した場合true。</returns>
        public bool TryPush(ulong tick, bool pressed, out InputRepeatStatus status, out InputRepeatError error)
        {
            if (tick < _currentTick)
            {
                status = Snapshot();
                error = InputRepeatError.TickMovedBackward;
                return false;
            }

            _currentTick = tick;
            if (!pressed)
            {
                var released = _isPressed;
                _isPressed = false;
                _pressTick = 0;
                _emittedRepeatCount = 0;
                status = CreateStatus(false, 0, 0, released);
                error = InputRepeatError.None;
                return true;
            }

            if (!_isPressed)
            {
                _isPressed = true;
                _pressTick = tick;
                _emittedRepeatCount = 0;
                status = CreateStatus(true, 0, 1, false);
                error = InputRepeatError.None;
                return true;
            }

            var elapsed = tick - _pressTick;
            ulong dueRepeatCount = 0;
            if (elapsed >= _initialDelayTicks) dueRepeatCount = 1 + ((elapsed - _initialDelayTicks) / _repeatIntervalTicks);
            var newlyDueRepeatCount = dueRepeatCount - _emittedRepeatCount;
            _emittedRepeatCount = dueRepeatCount;
            status = CreateStatus(false, newlyDueRepeatCount, newlyDueRepeatCount, false);
            error = InputRepeatError.None;
            return true;
        }

        /// <summary>状態を進めずterminal flagとtriggerを持たない現在statusを返す。</summary>
        /// <returns>現在のtickとpressed状態だけを反映したstatus。</returns>
        public InputRepeatStatus Snapshot() => CreateStatus(false, 0, 0, false);

        /// <summary>押下状態とrepeat進捗を破棄し、新しいsimulation tickへ初期化する。</summary>
        /// <param name="tick">新しいtimelineの開始tick。</param>
        public void Reset(ulong tick)
        {
            _currentTick = tick;
            _isPressed = false;
            _pressTick = 0;
            _emittedRepeatCount = 0;
        }

        private InputRepeatStatus CreateStatus(bool initialTriggered, ulong repeatTriggerCount, ulong triggerCount, bool released) => new InputRepeatStatus(_currentTick, _isPressed, initialTriggered, repeatTriggerCount, triggerCount, released);
    }
}
