namespace InputPressing
{
    /// <summary>Input Press Classifierが要求を受理できなかった理由。</summary>
    public enum InputPressError
    {
        /// <summary>処理が成功した。</summary>
        None = 0,

        /// <summary>hold判定までのtick数が0である。</summary>
        InvalidHoldThreshold = 1,

        /// <summary>入力tickが最後に受理したtickより前へ戻った。</summary>
        TickMovedBackward = 2
    }
}
