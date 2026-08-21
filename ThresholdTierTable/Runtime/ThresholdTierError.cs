namespace GameplayProgression
{
    /// <summary>Threshold Tier Tableが要求を受理できなかった理由を表します。</summary>
    public enum ThresholdTierError
    {
        /// <summary>失敗はありません。</summary>
        None = 0,
        /// <summary>容量が1以上32以下ではありません。</summary>
        InvalidCapacity = 1,
        /// <summary>tier IDが正の値ではありません。</summary>
        InvalidTierId = 2,
        /// <summary>thresholdがNaNまたはInfinityです。</summary>
        InvalidMinimumValue = 3,
        /// <summary>同じtier IDが既に登録されています。</summary>
        DuplicateTierId = 4,
        /// <summary>同じthresholdが既に登録されています。</summary>
        DuplicateMinimumValue = 5,
        /// <summary>登録数が容量に達しています。</summary>
        CapacityExceeded = 6,
        /// <summary>指定したtier IDが登録されていません。</summary>
        TierNotFound = 7,
        /// <summary>indexが登録範囲外です。</summary>
        IndexOutOfRange = 8,
        /// <summary>評価値がNaNまたはInfinityです。</summary>
        InvalidQueryValue = 9,
        /// <summary>評価できるtierが1件もありません。</summary>
        TableEmpty = 10
    }
}
