namespace GameplayThreat
{
    /// <summary>
    /// 入力順に適用した1件のthreat score増減明細を表します。
    /// </summary>
    public readonly struct ThreatScoreStep
    {
        /// <summary>増減列内のindexを取得します。</summary>
        public int AdjustmentIndex { get; }
        /// <summary>増減対象の識別子を取得します。</summary>
        public int TargetId { get; }
        /// <summary>適用前scoreを取得します。</summary>
        public double InputScore { get; }
        /// <summary>要求された増減量を取得します。</summary>
        public double RequestedDelta { get; }
        /// <summary>0下限を反映した実増減量を取得します。</summary>
        public double AppliedDelta { get; }
        /// <summary>適用後scoreを取得します。</summary>
        public double OutputScore { get; }
        /// <summary>負の要求が0下限でclampされたかを取得します。</summary>
        public bool WasClamped { get; }

        /// <summary>
        /// 1件の増減前後を不変値として保持します。
        /// </summary>
        public ThreatScoreStep(int adjustmentIndex, int targetId, double inputScore, double requestedDelta, double appliedDelta, double outputScore, bool wasClamped)
        {
            AdjustmentIndex = adjustmentIndex;
            TargetId = targetId;
            InputScore = inputScore;
            RequestedDelta = requestedDelta;
            AppliedDelta = appliedDelta;
            OutputScore = outputScore;
            WasClamped = wasClamped;
        }
    }
}
