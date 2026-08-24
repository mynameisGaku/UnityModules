namespace GameplayInventory
{
    /// <summary>移送元stackの正のIDと移送可能unit数を表します。</summary>
    public readonly struct StackTransferSource
    {
        /// <summary>移送元stackを構築します。</summary>
        /// <param name="identifier">移送元stackを区別する正の識別値です。</param>
        /// <param name="availableUnits">移送前に保持する非負unit数です。</param>
        public StackTransferSource(int identifier, int availableUnits)
        {
            Identifier = identifier;
            AvailableUnits = availableUnits;
        }

        /// <summary>移送元stackを区別する正の識別値です。</summary>
        public int Identifier { get; }

        /// <summary>移送前に保持する非負unit数です。</summary>
        public int AvailableUnits { get; }
    }
}
