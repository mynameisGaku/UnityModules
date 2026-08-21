namespace GameplayAllocation
{
    /// <summary>1 entryのbase配分・整数剰余・追加unit・最終配分を再構築できる明細です。</summary>
    public readonly struct WeightedIntegerAllocationLine
    {
        internal WeightedIntegerAllocationLine(int entryIdentifier, int inputIndex, int weight, int baseUnits, long remainderNumerator, bool receivedRemainderUnit)
        {
            EntryIdentifier = entryIdentifier;
            InputIndex = inputIndex;
            Weight = weight;
            BaseUnits = baseUnits;
            RemainderNumerator = remainderNumerator;
            ReceivedRemainderUnit = receivedRemainderUnit;
        }

        /// <summary>entry識別値です。</summary>
        public int EntryIdentifier { get; }

        /// <summary>entryの入力indexです。</summary>
        public int InputIndex { get; }

        /// <summary>配分比率に使用した非負weightです。</summary>
        public int Weight { get; }

        /// <summary>切り捨て除算で得たbase unit数です。</summary>
        public int BaseUnits { get; }

        /// <summary>largest remainder順位に使用した整数剰余の分子です。</summary>
        public long RemainderNumerator { get; }

        /// <summary>剰余順位により追加の1 unitを受け取ったかを示します。</summary>
        public bool ReceivedRemainderUnit { get; }

        /// <summary>base unitと追加unitを合計した最終配分です。</summary>
        public int AllocatedUnits => BaseUnits + (ReceivedRemainderUnit ? 1 : 0);
    }
}
