namespace GameplayDecision
{
    /// <summary>候補scoreへ寄与する1件の正規化utilityとweightを表します。</summary>
    public readonly struct UtilityScoreFactor
    {
        /// <summary>識別値、utility、weightを保持するfactorを構築します。</summary>
        public UtilityScoreFactor(int identifier, double utility, double weight)
        {
            Identifier = identifier;
            Utility = utility;
            Weight = weight;
        }

        /// <summary>候補内でfactorを区別する正の識別値です。</summary>
        public int Identifier { get; }

        /// <summary>0から1までの正規化済みutilityです。</summary>
        public double Utility { get; }

        /// <summary>weighted meanへ使用する正のweightです。</summary>
        public double Weight { get; }
    }
}
