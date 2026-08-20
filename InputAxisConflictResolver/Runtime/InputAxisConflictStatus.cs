namespace InputAxisConflict
{
    /// <summary>1回のaxis入力sampleを処理した後の状態と、そのsampleで発生したedge。</summary>
    public readonly struct InputAxisConflictStatus
    {
        /// <summary>最後に受理したsimulation tick。</summary>
        public ulong CurrentTick { get; }

        /// <summary>競合時に使用する解決policy。</summary>
        public InputAxisConflictPolicy Policy { get; }

        /// <summary>処理後にnegative入力が押下中か。</summary>
        public bool NegativePressed { get; }

        /// <summary>処理後にpositive入力が押下中か。</summary>
        public bool PositivePressed { get; }

        /// <summary>今回negative入力が非押下から押下へ変わったか。</summary>
        public bool NegativePressedThisSample { get; }

        /// <summary>今回positive入力が非押下から押下へ変わったか。</summary>
        public bool PositivePressedThisSample { get; }

        /// <summary>今回negative入力が押下から非押下へ変わったか。</summary>
        public bool NegativeReleasedThisSample { get; }

        /// <summary>今回positive入力が押下から非押下へ変わったか。</summary>
        public bool PositiveReleasedThisSample { get; }

        /// <summary>処理後にnegativeとpositiveが同時押下中か。</summary>
        public bool HasConflict { get; }

        /// <summary>処理後の解決値。negativeは-1、neutralは0、positiveは1。</summary>
        public int ResolvedValue { get; }

        /// <summary>今回の処理で解決値が変化したか。</summary>
        public bool ResolutionChanged { get; }

        internal InputAxisConflictStatus(ulong currentTick, InputAxisConflictPolicy policy, bool negativePressed, bool positivePressed, bool negativePressedThisSample, bool positivePressedThisSample, bool negativeReleasedThisSample, bool positiveReleasedThisSample, int resolvedValue, bool resolutionChanged)
        {
            CurrentTick = currentTick;
            Policy = policy;
            NegativePressed = negativePressed;
            PositivePressed = positivePressed;
            NegativePressedThisSample = negativePressedThisSample;
            PositivePressedThisSample = positivePressedThisSample;
            NegativeReleasedThisSample = negativeReleasedThisSample;
            PositiveReleasedThisSample = positiveReleasedThisSample;
            HasConflict = negativePressed && positivePressed;
            ResolvedValue = resolvedValue;
            ResolutionChanged = resolutionChanged;
        }
    }
}
