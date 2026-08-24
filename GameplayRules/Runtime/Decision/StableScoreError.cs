namespace GameplayDecision
{
    /// <summary>score選択を開始できなかった理由です。</summary>
    public enum StableScoreError
    {
        /// <summary>失敗はありません。</summary>
        None = 0,

        /// <summary>候補配列がnullです。</summary>
        NullCandidates = 1,

        /// <summary>候補数が許容範囲外です。</summary>
        InvalidCandidateCount = 2,

        /// <summary>候補識別値が正ではありません。</summary>
        InvalidCandidateIdentifier = 3,

        /// <summary>候補識別値が重複しています。</summary>
        DuplicateCandidateIdentifier = 4,

        /// <summary>候補scoreが有限な0から1の範囲ではありません。</summary>
        InvalidScore = 5,

        /// <summary>現在選択中の識別値が負です。</summary>
        InvalidCurrentIdentifier = 6,

        /// <summary>切替に必要な最小優位差が有限な0から1の範囲ではありません。</summary>
        InvalidMinimumAdvantage = 7
    }
}
