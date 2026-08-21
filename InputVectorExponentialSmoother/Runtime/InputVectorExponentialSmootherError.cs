namespace InputFiltering
{
    /// <summary>2D vector exponential smoothingを完了できなかった理由。</summary>
    public enum InputVectorExponentialSmootherError
    {
        /// <summary>処理が成功した。</summary>
        None = 0,

        /// <summary>smoothing factorが非有限、0以下、または1より大きかった。</summary>
        InvalidConfiguration = 1,

        /// <summary>horizontalまたはverticalへNaNかInfinityが入力された。</summary>
        NonFiniteInput = 2,

        /// <summary>horizontalまたはverticalが-1以上1以下の範囲外だった。</summary>
        InputOutOfRange = 3
    }
}
