namespace PlayModeTuning.Editor
{
    /// <summary>Reports one explicit in-Play capture and its bounded payload size.</summary>
    public sealed class PlayModeTuningCaptureResult
    {
        internal PlayModeTuningCaptureResult(PlayModeTuningError error, string message, PlayModeTuningSession session, int capturedPropertyCount, int payloadBytes)
        {
            Error = error;
            Message = message ?? string.Empty;
            Session = session;
            CapturedPropertyCount = capturedPropertyCount;
            PayloadBytes = payloadBytes;
        }

        public PlayModeTuningError Error { get; }
        public string Message { get; }
        public PlayModeTuningSession Session { get; }
        public int CapturedPropertyCount { get; }
        public int PayloadBytes { get; }
        public bool Succeeded => Error == PlayModeTuningError.None;
    }
}
