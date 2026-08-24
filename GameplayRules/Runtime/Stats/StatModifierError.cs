namespace GameplayStats
{
    /// <summary>stat構成またはmodifier変更を処理できなかった理由。</summary>
    public enum StatModifierError
    {
        /// <summary>処理が成功した。</summary>
        None = 0,

        /// <summary>base値がNaNかInfinityだった。</summary>
        NonFiniteBaseValue = 1,

        /// <summary>modifier IDが0以下だった。</summary>
        InvalidModifierId = 2,

        /// <summary>modifier kindが未定義値だった。</summary>
        InvalidModifierKind = 3,

        /// <summary>modifier値がNaNかInfinityだった。</summary>
        NonFiniteModifierValue = 4,

        /// <summary>同じmodifier IDが既に存在した。</summary>
        DuplicateModifierId = 5,

        /// <summary>指定modifier IDが存在しなかった。</summary>
        ModifierNotFound = 6,

        /// <summary>最大modifier件数へ到達していた。</summary>
        CapacityReached = 7,

        /// <summary>変更後のstage合計または最終値が有限値にならなかった。</summary>
        ResultNotFinite = 8
    }
}
