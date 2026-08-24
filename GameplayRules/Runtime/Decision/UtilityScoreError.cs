namespace GameplayDecision
{
    /// <summary>Utility Score Evaluatorが入力を拒否した理由を表します。</summary>
    public enum UtilityScoreError
    {
        /// <summary>失敗していません。</summary>
        None = 0,

        /// <summary>候補配列がnullです。</summary>
        NullCandidates = 1,

        /// <summary>候補数が1から32の範囲外です。</summary>
        InvalidCandidateCount = 2,

        /// <summary>候補識別値が正ではありません。</summary>
        InvalidCandidateIdentifier = 3,

        /// <summary>候補識別値が重複しています。</summary>
        DuplicateCandidateIdentifier = 4,

        /// <summary>候補のfactor数が1から16の範囲外です。</summary>
        InvalidFactorCount = 5,

        /// <summary>factor識別値が正ではありません。</summary>
        InvalidFactorIdentifier = 6,

        /// <summary>同じ候補内でfactor識別値が重複しています。</summary>
        DuplicateFactorIdentifier = 7,

        /// <summary>utilityが有限な0から1の範囲にありません。</summary>
        InvalidUtility = 8,

        /// <summary>weightが有限な0より大きく1,000,000以下の範囲にありません。</summary>
        InvalidWeight = 9
    }
}
