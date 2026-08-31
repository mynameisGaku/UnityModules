using System;
using System.Collections.Generic;
using System.Linq;

namespace PlayModeTuning.Editor.Tests
{
    internal sealed class FakePlayModeTuningSessionStore : IPlayModeTuningSessionStore
    {
        private PlayModeTuningPersistedSession current;

        internal Exception LoadFailure { get; set; }
        internal int SaveCalls { get; private set; }
        internal int SaveFailureCall { get; set; }
        internal ISet<int> SaveFailureCalls { get; } = new HashSet<int>();
        internal string SaveFailureDetail { get; set; } = "Injected save failure.";
        internal bool ClearFailure { get; set; }
        internal string ClearFailureDetail { get; set; } = "Injected clear failure.";

        public PlayModeTuningPersistedSession Load()
        {
            if (LoadFailure != null)
                throw LoadFailure;
            return Clone(current);
        }

        public void Save(PlayModeTuningPersistedSession session)
        {
            SaveCalls++;
            if (SaveCalls == SaveFailureCall || SaveFailureCalls.Contains(SaveCalls))
                throw new InvalidOperationException(SaveFailureDetail);
            current = Clone(session);
        }

        public void Clear()
        {
            if (ClearFailure)
                throw new InvalidOperationException(ClearFailureDetail);
            current = null;
        }

        internal PlayModeTuningPersistedSession Current => Load();

        internal void Inject(PlayModeTuningPersistedSession session)
        {
            current = Clone(session);
        }

        private static PlayModeTuningPersistedSession Clone(PlayModeTuningPersistedSession source)
        {
            if (source == null)
                return null;
            return new PlayModeTuningPersistedSession
            {
                schemaVersion = source.schemaVersion,
                sessionId = source.sessionId,
                phase = source.phase,
                error = source.error,
                message = source.message,
                domainReloadDisabled = source.domainReloadDisabled,
                startDomainToken = source.startDomainToken,
                playDomainToken = source.playDomainToken,
                planNonce = source.planNonce,
                planRevision = source.planRevision,
                planDomainToken = source.planDomainToken,
                planConsumed = source.planConsumed,
                properties = source.properties == null ? null : source.properties.Select(Clone).ToList(),
                components = source.components == null ? null : source.components.Select(Clone).ToList()
            };
        }

        private static PlayModeTuningPropertyRecord Clone(PlayModeTuningPropertyRecord source)
        {
            if (source == null)
                return null;
            return new PlayModeTuningPropertyRecord
            {
                componentKey = source.componentKey,
                globalObjectId = source.globalObjectId,
                sceneGuid = source.sceneGuid,
                scenePath = source.scenePath,
                scriptGuid = source.scriptGuid,
                typeName = source.typeName,
                targetName = source.targetName,
                propertyPath = source.propertyPath,
                propertyType = source.propertyType,
                numericType = source.numericType,
                baselineKind = source.baselineKind,
                baselinePayload = source.baselinePayload,
                baselineDisplay = source.baselineDisplay,
                capturedKind = source.capturedKind,
                capturedPayload = source.capturedPayload,
                capturedDisplay = source.capturedDisplay
            };
        }

        private static PlayModeTuningComponentRecord Clone(PlayModeTuningComponentRecord source)
        {
            if (source == null)
                return null;
            return new PlayModeTuningComponentRecord
            {
                componentKey = source.componentKey,
                scenePath = source.scenePath,
                baselineUnselectedFingerprint = source.baselineUnselectedFingerprint
            };
        }
    }
}
