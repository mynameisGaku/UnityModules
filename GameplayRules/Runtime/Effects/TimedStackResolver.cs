namespace GameplayEffects
{
    /// <summary>時限stackの現在値と追加値を方針に従って検証・解決します。</summary>
    public static class TimedStackResolver
    {
        /// <summary>stack数と残りtick数に受理する共通上限です。</summary>
        public const int MaximumValue = 1_000_000_000;

        /// <summary>現在状態と追加状態を検証し、成功時だけ不変の解決結果を返します。</summary>
        public static bool TryResolve(TimedStackState current, TimedStackState incoming, TimedStackPolicy policy, out TimedStackResolution resolution, out TimedStackError error)
        {
            resolution = default;
            error = Validate(current, incoming, policy);
            if (error != TimedStackError.None) return false;
            resolution = TimedStackResolverEngine.Resolve(current, incoming, policy);
            return true;
        }

        /// <summary>公開契約順で上限、方法、現在状態、追加状態を検証します。</summary>
        private static TimedStackError Validate(TimedStackState current, TimedStackState incoming, TimedStackPolicy policy)
        {
            if (policy.MaximumStackCount < 1 || policy.MaximumStackCount > MaximumValue) return TimedStackError.InvalidMaximumStackCount;
            if (policy.MaximumDurationTicks < 1 || policy.MaximumDurationTicks > MaximumValue) return TimedStackError.InvalidMaximumDurationTicks;
            if (policy.StackMode < TimedStackCountMode.AddClamped || policy.StackMode > TimedStackCountMode.MaximumClamped) return TimedStackError.InvalidStackMode;
            if (policy.DurationMode < TimedStackDurationMode.RefreshClamped || policy.DurationMode > TimedStackDurationMode.MaximumClamped) return TimedStackError.InvalidDurationMode;
            if (!IsValidCurrent(current, policy)) return TimedStackError.InvalidCurrentState;
            if (!IsValidIncoming(incoming)) return TimedStackError.InvalidIncomingState;
            return TimedStackError.None;
        }

        /// <summary>現在状態が0/0または方針内のactive状態かを判定します。</summary>
        private static bool IsValidCurrent(TimedStackState state, TimedStackPolicy policy)
        {
            if (state.IsInactive) return true;
            return state.StackCount >= 1 && state.StackCount <= policy.MaximumStackCount && state.RemainingTicks >= 1 && state.RemainingTicks <= policy.MaximumDurationTicks;
        }

        /// <summary>追加状態が共通上限内の正の状態かを判定します。</summary>
        private static bool IsValidIncoming(TimedStackState state) => state.StackCount >= 1 && state.StackCount <= MaximumValue && state.RemainingTicks >= 1 && state.RemainingTicks <= MaximumValue;
    }
}
