namespace PlayModeTuning.Editor
{
    /// <summary>Reports apply and rollback outcomes independently after one plan is consumed.</summary>
    public sealed class PlayModeTuningApplyResult
    {
        internal PlayModeTuningApplyResult(bool applyAttempted, bool applySucceeded, PlayModeTuningError applyError, string applyMessage, bool rollbackAttempted, bool rollbackSucceeded, PlayModeTuningError rollbackError, string rollbackMessage, PlayModeTuningSession session)
        {
            ApplyAttempted = applyAttempted;
            ApplySucceeded = applySucceeded;
            ApplyError = applyError;
            ApplyMessage = applyMessage ?? string.Empty;
            RollbackAttempted = rollbackAttempted;
            RollbackSucceeded = rollbackSucceeded;
            RollbackError = rollbackError;
            RollbackMessage = rollbackMessage ?? string.Empty;
            Session = session;
        }

        public bool ApplyAttempted { get; }
        public bool ApplySucceeded { get; }
        public PlayModeTuningError ApplyError { get; }
        public string ApplyMessage { get; }
        public bool RollbackAttempted { get; }
        public bool RollbackSucceeded { get; }
        public PlayModeTuningError RollbackError { get; }
        public string RollbackMessage { get; }
        public PlayModeTuningSession Session { get; }
    }
}
