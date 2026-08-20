using System;

namespace SimulationClock
{
    /// <summary>
    /// 明示された整数経過時間を固定step数と端数へ変換し、保存・復元可能な状態として保持する。
    /// ゲーム処理やUnity時刻は取得せず、利用側が結果の連続stepを実行する。
    /// </summary>
    public sealed class FixedStepClock
    {
        /// <summary>1回のAdvanceで返せるstep数上限。</summary>
        public const int MaximumSupportedStepsPerAdvance = 4096;

        private readonly FixedStepClockSettings _settings;
        private FixedStepClockState _state;

        private FixedStepClock(FixedStepClockSettings settings, FixedStepClockState state)
        {
            _settings = settings;
            _state = state;
        }

        /// <summary>作成時に固定されたstep時間とcatch-up上限。</summary>
        public FixedStepClockSettings Settings => _settings;

        /// <summary>保存・復元または比較に使える現在状態。</summary>
        public FixedStepClockState State => _state;

        /// <summary>初期状態から時計を作る。設定が不正ならclockを返さない。</summary>
        /// <param name="settings">step時間と1回の最大step数。</param>
        /// <param name="clock">成功時に作成した時計。</param>
        /// <param name="error">作成できなかった理由。</param>
        /// <returns>作成できた場合にtrue。</returns>
        public static bool TryCreate(FixedStepClockSettings settings, out FixedStepClock clock, out FixedStepClockError error) => TryCreate(settings, default, out clock, out error);

        /// <summary>指定状態から時計を再構築する。設定または状態が不正ならclockを返さない。</summary>
        /// <param name="settings">step時間と1回の最大step数。</param>
        /// <param name="state">復元する完了件数、端数、累積破棄時間。</param>
        /// <param name="clock">成功時に作成した時計。</param>
        /// <param name="error">作成できなかった理由。</param>
        /// <returns>作成できた場合にtrue。</returns>
        public static bool TryCreate(FixedStepClockSettings settings, FixedStepClockState state, out FixedStepClock clock, out FixedStepClockError error)
        {
            error = ValidateSettings(settings);
            if (error == FixedStepClockError.None) error = ValidateState(settings, state);
            if (error != FixedStepClockError.None)
            {
                clock = null;
                return false;
            }

            clock = new FixedStepClock(settings, state);
            return true;
        }

        /// <summary>明示されたTimeSpanだけ時計を進め、今回実行する連続step範囲を返す。</summary>
        /// <param name="elapsed">0以上の経過時間。</param>
        /// <returns>step範囲、端数、補間率、破棄量。失敗時は状態を変更しない。</returns>
        public FixedStepAdvanceResult Advance(TimeSpan elapsed) => AdvanceTicks(elapsed.Ticks);

        /// <summary>明示された100ns単位の整数時間だけ時計を進め、今回実行する連続step範囲を返す。</summary>
        /// <param name="elapsedTicks">0以上のTimeSpan tick数。</param>
        /// <returns>step範囲、端数、補間率、破棄量。失敗時は状態を変更しない。</returns>
        public FixedStepAdvanceResult AdvanceTicks(long elapsedTicks)
        {
            if (elapsedTicks < 0) return Failure(FixedStepClockError.InvalidElapsedTime);

            var step = (ulong)_settings.StepDurationTicks;
            var total = (ulong)elapsedTicks + (ulong)_state.RemainderTicks;
            var availableSteps = total / step;
            var remainder = (long)(total % step);
            var executedSteps = Math.Min(availableSteps, (ulong)_settings.MaximumStepsPerAdvance);
            var droppedSteps = availableSteps - executedSteps;
            if (executedSteps > int.MaxValue || executedSteps > (ulong)(long.MaxValue - _state.CompletedStepCount)) return Failure(FixedStepClockError.Overflow);
            if (droppedSteps > long.MaxValue || droppedSteps > (ulong)long.MaxValue / step) return Failure(FixedStepClockError.Overflow);

            var droppedTicks = droppedSteps * step;
            if (droppedTicks > (ulong)(long.MaxValue - _state.TotalDroppedTicks)) return Failure(FixedStepClockError.Overflow);

            var firstStepIndex = _state.CompletedStepCount;
            var nextState = new FixedStepClockState(
                _state.CompletedStepCount + (long)executedSteps,
                remainder,
                _state.TotalDroppedTicks + (long)droppedTicks);
            _state = nextState;
            return new FixedStepAdvanceResult(
                FixedStepClockError.None,
                firstStepIndex,
                (int)executedSteps,
                _settings.StepDurationTicks,
                (long)droppedSteps,
                (long)droppedTicks,
                nextState);
        }

        /// <summary>時計を指定状態へ戻す。不正状態の場合は現在状態を変更しない。</summary>
        /// <param name="state">復元する完了件数、端数、累積破棄時間。</param>
        /// <returns>復元できた場合はNone。それ以外は失敗理由。</returns>
        public FixedStepClockError Reset(FixedStepClockState state)
        {
            var error = ValidateState(_settings, state);
            if (error == FixedStepClockError.None) _state = state;
            return error;
        }

        private FixedStepAdvanceResult Failure(FixedStepClockError error) => new FixedStepAdvanceResult(error, _state.CompletedStepCount, 0, _settings.StepDurationTicks, 0, 0, _state);

        private static FixedStepClockError ValidateSettings(FixedStepClockSettings settings)
        {
            return settings.StepDurationTicks <= 0 || settings.MaximumStepsPerAdvance <= 0 || settings.MaximumStepsPerAdvance > MaximumSupportedStepsPerAdvance
                ? FixedStepClockError.InvalidSettings
                : FixedStepClockError.None;
        }

        private static FixedStepClockError ValidateState(FixedStepClockSettings settings, FixedStepClockState state)
        {
            return state.CompletedStepCount < 0 || state.RemainderTicks < 0 || state.RemainderTicks >= settings.StepDurationTicks || state.TotalDroppedTicks < 0
                ? FixedStepClockError.InvalidState
                : FixedStepClockError.None;
        }
    }
}
