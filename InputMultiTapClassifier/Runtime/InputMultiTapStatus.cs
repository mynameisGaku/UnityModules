namespace InputMultiTapping
{
    /// <summary>1回のsample後に観測できるtap burstの状態とevent。</summary>
    public readonly struct InputMultiTapStatus
    {
        /// <summary>最後に受理したsimulation tick。</summary>
        public ulong CurrentTick { get; }

        /// <summary>確定待ちのtap数。</summary>
        public int PendingTapCount { get; }

        /// <summary>確定待ちのtapが存在するか。</summary>
        public bool HasPendingTaps => PendingTapCount > 0;

        /// <summary>確定待ちburstが許容する最後のtick。待ちが無い場合は0。</summary>
        public ulong PendingDeadlineTick { get; }

        /// <summary>今回のsampleでtapを受理したか。</summary>
        public bool TapAcceptedThisSample { get; }

        /// <summary>今回のsampleで確定したtap数。未確定時は0。</summary>
        public int CompletedTapCount { get; }

        /// <summary>今回のsampleでburstを確定したか。</summary>
        public bool CompletedThisSample => CompletedTapCount > 0;

        /// <summary>今回のsampleでburstを確定した理由。</summary>
        public InputMultiTapCompletionReason CompletionReason { get; }

        internal InputMultiTapStatus(ulong currentTick, int pendingTapCount, ulong pendingDeadlineTick, bool tapAcceptedThisSample, int completedTapCount, InputMultiTapCompletionReason completionReason)
        {
            CurrentTick = currentTick;
            PendingTapCount = pendingTapCount;
            PendingDeadlineTick = pendingDeadlineTick;
            TapAcceptedThisSample = tapAcceptedThisSample;
            CompletedTapCount = completedTapCount;
            CompletionReason = completionReason;
        }
    }
}
