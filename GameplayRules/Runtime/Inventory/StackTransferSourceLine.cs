namespace GameplayInventory
{
    /// <summary>1つの移送元stackに対する減算計画です。</summary>
    public readonly struct StackTransferSourceLine
    {
        internal StackTransferSourceLine(int index, int identifier, int beforeUnits, int movedUnits)
        {
            Index = index;
            Identifier = identifier;
            BeforeUnits = beforeUnits;
            MovedUnits = movedUnits;
        }

        /// <summary>入力配列内のindexです。</summary>
        public int Index { get; }

        /// <summary>移送元stackの識別値です。</summary>
        public int Identifier { get; }

        /// <summary>移送前unit数です。</summary>
        public int BeforeUnits { get; }

        /// <summary>このstackから移すunit数です。</summary>
        public int MovedUnits { get; }

        /// <summary>計画適用後に残るunit数です。</summary>
        public int AfterUnits => BeforeUnits - MovedUnits;
    }
}
