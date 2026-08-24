namespace GameplayTiming
{
    /// <summary>整数simulation tick上の定期発火cursorを検証し、今回の発火範囲を計画します。</summary>
    public static class PeriodicTickPlanner
    {
        /// <summary>発火間隔と残り回数に受理する共通上限です。</summary>
        public const int MaximumScheduleValue = 1_000_000_000;

        /// <summary>1回の計画に含められる発火数上限です。</summary>
        public const int MaximumEmissionCount = 1_000_000;

        /// <summary>cursorと評価境界を検証し、成功時だけ不変の発火計画を返します。</summary>
        /// <param name="state">計画前の定期発火cursorです。</param>
        /// <param name="throughTick">このtick以下を到来済みとして評価する境界です。</param>
        /// <param name="maximumEmissionCount">今回の計画へ含められる最大発火数です。</param>
        /// <param name="plan">成功時の発火計画です。</param>
        /// <param name="error">失敗理由です。成功時は<see cref="PeriodicTickError.None"/>です。</param>
        /// <returns>入力が有効で計画を作成できた場合はtrueです。</returns>
        public static bool TryPlan(PeriodicTickState state, long throughTick, int maximumEmissionCount, out PeriodicTickPlan plan, out PeriodicTickError error)
        {
            plan = default;
            error = Validate(state, throughTick, maximumEmissionCount);
            if (error != PeriodicTickError.None) return false;
            plan = PeriodicTickPlannerEngine.Plan(state, throughTick, maximumEmissionCount);
            return true;
        }

        /// <summary>cursor、schedule範囲、評価tick、今回上限の順で入力を検証します。</summary>
        private static PeriodicTickError Validate(PeriodicTickState state, long throughTick, int maximumEmissionCount)
        {
            if (state.NextTick < 0L) return PeriodicTickError.InvalidNextTick;
            if (state.IntervalTicks < 1 || state.IntervalTicks > MaximumScheduleValue) return PeriodicTickError.InvalidIntervalTicks;
            if (state.RemainingCount < 0 || state.RemainingCount > MaximumScheduleValue) return PeriodicTickError.InvalidRemainingCount;
            if (state.IsCompleted)
            {
                if (state.NextTick != 0L || state.IntervalTicks != 1) return PeriodicTickError.InvalidCompletedState;
            }
            else if (WouldOverflow(state))
            {
                return PeriodicTickError.ScheduleOverflow;
            }

            if (throughTick < 0L) return PeriodicTickError.InvalidThroughTick;
            if (maximumEmissionCount < 1 || maximumEmissionCount > MaximumEmissionCount) return PeriodicTickError.InvalidMaximumEmissionCount;
            return PeriodicTickError.None;
        }

        /// <summary>最後の予定tickがlong範囲を超えるかを除算で判定します。</summary>
        private static bool WouldOverflow(PeriodicTickState state)
        {
            var intervals = (long)state.RemainingCount - 1L;
            return intervals > (long.MaxValue - state.NextTick) / state.IntervalTicks;
        }
    }
}
