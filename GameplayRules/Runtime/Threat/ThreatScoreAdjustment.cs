namespace GameplayThreat
{
    /// <summary>
    /// 1対象へ入力順に適用する有限のthreat score増減を表します。
    /// </summary>
    public readonly struct ThreatScoreAdjustment
    {
        /// <summary>
        /// 増減対象の正の識別子を取得します。
        /// </summary>
        public int TargetId { get; }

        /// <summary>
        /// 正なら加算、負なら減算する有限量を取得します。
        /// </summary>
        public double Delta { get; }

        /// <summary>
        /// 増減対象と適用量を保持します。入力検証は<see cref="ThreatScoreResolver"/>が行います。
        /// </summary>
        public ThreatScoreAdjustment(int targetId, double delta)
        {
            TargetId = targetId;
            Delta = delta;
        }
    }
}
