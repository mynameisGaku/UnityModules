namespace GameplayDecision
{
    /// <summary>選択候補を識別する正のIDと0から1までのscoreを表します。</summary>
    public readonly struct StableScoreCandidate
    {
        /// <summary>識別値とscoreを保持する候補を構築します。</summary>
        public StableScoreCandidate(int identifier, double score)
        {
            Identifier = identifier;
            Score = score;
        }

        /// <summary>候補を区別する正の識別値です。</summary>
        public int Identifier { get; }

        /// <summary>0から1までの正規化済みscoreです。</summary>
        public double Score { get; }
    }
}
