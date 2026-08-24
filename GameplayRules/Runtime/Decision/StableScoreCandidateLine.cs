namespace GameplayDecision
{
    /// <summary>入力候補がcurrent・best・selectedのどれに該当したかを再構築できる明細です。</summary>
    public readonly struct StableScoreCandidateLine
    {
        internal StableScoreCandidateLine(int candidateIdentifier, int inputIndex, double score, bool isCurrent, bool isBestCandidate, bool isSelected)
        {
            CandidateIdentifier = candidateIdentifier;
            InputIndex = inputIndex;
            Score = score;
            IsCurrent = isCurrent;
            IsBestCandidate = isBestCandidate;
            IsSelected = isSelected;
        }

        /// <summary>候補識別値です。</summary>
        public int CandidateIdentifier { get; }

        /// <summary>候補の入力indexです。</summary>
        public int InputIndex { get; }

        /// <summary>候補の正規化済みscoreです。</summary>
        public double Score { get; }

        /// <summary>要求された現在候補と一致するかを示します。</summary>
        public bool IsCurrent { get; }

        /// <summary>入力全体で安定tie-break後の最高score候補かを示します。</summary>
        public bool IsBestCandidate { get; }

        /// <summary>最終的に選択された候補かを示します。</summary>
        public bool IsSelected { get; }
    }
}
