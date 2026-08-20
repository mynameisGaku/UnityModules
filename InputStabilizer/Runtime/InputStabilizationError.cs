namespace InputStabilization
{
    /// <summary>command stabilizerを構成できなかった理由。</summary>
    public enum InputStabilizationError
    {
        /// <summary>構成が成功した。</summary>
        None = 0,

        /// <summary>必要連続sample数が対応範囲外だった。</summary>
        InvalidRequiredSampleCount = 1
    }
}
