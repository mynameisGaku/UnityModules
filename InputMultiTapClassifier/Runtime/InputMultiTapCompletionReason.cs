namespace InputMultiTapping
{
    /// <summary>tap burstを確定した理由。</summary>
    public enum InputMultiTapCompletionReason
    {
        /// <summary>今回のsampleではburstを確定していない。</summary>
        None = 0,

        /// <summary>最後のtapから許容gapを越えた。</summary>
        GapExpired = 1,

        /// <summary>設定した最大tap数へ到達した。</summary>
        MaximumReached = 2
    }
}
