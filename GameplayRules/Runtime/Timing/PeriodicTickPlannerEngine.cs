using System;

namespace GameplayTiming
{
    /// <summary>検証済みcursorから到来済み発火範囲を決定論的に計算します。</summary>
    internal static class PeriodicTickPlannerEngine
    {
        /// <summary>指定tick以下の到来数を数え、今回の上限内で次cursorへ進めます。</summary>
        internal static PeriodicTickPlan Plan(PeriodicTickState state, long throughTick, int maximumEmissionCount)
        {
            if (state.IsCompleted) return new PeriodicTickPlan(state, state, throughTick, maximumEmissionCount, 0, 0, -1L, -1L);
            var dueCount = 0;
            if (throughTick >= state.NextTick)
            {
                var arrivedIntervals = (throughTick - state.NextTick) / state.IntervalTicks;
                dueCount = arrivedIntervals >= (long)state.RemainingCount - 1L ? state.RemainingCount : (int)arrivedIntervals + 1;
            }

            var emittedCount = Math.Min(dueCount, maximumEmissionCount);
            if (emittedCount == 0) return new PeriodicTickPlan(state, state, throughTick, maximumEmissionCount, dueCount, 0, -1L, -1L);
            var firstTick = state.NextTick;
            var lastTick = firstTick + ((long)emittedCount - 1L) * state.IntervalTicks;
            var remaining = state.RemainingCount - emittedCount;
            var nextState = remaining == 0 ? PeriodicTickState.Completed : new PeriodicTickState(lastTick + state.IntervalTicks, state.IntervalTicks, remaining);
            return new PeriodicTickPlan(state, nextState, throughTick, maximumEmissionCount, dueCount, emittedCount, firstTick, lastTick);
        }
    }
}
