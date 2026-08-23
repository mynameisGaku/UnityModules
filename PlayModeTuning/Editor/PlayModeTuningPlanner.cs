using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PlayModeTuning.Editor
{
    /// <summary>Builds one deterministic plan from immutable baseline and captured values.</summary>
    internal static class PlayModeTuningPlanner
    {
        internal static PlayModeTuningPlan Create(PlayModeTuningPersistedSession session, Guid nonce)
        {
            if (session == null || !Guid.TryParseExact(session.sessionId, "N", out var sessionId) || sessionId == Guid.Empty)
                return Failure(PlayModeTuningError.SessionDataInvalid, "The stored session identity is invalid.");

            var ordered = PlayModeTuningIdentityOrder.OrderProperties(session.properties, item => item).ToArray();
            var changes = new List<PlayModeTuningChange>();
            foreach (var property in ordered)
            {
                if (!property.Baseline.EqualsExact(property.Captured))
                {
                    if (!PlayModeTuningValueCodec.TryCreateCanonicalDisplay(property.Baseline, out var beforeDisplay) || !PlayModeTuningValueCodec.TryCreateCanonicalDisplay(property.Captured, out var afterDisplay))
                        return Failure(PlayModeTuningError.SessionDataInvalid, "A stored value cannot produce an exact preview display.", sessionId);
                    changes.Add(new PlayModeTuningChange(property.targetName, property.typeName, property.propertyPath, property.Captured.Kind, beforeDisplay, afterDisplay));
                }
            }
            if (changes.Count == 0)
                return Failure(PlayModeTuningError.NoChanges, "The captured values match the Edit Mode baseline.", sessionId);

            var revisionTokens = new List<string>
            {
                "PlayModeTuningPlan",
                PlayModeTuningPersistedSession.CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture),
                session.sessionId,
                nonce.ToString("N")
            };
            foreach (var property in ordered)
            {
                revisionTokens.Add(property.componentKey);
                revisionTokens.Add(property.globalObjectId);
                revisionTokens.Add(property.sceneGuid);
                revisionTokens.Add(property.scenePath);
                revisionTokens.Add(property.scriptGuid);
                revisionTokens.Add(property.typeName);
                revisionTokens.Add(property.targetName);
                revisionTokens.Add(property.propertyPath);
                revisionTokens.Add(property.propertyType);
                revisionTokens.Add(property.numericType);
                revisionTokens.Add(property.baselineKind.ToString(CultureInfo.InvariantCulture));
                revisionTokens.Add(property.baselinePayload);
                revisionTokens.Add(property.baselineDisplay);
                revisionTokens.Add(property.capturedKind.ToString(CultureInfo.InvariantCulture));
                revisionTokens.Add(property.capturedPayload);
                revisionTokens.Add(property.capturedDisplay);
            }
            foreach (var component in PlayModeTuningIdentityOrder.OrderComponents(session.components, item => item.componentKey, item => item.scenePath, ordered))
            {
                revisionTokens.Add(component.componentKey);
                revisionTokens.Add(component.scenePath);
                revisionTokens.Add(component.baselineUnselectedFingerprint);
            }

            var revision = PlayModeTuningFingerprint.Compute(revisionTokens);
            return new PlayModeTuningPlan(PlayModeTuningError.None, string.Empty, sessionId, nonce, revision, changes);
        }

        internal static PlayModeTuningPlan Failure(PlayModeTuningError error, string message, Guid sessionId = default(Guid))
        {
            return new PlayModeTuningPlan(error, message, sessionId, Guid.Empty, string.Empty, Array.Empty<PlayModeTuningChange>());
        }
    }
}
