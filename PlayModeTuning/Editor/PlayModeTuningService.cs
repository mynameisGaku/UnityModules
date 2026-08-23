using System;
using System.Collections.Generic;

namespace PlayModeTuning.Editor
{
    /// <summary>Provides the public editor-only entry points for one explicit Play Mode tuning workflow.</summary>
    public static class PlayModeTuningService
    {
        private static readonly PlayModeTuningOperations Operations = new PlayModeTuningOperations(new UnityPlayModeTuningGateway(), new UnityPlayModeTuningSessionStore(), new PlayModeTuningPlanRegistry(), PlayModeTuningDomain.Token);

        /// <summary>Validates and arms an immutable copy of the selected top-level properties in Edit Mode.</summary>
        public static PlayModeTuningStartResult Start(IReadOnlyList<PlayModeTuningPropertySelection> selections)
        {
            return Operations.Start(selections);
        }

        /// <summary>Returns the current session state without capturing, previewing, or applying values.</summary>
        public static PlayModeTuningSession GetCurrentSession()
        {
            return Operations.GetCurrentSession();
        }

        /// <summary>Captures only the armed selected values after an explicit call during Play Mode.</summary>
        public static PlayModeTuningCaptureResult CaptureDuringPlay(Guid sessionId)
        {
            return Operations.CaptureDuringPlay(sessionId);
        }

        /// <summary>Creates one immutable single-use plan after Play Mode without changing scene values.</summary>
        public static PlayModeTuningPlan PreviewAfterPlay(Guid sessionId)
        {
            return Operations.PreviewAfterPlay(sessionId);
        }

        /// <summary>Consumes, revalidates, applies, post-verifies, and if needed rolls back one exact plan.</summary>
        public static PlayModeTuningApplyResult Apply(PlayModeTuningPlan plan)
        {
            return Operations.Apply(plan);
        }

        /// <summary>Ends the matching session without applying captured values.</summary>
        public static PlayModeTuningSession Discard(Guid sessionId)
        {
            return Operations.Discard(sessionId);
        }

        internal static PlayModeTuningOperations InternalOperations => Operations;
    }
}
