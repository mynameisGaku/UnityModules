namespace GameplayTiming
{
    /// <summary>次の発火tick、間隔、残り発火回数を保持する不変cursorです。</summary>
    public readonly struct PeriodicTickState
    {
        /// <summary>次の発火tick、間隔、残り回数を指定してcursorを作成します。</summary>
        /// <param name="nextTick">次に発火する0以上のsimulation tickです。</param>
        /// <param name="intervalTicks">発火間隔を表す1以上の整数tickです。</param>
        /// <param name="remainingCount">未発火の残り回数です。</param>
        public PeriodicTickState(long nextTick, int intervalTicks, int remainingCount)
        {
            NextTick = nextTick;
            IntervalTicks = intervalTicks;
            RemainingCount = remainingCount;
        }

        /// <summary>完了済みのcanonical cursorを取得します。</summary>
        public static PeriodicTickState Completed => new PeriodicTickState(0L, 1, 0);

        /// <summary>次に発火するsimulation tickを取得します。</summary>
        public long NextTick { get; }

        /// <summary>発火間隔を整数tickで取得します。</summary>
        public int IntervalTicks { get; }

        /// <summary>未発火の残り回数を取得します。</summary>
        public int RemainingCount { get; }

        /// <summary>残り回数が0の完了済みcursorかを取得します。</summary>
        public bool IsCompleted => RemainingCount == 0;
    }
}
