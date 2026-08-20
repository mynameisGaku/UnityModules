namespace InputMultiTapping
{
    /// <summary>InputMultiTapClassifierが要求を受理できない理由。</summary>
    public enum InputMultiTapError
    {
        /// <summary>errorなし。</summary>
        None = 0,

        /// <summary>最大gapが0である。</summary>
        InvalidMaximumGapTicks = 1,

        /// <summary>最大tap数が対応範囲外である。</summary>
        InvalidMaximumTapCount = 2,

        /// <summary>simulation tickが前回より小さい。</summary>
        TickMovedBackward = 3
    }
}
