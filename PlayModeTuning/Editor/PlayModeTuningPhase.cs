namespace PlayModeTuning.Editor
{
    /// <summary>Describes the explicit manual workflow state of the current session.</summary>
    public enum PlayModeTuningPhase
    {
        Idle,
        Armed,
        Capturable,
        Captured,
        ReadyToPreview,
        Previewed,
        Completed,
        Stale
    }
}
