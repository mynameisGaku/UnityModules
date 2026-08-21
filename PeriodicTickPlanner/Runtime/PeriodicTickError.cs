namespace GameplayTiming
{
    /// <summary>定期tick計画要求を受理できなかった理由です。</summary>
    public enum PeriodicTickError
    {
        /// <summary>失敗していません。</summary>
        None = 0,

        /// <summary>次の発火tickが負です。</summary>
        InvalidNextTick = 1,

        /// <summary>発火間隔が許容範囲外です。</summary>
        InvalidIntervalTicks = 2,

        /// <summary>残り発火回数が許容範囲外です。</summary>
        InvalidRemainingCount = 3,

        /// <summary>完了済みcursorがcanonical表現ではありません。</summary>
        InvalidCompletedState = 4,

        /// <summary>最後の予定tickが64bit整数範囲を超えます。</summary>
        ScheduleOverflow = 5,

        /// <summary>計画対象tickが負です。</summary>
        InvalidThroughTick = 6,

        /// <summary>今回許可する最大発火数が許容範囲外です。</summary>
        InvalidMaximumEmissionCount = 7
    }
}
