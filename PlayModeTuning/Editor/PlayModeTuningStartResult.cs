namespace PlayModeTuning.Editor
{
    /// <summary>Reports whether a bounded selection was armed before entering Play Mode.</summary>
    public sealed class PlayModeTuningStartResult
    {
        internal PlayModeTuningStartResult(PlayModeTuningError error, string message, PlayModeTuningSession session)
        {
            Error = error;
            Message = message ?? string.Empty;
            Session = session;
        }

        public PlayModeTuningError Error { get; }
        public string Message { get; }
        public PlayModeTuningSession Session { get; }
        public bool Succeeded => Error == PlayModeTuningError.None;
    }
}
