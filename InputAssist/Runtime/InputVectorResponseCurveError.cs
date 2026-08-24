namespace InputResponse
{
    /// <summary>2D vector response curve処理を完了できなかった理由。</summary>
    public enum InputVectorResponseCurveError
    {
        /// <summary>処理が成功した。</summary>
        None = 0,

        /// <summary>default値または未定義modeの設定が使われた。</summary>
        InvalidConfiguration = 1,

        /// <summary>horizontalまたはverticalへNaNかInfinityが入力された。</summary>
        NonFiniteInput = 2,

        /// <summary>入力vectorのmagnitudeが1を超えた。</summary>
        InputOutOfRange = 3
    }
}
