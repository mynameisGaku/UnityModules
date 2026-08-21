namespace GameplayEffects
{
    /// <summary>stack数と残りtick数を組にした、時限stackの不変状態です。</summary>
    public readonly struct TimedStackState
    {
        /// <summary>stack数と残りtick数を指定して状態を作成します。</summary>
        public TimedStackState(int stackCount, int remainingTicks)
        {
            StackCount = stackCount;
            RemainingTicks = remainingTicks;
        }

        /// <summary>現在のstack数を取得します。</summary>
        public int StackCount { get; }

        /// <summary>残りtick数を取得します。</summary>
        public int RemainingTicks { get; }

        /// <summary>stackも残りtickも0の非active状態かを取得します。</summary>
        public bool IsInactive => StackCount == 0 && RemainingTicks == 0;
    }
}
