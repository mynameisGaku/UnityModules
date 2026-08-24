namespace GameplayAllocation
{
    /// <summary>整数総量を非負weight比で分け、largest remainderにより合計を保つ純粋関数を提供します。</summary>
    public static class WeightedIntegerAllocator
    {
        /// <summary>1回に配分できる最大entry数です。</summary>
        public const int MaximumEntryCount = 32;

        /// <summary>配分できる最大整数総量です。</summary>
        public const int MaximumTotalUnits = 1_000_000_000;

        /// <summary>1 entryに指定できる最大weightです。</summary>
        public const int MaximumWeight = 1_000_000_000;

        /// <summary>entry列と整数総量を検証し、入力順の全配分明細を構築します。</summary>
        /// <param name="entries">正の固有IDと非負weightを持つ入力entry列です。</param>
        /// <param name="totalUnits">全entryへ配分する非負整数総量です。</param>
        /// <param name="allocation">成功時に構築される入力順の配分結果です。</param>
        /// <param name="error">失敗理由、または成功時のNoneです。</param>
        /// <returns>全入力が有効で配分結果を構築できた場合はtrueです。</returns>
        public static bool TryAllocate(WeightedIntegerEntry[] entries, int totalUnits, out WeightedIntegerAllocation allocation, out WeightedIntegerError error)
        {
            allocation = null;
            if (entries == null)
            {
                error = WeightedIntegerError.NullEntries;
                return false;
            }

            if (entries.Length < 1 || entries.Length > MaximumEntryCount)
            {
                error = WeightedIntegerError.InvalidEntryCount;
                return false;
            }

            if (totalUnits < 0 || totalUnits > MaximumTotalUnits)
            {
                error = WeightedIntegerError.InvalidTotalUnits;
                return false;
            }

            var totalWeight = 0L;
            for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
            {
                var entry = entries[entryIndex];
                if (entry.Identifier <= 0)
                {
                    error = WeightedIntegerError.InvalidEntryIdentifier;
                    return false;
                }

                for (var previous = 0; previous < entryIndex; previous++)
                {
                    if (entries[previous].Identifier != entry.Identifier) continue;
                    error = WeightedIntegerError.DuplicateEntryIdentifier;
                    return false;
                }

                if (entry.Weight < 0 || entry.Weight > MaximumWeight)
                {
                    error = WeightedIntegerError.InvalidWeight;
                    return false;
                }

                totalWeight += entry.Weight;
            }

            if (totalUnits > 0 && totalWeight == 0)
            {
                error = WeightedIntegerError.ZeroTotalWeight;
                return false;
            }

            allocation = WeightedIntegerAllocationEngine.Allocate(entries, totalUnits, totalWeight);
            error = WeightedIntegerError.None;
            return true;
        }
    }
}
