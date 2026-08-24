namespace GameplayInventory
{
    /// <summary>1つの移送先stackに対する加算計画です。</summary>
    public readonly struct StackTransferDestinationLine
    {
        internal StackTransferDestinationLine(int index, int identifier, int beforeUnits, int capacity, int receivedUnits)
        {
            Index = index;
            Identifier = identifier;
            BeforeUnits = beforeUnits;
            Capacity = capacity;
            ReceivedUnits = receivedUnits;
        }

        /// <summary>入力配列内のindexです。</summary>
        public int Index { get; }

        /// <summary>移送先stackの識別値です。</summary>
        public int Identifier { get; }

        /// <summary>移送前unit数です。</summary>
        public int BeforeUnits { get; }

        /// <summary>移送後unit数の上限です。</summary>
        public int Capacity { get; }

        /// <summary>このstackが受け取るunit数です。</summary>
        public int ReceivedUnits { get; }

        /// <summary>計画適用後に保持するunit数です。</summary>
        public int AfterUnits => BeforeUnits + ReceivedUnits;
    }
}
