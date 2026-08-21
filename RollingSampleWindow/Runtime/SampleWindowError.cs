namespace GameplayMetrics
{
    /// <summary>Rolling Sample Windowの操作結果を分類する。</summary>
    public enum SampleWindowError
    {
        /// <summary>操作が成功した。</summary>
        None = 0,

        /// <summary>容量が1以上32以下ではない。</summary>
        InvalidCapacity = 1,

        /// <summary>sampleがNaNまたは無限大である。</summary>
        InvalidSample = 2,

        /// <summary>oldest-first indexが現在のsample範囲外である。</summary>
        IndexOutOfRange = 3
    }
}
