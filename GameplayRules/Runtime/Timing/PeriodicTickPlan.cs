namespace GameplayTiming
{
    /// <summary>指定tickまでに到来した定期発火範囲と次cursorを保持する不変計画です。</summary>
    public readonly struct PeriodicTickPlan
    {
        /// <summary>計画前後のcursor、到来数、実発火範囲をまとめます。</summary>
        /// <param name="previousState">計画前のcursorです。</param>
        /// <param name="nextState">今回の発火を消費した後のcursorです。</param>
        /// <param name="throughTick">到来済みとして評価したtick境界です。</param>
        /// <param name="maximumEmissionCount">今回許可した最大発火数です。</param>
        /// <param name="dueCount">境界までに到来していた総発火数です。</param>
        /// <param name="emittedCount">今回の計画へ含めた発火数です。</param>
        /// <param name="firstEmittedTick">最初に発火するtickです。発火0件では-1です。</param>
        /// <param name="lastEmittedTick">最後に発火するtickです。発火0件では-1です。</param>
        public PeriodicTickPlan(PeriodicTickState previousState, PeriodicTickState nextState, long throughTick, int maximumEmissionCount, int dueCount, int emittedCount, long firstEmittedTick, long lastEmittedTick)
        {
            PreviousState = previousState;
            NextState = nextState;
            ThroughTick = throughTick;
            MaximumEmissionCount = maximumEmissionCount;
            DueCount = dueCount;
            EmittedCount = emittedCount;
            FirstEmittedTick = firstEmittedTick;
            LastEmittedTick = lastEmittedTick;
        }

        /// <summary>計画前のcursorを取得します。</summary>
        public PeriodicTickState PreviousState { get; }

        /// <summary>今回の発火を消費した後のcursorを取得します。</summary>
        public PeriodicTickState NextState { get; }

        /// <summary>このtick以下を到来済みとして評価した境界を取得します。</summary>
        public long ThroughTick { get; }

        /// <summary>今回許可した最大発火数を取得します。</summary>
        public int MaximumEmissionCount { get; }

        /// <summary>境界までに到来していた総発火数を取得します。</summary>
        public int DueCount { get; }

        /// <summary>今回の計画へ含めた発火数を取得します。</summary>
        public int EmittedCount { get; }

        /// <summary>最初に発火するtickを取得します。発火0件では-1です。</summary>
        public long FirstEmittedTick { get; }

        /// <summary>最後に発火するtickを取得します。発火0件では-1です。</summary>
        public long LastEmittedTick { get; }

        /// <summary>1件以上の発火を含むかを取得します。</summary>
        public bool HasEmissions => EmittedCount > 0;

        /// <summary>到来数が今回の上限で分割されたかを取得します。</summary>
        public bool WasLimited => DueCount > EmittedCount;

        /// <summary>今回の発火後に全予定を完了したかを取得します。</summary>
        public bool IsCompleted => NextState.IsCompleted;
    }
}
