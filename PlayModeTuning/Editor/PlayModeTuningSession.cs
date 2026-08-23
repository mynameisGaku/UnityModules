using System;

namespace PlayModeTuning.Editor
{
    /// <summary>Reports the immutable current state of one explicit Play Mode tuning session.</summary>
    public sealed class PlayModeTuningSession
    {
        internal PlayModeTuningSession(Guid sessionId, PlayModeTuningPhase phase, PlayModeTuningError error, string message, int componentCount, int propertyCount)
        {
            SessionId = sessionId;
            Phase = phase;
            Error = error;
            Message = message ?? string.Empty;
            ComponentCount = componentCount;
            PropertyCount = propertyCount;
        }

        public Guid SessionId { get; }
        public PlayModeTuningPhase Phase { get; }
        public PlayModeTuningError Error { get; }
        public string Message { get; }
        public int ComponentCount { get; }
        public int PropertyCount { get; }
        public bool IsTerminal => Phase == PlayModeTuningPhase.Completed || Phase == PlayModeTuningPhase.Stale;
    }
}
