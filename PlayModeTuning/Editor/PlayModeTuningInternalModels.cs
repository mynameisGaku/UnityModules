using System;
using System.Collections.Generic;
using System.Linq;

namespace PlayModeTuning.Editor
{
    [Serializable]
    internal sealed class PlayModeTuningPersistedSession
    {
        internal const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string sessionId = string.Empty;
        public int phase;
        public int error;
        public string message = string.Empty;
        public bool domainReloadDisabled;
        public string startDomainToken = string.Empty;
        public string playDomainToken = string.Empty;
        public string planNonce = string.Empty;
        public string planRevision = string.Empty;
        public string planDomainToken = string.Empty;
        public bool planConsumed;
        public List<PlayModeTuningPropertyRecord> properties = new List<PlayModeTuningPropertyRecord>();
        public List<PlayModeTuningComponentRecord> components = new List<PlayModeTuningComponentRecord>();
    }

    [Serializable]
    internal sealed class PlayModeTuningPropertyRecord
    {
        public string componentKey = string.Empty;
        public string globalObjectId = string.Empty;
        public string sceneGuid = string.Empty;
        public string scenePath = string.Empty;
        public string scriptGuid = string.Empty;
        public string typeName = string.Empty;
        public string targetName = string.Empty;
        public string propertyPath = string.Empty;
        public string propertyType = string.Empty;
        public string numericType = string.Empty;
        public int baselineKind;
        public string baselinePayload = string.Empty;
        public string baselineDisplay = string.Empty;
        public int capturedKind;
        public string capturedPayload = string.Empty;
        public string capturedDisplay = string.Empty;

        internal string PropertyKey => PlayModeTuningFingerprint.Compute(new[] { componentKey, propertyPath, propertyType, numericType });

        internal PlayModeTuningEncodedValue Baseline => new PlayModeTuningEncodedValue((PlayModeTuningValueKind)baselineKind, baselinePayload, baselineDisplay);

        internal PlayModeTuningEncodedValue Captured => new PlayModeTuningEncodedValue((PlayModeTuningValueKind)capturedKind, capturedPayload, capturedDisplay);
    }

    [Serializable]
    internal sealed class PlayModeTuningComponentRecord
    {
        public string componentKey = string.Empty;
        public string scenePath = string.Empty;
        public string baselineUnselectedFingerprint = string.Empty;
    }

    internal sealed class PlayModeTuningEncodedValue
    {
        internal PlayModeTuningEncodedValue(PlayModeTuningValueKind kind, string payload, string display)
        {
            Kind = kind;
            Payload = payload ?? string.Empty;
            Display = display ?? string.Empty;
        }

        internal PlayModeTuningValueKind Kind { get; }
        internal string Payload { get; }
        internal string Display { get; }
        internal bool EqualsExact(PlayModeTuningEncodedValue other)
        {
            return other != null && Kind == other.Kind && StringComparer.Ordinal.Equals(Payload, other.Payload);
        }
    }

    internal sealed class PlayModeTuningEnvironment
    {
        internal PlayModeTuningEnvironment(bool playing, bool playingOrWillChange, bool compiling, bool updating, bool sceneReloadDisabled, bool domainReloadDisabled)
        {
            Playing = playing;
            PlayingOrWillChange = playingOrWillChange;
            Compiling = compiling;
            Updating = updating;
            SceneReloadDisabled = sceneReloadDisabled;
            DomainReloadDisabled = domainReloadDisabled;
        }

        internal bool Playing { get; }
        internal bool PlayingOrWillChange { get; }
        internal bool Compiling { get; }
        internal bool Updating { get; }
        internal bool SceneReloadDisabled { get; }
        internal bool DomainReloadDisabled { get; }
    }

    internal sealed class PlayModeTuningGatewayPropertySnapshot
    {
        internal PlayModeTuningGatewayPropertySnapshot(PlayModeTuningPropertyRecord record, PlayModeTuningEncodedValue value)
        {
            Record = record;
            Value = value;
        }

        internal PlayModeTuningPropertyRecord Record { get; }
        internal PlayModeTuningEncodedValue Value { get; }
    }

    internal sealed class PlayModeTuningGatewayComponentSnapshot
    {
        internal PlayModeTuningGatewayComponentSnapshot(string componentKey, string scenePath, string unselectedFingerprint)
        {
            ComponentKey = componentKey ?? string.Empty;
            ScenePath = scenePath ?? string.Empty;
            UnselectedFingerprint = unselectedFingerprint ?? string.Empty;
        }

        internal string ComponentKey { get; }
        internal string ScenePath { get; }
        internal string UnselectedFingerprint { get; }
    }

    internal sealed class PlayModeTuningGatewaySnapshot
    {
        internal PlayModeTuningGatewaySnapshot(IEnumerable<PlayModeTuningGatewayPropertySnapshot> properties, IEnumerable<PlayModeTuningGatewayComponentSnapshot> components)
        {
            var orderedProperties = PlayModeTuningIdentityOrder.OrderProperties(properties, item => item.Record).ToArray();
            Properties = Array.AsReadOnly(orderedProperties);
            Components = Array.AsReadOnly(PlayModeTuningIdentityOrder.OrderComponents(components, item => item.ComponentKey, item => item.ScenePath, orderedProperties.Select(item => item.Record)).ToArray());
        }

        internal IReadOnlyList<PlayModeTuningGatewayPropertySnapshot> Properties { get; }
        internal IReadOnlyList<PlayModeTuningGatewayComponentSnapshot> Components { get; }
    }

    internal sealed class PlayModeTuningGatewayResult
    {
        private PlayModeTuningGatewayResult(PlayModeTuningError error, string message, PlayModeTuningGatewaySnapshot snapshot)
        {
            Error = error;
            Message = message ?? string.Empty;
            Snapshot = snapshot;
        }

        internal PlayModeTuningError Error { get; }
        internal string Message { get; }
        internal PlayModeTuningGatewaySnapshot Snapshot { get; }
        internal bool Succeeded => Error == PlayModeTuningError.None && Snapshot != null;

        internal static PlayModeTuningGatewayResult Success(PlayModeTuningGatewaySnapshot snapshot)
        {
            return new PlayModeTuningGatewayResult(PlayModeTuningError.None, string.Empty, snapshot);
        }

        internal static PlayModeTuningGatewayResult Failure(PlayModeTuningError error, string message)
        {
            return new PlayModeTuningGatewayResult(error, message, null);
        }
    }

    internal sealed class PlayModeTuningMutationResult
    {
        private PlayModeTuningMutationResult(PlayModeTuningError error, string message)
        {
            Error = error;
            Message = message ?? string.Empty;
        }

        internal PlayModeTuningError Error { get; }
        internal string Message { get; }
        internal bool Succeeded => Error == PlayModeTuningError.None;

        internal static PlayModeTuningMutationResult Success()
        {
            return new PlayModeTuningMutationResult(PlayModeTuningError.None, string.Empty);
        }

        internal static PlayModeTuningMutationResult Failure(PlayModeTuningError error, string message)
        {
            return new PlayModeTuningMutationResult(error, message);
        }
    }

    internal sealed class PlayModeTuningWrite
    {
        internal PlayModeTuningWrite(PlayModeTuningPropertyRecord record, PlayModeTuningEncodedValue value)
        {
            Record = record;
            Value = value;
        }

        internal PlayModeTuningPropertyRecord Record { get; }
        internal PlayModeTuningEncodedValue Value { get; }
    }
}
