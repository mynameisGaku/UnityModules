namespace GameplayAllocation
{
    /// <summary>整数総量をlargest remainder方式で配分した入力順の全明細です。</summary>
    public sealed class WeightedIntegerAllocation
    {
        private readonly WeightedIntegerAllocationLine[] _lines;

        internal WeightedIntegerAllocation(int totalUnits, long totalWeight, int positiveWeightEntryCount, int remainderUnitCount, WeightedIntegerAllocationLine[] lines)
        {
            TotalUnits = totalUnits;
            TotalWeight = totalWeight;
            PositiveWeightEntryCount = positiveWeightEntryCount;
            RemainderUnitCount = remainderUnitCount;
            _lines = (WeightedIntegerAllocationLine[])lines.Clone();
        }

        /// <summary>配分を要求された整数総量です。</summary>
        public int TotalUnits { get; }

        /// <summary>全entryへ実際に配分したunit合計です。</summary>
        public int TotalAllocatedUnits => TotalUnits;

        /// <summary>入力entryのweight合計です。</summary>
        public long TotalWeight { get; }

        /// <summary>正のweightを持つentry数です。</summary>
        public int PositiveWeightEntryCount { get; }

        /// <summary>base配分後にlargest remainder順位で追加したunit数です。</summary>
        public int RemainderUnitCount { get; }

        /// <summary>配分したentry数です。</summary>
        public int EntryCount => _lines.Length;

        /// <summary>指定した入力indexの配分明細を取得します。</summary>
        /// <param name="index">取得する入力indexです。</param>
        /// <param name="line">取得できた場合の配分明細です。</param>
        /// <returns>indexが配分明細の範囲内ならtrueです。</returns>
        public bool TryGetLine(int index, out WeightedIntegerAllocationLine line)
        {
            if (index < 0 || index >= _lines.Length)
            {
                line = default;
                return false;
            }

            line = _lines[index];
            return true;
        }
    }
}
