namespace InputDirectionQuantization
{
    /// <summary>2D方向量子化を完了できなかった理由。</summary>
    public enum InputDirectionQuantizationError
    {
        /// <summary>量子化が成功した。</summary>
        None = 0,

        /// <summary>default値または範囲外設定のquantizerが使われた。</summary>
        InvalidConfiguration = 1,

        /// <summary>horizontalまたはverticalへNaNかInfinityが入力された。</summary>
        NonFiniteInput = 2
    }
}
