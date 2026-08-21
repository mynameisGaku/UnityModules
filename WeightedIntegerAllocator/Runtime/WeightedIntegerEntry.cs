namespace GameplayAllocation
{
    /// <summary>整数配分を受ける正のIDと非負weightを表します。</summary>
    public readonly struct WeightedIntegerEntry
    {
        /// <summary>識別値とweightを保持するentryを構築します。</summary>
        /// <param name="identifier">entryを区別する正の識別値です。</param>
        /// <param name="weight">配分比率に使う非負整数weightです。</param>
        public WeightedIntegerEntry(int identifier, int weight)
        {
            Identifier = identifier;
            Weight = weight;
        }

        /// <summary>entryを区別する正の識別値です。</summary>
        public int Identifier { get; }

        /// <summary>配分比率に使う非負整数weightです。</summary>
        public int Weight { get; }
    }
}
