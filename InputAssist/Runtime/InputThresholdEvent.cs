namespace InputThresholding
{
    /// <summary>analog sampleによりpressed状態が変化したedge。</summary>
    public enum InputThresholdEvent
    {
        /// <summary>状態変化が無い。</summary>
        None = 0,

        /// <summary>release状態からpressed状態へ変化した。</summary>
        Pressed = 1,

        /// <summary>pressed状態からrelease状態へ変化した。</summary>
        Released = 2
    }
}
