namespace SceneWorkspace.Editor
{
    /// <summary>Reports apply and rollback independently so recovery never hides the original failure.</summary>
    public sealed class SceneWorkspaceApplyResult
    {
        internal SceneWorkspaceApplyResult(bool applyAttempted, bool applySucceeded, SceneWorkspaceError applyError, string applyMessage, bool rollbackAttempted, bool rollbackSucceeded, SceneWorkspaceError rollbackError, string rollbackMessage)
        {
            ApplyAttempted = applyAttempted;
            ApplySucceeded = applySucceeded;
            ApplyError = applyError;
            ApplyMessage = applyMessage ?? string.Empty;
            RollbackAttempted = rollbackAttempted;
            RollbackSucceeded = rollbackSucceeded;
            RollbackError = rollbackError;
            RollbackMessage = rollbackMessage ?? string.Empty;
        }

        public bool ApplyAttempted { get; }
        public bool ApplySucceeded { get; }
        public SceneWorkspaceError ApplyError { get; }
        public string ApplyMessage { get; }
        public bool RollbackAttempted { get; }
        public bool RollbackSucceeded { get; }
        public SceneWorkspaceError RollbackError { get; }
        public string RollbackMessage { get; }
        public bool Succeeded => ApplySucceeded && RollbackError == SceneWorkspaceError.None;
    }
}
