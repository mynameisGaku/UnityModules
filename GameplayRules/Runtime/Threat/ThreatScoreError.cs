namespace GameplayThreat
{
    /// <summary>
    /// threat score解決を開始できない、または途中で安全に完了できない理由を表します。
    /// </summary>
    public enum ThreatScoreError
    {
        /// <summary>失敗していません。</summary>
        None = 0,
        /// <summary>初期entry列がnullです。</summary>
        NullEntries = 1,
        /// <summary>初期entry数が1〜32件の範囲外です。</summary>
        EntryCountOutOfRange = 2,
        /// <summary>対象識別子が正ではありません。</summary>
        InvalidTargetId = 3,
        /// <summary>同じ対象識別子が重複しています。</summary>
        DuplicateTargetId = 4,
        /// <summary>初期scoreが有限の非負値ではありません。</summary>
        InvalidInitialScore = 5,
        /// <summary>増減列がnullです。</summary>
        NullAdjustments = 6,
        /// <summary>増減数が0〜64件の範囲外です。</summary>
        AdjustmentCountOutOfRange = 7,
        /// <summary>増減対象が初期entry列に存在しません。</summary>
        UnknownTargetId = 8,
        /// <summary>増減量が有限ではありません。</summary>
        InvalidAdjustmentDelta = 9,
        /// <summary>加算後のscoreを有限値として表現できません。</summary>
        ScoreOverflow = 10
    }
}
