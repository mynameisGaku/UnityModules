namespace InputSmoothing
{
    /// <summary>2D vector slew制限を完了できなかった理由。</summary>
    public enum InputVectorSlewLimiterError
    {
        /// <summary>処理が成功した。</summary>
        None = 0,

        /// <summary>最大変化量が非有限または0以下だった。</summary>
        InvalidConfiguration = 1,

        /// <summary>horizontalまたはverticalへNaNかInfinityが入力された。</summary>
        NonFiniteInput = 2,

        /// <summary>horizontalまたはverticalが-1以上1以下の範囲外だった。</summary>
        InputOutOfRange = 3
    }
}
