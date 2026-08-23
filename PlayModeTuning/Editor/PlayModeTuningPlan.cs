using System;
using System.Collections.Generic;
using System.Linq;

namespace PlayModeTuning.Editor
{
    /// <summary>Captures one immutable, exact-object, single-use preview of selected tuning changes.</summary>
    public sealed class PlayModeTuningPlan
    {
        internal PlayModeTuningPlan(PlayModeTuningError error, string message, Guid sessionId, Guid nonce, string revision, IEnumerable<PlayModeTuningChange> changes)
        {
            Error = error;
            Message = message ?? string.Empty;
            SessionId = sessionId;
            Nonce = nonce;
            Revision = revision ?? string.Empty;
            Changes = Array.AsReadOnly((changes ?? Enumerable.Empty<PlayModeTuningChange>()).ToArray());
        }

        public PlayModeTuningError Error { get; }
        public string Message { get; }
        public Guid SessionId { get; }
        public Guid Nonce { get; }
        public string Revision { get; }
        public IReadOnlyList<PlayModeTuningChange> Changes { get; }
        public bool IsReady => Error == PlayModeTuningError.None;
    }
}
