namespace InputThresholding
{
    /// <summary>threshold sampleを分類できなかった理由。</summary>
    public enum InputThresholdClassificationError
    {
        /// <summary>分類が成功した。</summary>
        None = 0,

        /// <summary>default値またはthreshold範囲が不正なclassifierが使われた。</summary>
        InvalidConfiguration = 1,

        /// <summary>sampleへNaNかInfinityが入力された。</summary>
        NonFiniteInput = 2
    }
}
