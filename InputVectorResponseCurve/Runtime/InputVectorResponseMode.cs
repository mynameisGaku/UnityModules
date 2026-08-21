namespace InputResponse
{
    /// <summary>入力magnitudeへ適用する決定論的なresponse curve。</summary>
    public enum InputVectorResponseMode
    {
        /// <summary>入力magnitudeを変更しない。</summary>
        Linear = 1,

        /// <summary>入力magnitudeを2乗する。</summary>
        Squared = 2,

        /// <summary>入力magnitudeを3乗する。</summary>
        Cubic = 3,

        /// <summary>0と1で傾きが0になるsmooth stepを適用する。</summary>
        SmoothStep = 4
    }
}
