namespace GameplayAnalysis
{
    /// <summary>Sample Statisticsが要求を受理できなかった理由を表します。</summary>
    public enum SampleStatisticsError
    {
        /// <summary>失敗はありません。</summary>
        None = 0,
        /// <summary>sample配列がnullです。</summary>
        NullSamples = 1,
        /// <summary>開始indexが負です。</summary>
        InvalidStartIndex = 2,
        /// <summary>sample件数が1以上32以下ではありません。</summary>
        InvalidSampleCount = 3,
        /// <summary>指定範囲がsample配列内に収まりません。</summary>
        RangeOutOfBounds = 4,
        /// <summary>指定範囲にNaNまたはInfinityが含まれます。</summary>
        NonFiniteSample = 5,
        /// <summary>有限sampleから有限なrange・平均・母分散・母標準偏差を表現できません。</summary>
        ResultOutOfRange = 6
    }
}
