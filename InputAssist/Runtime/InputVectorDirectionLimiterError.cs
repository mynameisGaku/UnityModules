namespace InputSmoothing
{
    /// <summary>2D vector方向変化を制限できなかった理由。</summary>
    public enum InputVectorDirectionLimiterError
    {
        /// <summary>処理が成功した。</summary>
        None = 0,

        /// <summary>1 stepの最大回転量が非有限、0未満、またはPIより大きかった。</summary>
        InvalidConfiguration = 1,

        /// <summary>horizontalまたはverticalへNaNかInfinityが入力された。</summary>
        NonFiniteInput = 2,

        /// <summary>horizontalまたはverticalが-1以上1以下の範囲外だった。</summary>
        InputOutOfRange = 3,

        /// <summary>2D vectorのmagnitudeが1より大きかった。</summary>
        InputOutsideUnitCircle = 4
    }
}
