namespace GameplayResources
{
    /// <summary>Resource Cost Evaluatorが入力を受理できなかった理由を表します。</summary>
    public enum ResourceCostError
    {
        /// <summary>失敗はありません。</summary>
        None = 0,
        /// <summary>resource残量配列がnullです。</summary>
        NullBalances = 1,
        /// <summary>resource cost配列がnullです。</summary>
        NullCosts = 2,
        /// <summary>resource残量が0〜32件に収まりません。</summary>
        InvalidBalanceCount = 3,
        /// <summary>resource costが1〜32件に収まりません。</summary>
        InvalidCostCount = 4,
        /// <summary>resource IDが正の整数ではありません。</summary>
        InvalidResourceId = 5,
        /// <summary>残量またはcostがNaNかInfinityです。</summary>
        NonFiniteAmount = 6,
        /// <summary>残量またはcostが負です。</summary>
        NegativeAmount = 7,
        /// <summary>resource残量に同じIDが複数あります。</summary>
        DuplicateBalanceId = 8,
        /// <summary>resource costに同じIDが複数あります。</summary>
        DuplicateCostId = 9
    }
}
