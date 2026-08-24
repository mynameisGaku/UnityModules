namespace GameplaySelection
{
    /// <summary>Weighted Choice Tableの操作を拒否した理由。</summary>
    public enum WeightedChoiceError
    {
        /// <summary>操作が成功した。</summary>
        None = 0,

        /// <summary>IDが正の整数ではない。</summary>
        InvalidIdentifier = 1,

        /// <summary>weightが有限の正数ではない。</summary>
        InvalidWeight = 2,

        /// <summary>sampleが有限な0以上1未満ではない。</summary>
        InvalidSample = 3,

        /// <summary>同じIDのentryが既に存在する。</summary>
        DuplicateIdentifier = 4,

        /// <summary>指定IDのentryが存在しない。</summary>
        EntryNotFound = 5,

        /// <summary>entry数が上限へ達している。</summary>
        CapacityReached = 6,

        /// <summary>選択できるentryが存在しない。</summary>
        EmptyTable = 7,

        /// <summary>weight合計が有限範囲を超える。</summary>
        NumericOverflow = 8,

        /// <summary>指定indexが現在のentry範囲外である。</summary>
        IndexOutOfRange = 9
    }
}
