namespace SimulationClock
{
    /// <summary>固定step時計を作成、復元、または進行できなかった理由。</summary>
    public enum FixedStepClockError
    {
        /// <summary>失敗していない。</summary>
        None = 0,
        /// <summary>step時間または1回の最大step数が範囲外。</summary>
        InvalidSettings = 1,
        /// <summary>完了件数、端数、または累積破棄時間が範囲外。</summary>
        InvalidState = 2,
        /// <summary>渡した経過時間が負数。</summary>
        InvalidElapsedTime = 3,
        /// <summary>完了件数または破棄時間をlongで表現できない。</summary>
        Overflow = 4
    }
}
