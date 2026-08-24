using System;

namespace GameplayEffects
{
    /// <summary>検証済みの時限stack状態を決定論的に組み合わせます。</summary>
    internal static class TimedStackResolverEngine
    {
        /// <summary>検証済み入力を解決し、上限適用情報を含む結果を返します。</summary>
        internal static TimedStackResolution Resolve(TimedStackState current, TimedStackState incoming, TimedStackPolicy policy)
        {
            var currentStacks = current.StackCount;
            var currentTicks = current.RemainingTicks;
            var rawStacks = ResolveStackCount(currentStacks, incoming.StackCount, policy.StackMode);
            var rawTicks = ResolveDuration(currentTicks, incoming.RemainingTicks, policy.DurationMode);
            var resultStacks = (int)Math.Min(rawStacks, policy.MaximumStackCount);
            var resultTicks = (int)Math.Min(rawTicks, policy.MaximumDurationTicks);
            return new TimedStackResolution(current, incoming, new TimedStackState(resultStacks, resultTicks), policy, rawStacks > policy.MaximumStackCount, rawTicks > policy.MaximumDurationTicks);
        }

        /// <summary>指定された方法でstack数を組み合わせます。</summary>
        private static long ResolveStackCount(int current, int incoming, TimedStackCountMode mode)
        {
            if (mode == TimedStackCountMode.AddClamped) return (long)current + incoming;
            if (mode == TimedStackCountMode.ReplaceClamped) return incoming;
            return Math.Max(current, incoming);
        }

        /// <summary>指定された方法で残りtick数を組み合わせます。</summary>
        private static long ResolveDuration(int current, int incoming, TimedStackDurationMode mode)
        {
            if (mode == TimedStackDurationMode.RefreshClamped) return incoming;
            if (mode == TimedStackDurationMode.AddClamped) return (long)current + incoming;
            return Math.Max(current, incoming);
        }
    }
}
