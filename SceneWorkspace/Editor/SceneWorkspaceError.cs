namespace SceneWorkspace.Editor
{
    /// <summary>Identifies one bounded capture, preview, apply, verification, or rollback failure.</summary>
    public enum SceneWorkspaceError
    {
        None,
        InvalidProfile,
        ProfileNotSaved,
        NoScenes,
        MissingScene,
        DuplicateScene,
        UntitledScene,
        DirtyScene,
        UnsupportedScenePath,
        NoLoadedScene,
        InvalidActiveScene,
        PlayModeActive,
        EditorBusy,
        PrefabStageOpen,
        StalePlan,
        PlanAlreadyConsumed,
        ApplyInProgress,
        CaptureFailed,
        ApplyFailed,
        VerificationFailed,
        RollbackFailed
    }
}
