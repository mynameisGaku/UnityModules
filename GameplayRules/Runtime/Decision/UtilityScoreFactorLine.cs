namespace GameplayDecision
{
    /// <summary>1factorが候補scoreへ与えたweighted utilityを表します。</summary>
    public readonly struct UtilityScoreFactorLine
    {
        internal UtilityScoreFactorLine(int factorIdentifier, double utility, double weight, double weightedUtility)
        {
            FactorIdentifier = factorIdentifier;
            Utility = utility;
            Weight = weight;
            WeightedUtility = weightedUtility;
        }

        /// <summary>factor識別値です。</summary>
        public int FactorIdentifier { get; }

        /// <summary>入力された0から1のutilityです。</summary>
        public double Utility { get; }

        /// <summary>入力された正のweightです。</summary>
        public double Weight { get; }

        /// <summary>utilityとweightを掛けた寄与値です。</summary>
        public double WeightedUtility { get; }
    }
}
