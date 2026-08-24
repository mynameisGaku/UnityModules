namespace GameplayInventory
{
    /// <summary>移送先stackの正のID、現在unit数、capacityを表します。</summary>
    public readonly struct StackTransferDestination
    {
        /// <summary>移送先stackを構築します。</summary>
        /// <param name="identifier">移送先stackを区別する正の識別値です。</param>
        /// <param name="currentUnits">移送前に保持する非負unit数です。</param>
        /// <param name="capacity">移送後unit数の正の上限です。</param>
        public StackTransferDestination(int identifier, int currentUnits, int capacity)
        {
            Identifier = identifier;
            CurrentUnits = currentUnits;
            Capacity = capacity;
        }

        /// <summary>移送先stackを区別する正の識別値です。</summary>
        public int Identifier { get; }

        /// <summary>移送前に保持する非負unit数です。</summary>
        public int CurrentUnits { get; }

        /// <summary>移送後unit数の正の上限です。</summary>
        public int Capacity { get; }
    }
}
