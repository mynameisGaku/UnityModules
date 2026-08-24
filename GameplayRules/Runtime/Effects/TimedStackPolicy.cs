namespace GameplayEffects
{
    /// <summary>stack数と残りtick数の再適用方法および上限をまとめた不変方針です。</summary>
    public readonly struct TimedStackPolicy
    {
        /// <summary>各上限と再適用方法を指定して方針を作成します。</summary>
        public TimedStackPolicy(int maximumStackCount, int maximumDurationTicks, TimedStackCountMode stackMode, TimedStackDurationMode durationMode)
        {
            MaximumStackCount = maximumStackCount;
            MaximumDurationTicks = maximumDurationTicks;
            StackMode = stackMode;
            DurationMode = durationMode;
        }

        /// <summary>結果に許可する最大stack数を取得します。</summary>
        public int MaximumStackCount { get; }

        /// <summary>結果に許可する最大残りtick数を取得します。</summary>
        public int MaximumDurationTicks { get; }

        /// <summary>stack数の再適用方法を取得します。</summary>
        public TimedStackCountMode StackMode { get; }

        /// <summary>残りtick数の再適用方法を取得します。</summary>
        public TimedStackDurationMode DurationMode { get; }
    }
}
