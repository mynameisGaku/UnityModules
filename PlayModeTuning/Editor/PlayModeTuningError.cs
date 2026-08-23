namespace PlayModeTuning.Editor
{
    /// <summary>Identifies one bounded session, capture, preview, apply, or rollback failure.</summary>
    public enum PlayModeTuningError
    {
        None,
        InvalidSelection,
        InvalidSession,
        WrongPhase,
        EditorBusy,
        PlayModeRequired,
        EditModeRequired,
        DisableSceneReloadUnsupported,
        DomainReloadMismatch,
        TooManyComponents,
        TooManyProperties,
        PayloadTooLarge,
        StringTooLong,
        UnsupportedTarget,
        UnsupportedProperty,
        DuplicateProperty,
        TargetMissing,
        IdentityMismatch,
        NonFiniteValue,
        CaptureFailed,
        NoChanges,
        StaleSession,
        StalePlan,
        PlanAlreadyConsumed,
        ApplyInProgress,
        ApplyFailed,
        VerificationFailed,
        SceneDirtyFailed,
        RollbackFailed,
        SessionDataInvalid
    }
}
