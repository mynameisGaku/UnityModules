namespace GameplayAllocation
{
    /// <summary>整数配分を開始できなかった理由です。</summary>
    public enum WeightedIntegerError
    {
        /// <summary>失敗はありません。</summary>
        None = 0,

        /// <summary>entry配列がnullです。</summary>
        NullEntries = 1,

        /// <summary>entry数が許容範囲外です。</summary>
        InvalidEntryCount = 2,

        /// <summary>配分する整数総量が許容範囲外です。</summary>
        InvalidTotalUnits = 3,

        /// <summary>entry識別値が正ではありません。</summary>
        InvalidEntryIdentifier = 4,

        /// <summary>entry識別値が重複しています。</summary>
        DuplicateEntryIdentifier = 5,

        /// <summary>entry weightが許容範囲外です。</summary>
        InvalidWeight = 6,

        /// <summary>正の整数総量に対してweight合計が0です。</summary>
        ZeroTotalWeight = 7
    }
}
