namespace GameplayStats
{
    /// <summary>modifier値をbase値へ適用するstage。</summary>
    public enum StatModifierKind
    {
        /// <summary>base値へ直接加算する値。</summary>
        Flat = 0,

        /// <summary>全Flat適用後の値へ、合計した比率を加算する値。0.2は20 percentを表す。</summary>
        AdditivePercent = 1,

        /// <summary>FlatとAdditivePercent適用後の値へ順に乗算するfactor。</summary>
        MultiplicativeFactor = 2
    }
}
