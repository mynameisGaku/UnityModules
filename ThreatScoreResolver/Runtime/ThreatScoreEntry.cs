namespace GameplayThreat
{
    /// <summary>
    /// 1対象の正の識別子と現在の非負threat scoreを表します。
    /// </summary>
    public readonly struct ThreatScoreEntry
    {
        /// <summary>
        /// 正の対象識別子を取得します。
        /// </summary>
        public int TargetId { get; }

        /// <summary>
        /// 現在の非負threat scoreを取得します。
        /// </summary>
        public double Score { get; }

        /// <summary>
        /// 対象識別子と現在scoreを保持します。入力検証は<see cref="ThreatScoreResolver"/>が行います。
        /// </summary>
        public ThreatScoreEntry(int targetId, double score)
        {
            TargetId = targetId;
            Score = score;
        }
    }
}
