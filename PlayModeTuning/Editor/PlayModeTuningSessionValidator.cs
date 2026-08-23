using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PlayModeTuning.Editor
{
    /// <summary>Validates every persisted field that can influence session state, preview, apply, or rollback.</summary>
    internal static class PlayModeTuningSessionValidator
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        internal static bool TryValidate(PlayModeTuningPersistedSession session, out string message)
        {
            message = string.Empty;
            if (session == null)
                return Fail("The stored session is missing.", out message);
            if (session.schemaVersion != PlayModeTuningPersistedSession.CurrentSchemaVersion)
                return Fail("The stored session schema is unsupported.", out message);
            if (!Guid.TryParseExact(session.sessionId, "N", out var sessionId))
                return Fail("The stored session identity is invalid.", out message);
            if (!Enum.IsDefined(typeof(PlayModeTuningPhase), session.phase) || (PlayModeTuningPhase)session.phase == PlayModeTuningPhase.Idle)
                return Fail("The stored session phase is invalid.", out message);
            if (!Enum.IsDefined(typeof(PlayModeTuningError), session.error))
                return Fail("The stored session error is invalid.", out message);
            if (!StringsArePresent(session) || session.properties == null || session.components == null)
                return Fail("The stored session contains null fields.", out message);

            var phase = (PlayModeTuningPhase)session.phase;
            if (IsNormalizedInvalidSession(sessionId, phase, (PlayModeTuningError)session.error, session))
                return true;
            if (sessionId == Guid.Empty)
                return Fail("An active stored session cannot use the empty identity.", out message);
            if (session.properties.Count == 0 || session.components.Count == 0)
                return Fail("The stored session has no target properties.", out message);
            if (session.properties.Count > PlayModeTuningOperations.MaximumProperties)
                return Fail("The stored session exceeds the 256-property limit.", out message);
            if (session.components.Count > PlayModeTuningOperations.MaximumComponents)
                return Fail("The stored session exceeds the 32-component limit.", out message);
            if (string.IsNullOrEmpty(session.startDomainToken))
                return Fail("The stored start domain token is missing.", out message);
            if ((phase == PlayModeTuningPhase.Capturable || phase == PlayModeTuningPhase.Captured || phase == PlayModeTuningPhase.ReadyToPreview || phase == PlayModeTuningPhase.Previewed) && string.IsNullOrEmpty(session.playDomainToken))
                return Fail("The stored Play Mode domain token is missing.", out message);

            var componentByKey = new Dictionary<string, PlayModeTuningComponentRecord>(StringComparer.Ordinal);
            foreach (var component in session.components)
            {
                if (component == null || string.IsNullOrEmpty(component.componentKey) || string.IsNullOrEmpty(component.scenePath) || string.IsNullOrEmpty(component.baselineUnselectedFingerprint))
                    return Fail("A stored component record is incomplete.", out message);
                if (!IsLowerHex(component.componentKey, 64) || !IsLowerHex(component.baselineUnselectedFingerprint, 64))
                    return Fail("A stored component fingerprint is invalid.", out message);
                if (!IsSavedScenePath(component.scenePath))
                    return Fail("A stored component Scene path is invalid.", out message);
                if (!componentByKey.TryAdd(component.componentKey, component))
                    return Fail("A stored component identity is duplicated.", out message);
            }

            var capturedRequired = phase == PlayModeTuningPhase.Captured || phase == PlayModeTuningPhase.ReadyToPreview || phase == PlayModeTuningPhase.Previewed;
            var seenProperties = new HashSet<string>(StringComparer.Ordinal);
            var usedComponents = new HashSet<string>(StringComparer.Ordinal);
            long payloadBytes = 0;
            foreach (var property in session.properties)
            {
                if (!TryValidateProperty(property, componentByKey, capturedRequired, out var propertyHasCaptured, out message))
                    return false;
                if (!seenProperties.Add(property.PropertyKey))
                    return Fail("A stored property identity is duplicated.", out message);
                usedComponents.Add(property.componentKey);
                payloadBytes += Encoding.UTF8.GetByteCount(property.PropertyKey);
                payloadBytes += Encoding.UTF8.GetByteCount(property.baselinePayload);
                if (propertyHasCaptured)
                    payloadBytes += Encoding.UTF8.GetByteCount(property.capturedPayload);
                if (payloadBytes > PlayModeTuningOperations.MaximumPayloadBytes)
                    return Fail("The stored value payload exceeds the 256 KiB limit.", out message);
            }
            if (usedComponents.Count != componentByKey.Count)
                return Fail("A stored component has no matching selected property.", out message);

            if (phase == PlayModeTuningPhase.Previewed)
            {
                if (!Guid.TryParseExact(session.planNonce, "N", out var nonce) || nonce == Guid.Empty || !IsLowerHex(session.planRevision, 64) || string.IsNullOrEmpty(session.planDomainToken))
                    return Fail("The previewed plan identity is incomplete.", out message);
            }
            return true;
        }

        private static bool TryValidateProperty(PlayModeTuningPropertyRecord property, IReadOnlyDictionary<string, PlayModeTuningComponentRecord> componentByKey, bool capturedRequired, out bool hasCaptured, out string message)
        {
            hasCaptured = false;
            message = string.Empty;
            if (property == null || !PropertyStringsArePresent(property))
                return Fail("A stored property record is incomplete.", out message);
            if (!componentByKey.TryGetValue(property.componentKey, out var component) || !StringComparer.Ordinal.Equals(component.scenePath, property.scenePath))
                return Fail("A stored property does not match its component Scene.", out message);
            if (!IsSavedScenePath(property.scenePath) || string.IsNullOrEmpty(property.globalObjectId) || string.IsNullOrEmpty(property.sceneGuid) || string.IsNullOrEmpty(property.scriptGuid) || string.IsNullOrEmpty(property.typeName) || string.IsNullOrEmpty(property.propertyPath) || string.IsNullOrEmpty(property.propertyType))
                return Fail("A stored property identity is incomplete.", out message);

            var expectedComponentKey = PlayModeTuningFingerprint.Compute(new[] { property.globalObjectId, property.sceneGuid, property.scenePath, property.scriptGuid, property.typeName });
            if (!StringComparer.Ordinal.Equals(expectedComponentKey, property.componentKey))
                return Fail("A stored component key does not match its direct identity fields.", out message);
            if (!Enum.IsDefined(typeof(PlayModeTuningValueKind), property.baselineKind))
                return Fail("A stored baseline value kind is invalid.", out message);
            var baselineKind = (PlayModeTuningValueKind)property.baselineKind;
            if (!KindMatchesDescriptor(baselineKind, property.propertyType, property.numericType) || !TryValidatePayload(baselineKind, property.baselinePayload, out message))
                return false;
            if (!PlayModeTuningValueCodec.TryCreateCanonicalDisplay(property.Baseline, out var baselineDisplay) || !StringComparer.Ordinal.Equals(property.baselineDisplay, baselineDisplay))
                return Fail("A stored baseline display does not match its exact payload.", out message);

            hasCaptured = capturedRequired || property.capturedKind != 0 || !string.IsNullOrEmpty(property.capturedPayload) || !string.IsNullOrEmpty(property.capturedDisplay);
            if (!hasCaptured)
                return true;
            if (!Enum.IsDefined(typeof(PlayModeTuningValueKind), property.capturedKind))
                return Fail("A stored captured value kind is invalid.", out message);
            var capturedKind = (PlayModeTuningValueKind)property.capturedKind;
            if (capturedKind != baselineKind || !KindMatchesDescriptor(capturedKind, property.propertyType, property.numericType) || !TryValidatePayload(capturedKind, property.capturedPayload, out message))
                return false;
            if (!PlayModeTuningValueCodec.TryCreateCanonicalDisplay(property.Captured, out var capturedDisplay) || !StringComparer.Ordinal.Equals(property.capturedDisplay, capturedDisplay))
                return Fail("A stored captured display does not match its exact payload.", out message);
            return true;
        }

        private static bool TryValidatePayload(PlayModeTuningValueKind kind, string payload, out string message)
        {
            message = string.Empty;
            try
            {
                switch (kind)
                {
                    case PlayModeTuningValueKind.Boolean:
                        return StringComparer.Ordinal.Equals(payload, "0") || StringComparer.Ordinal.Equals(payload, "1") || Fail("A stored Boolean payload is invalid.", out message);
                    case PlayModeTuningValueKind.SignedInteger:
                        return long.TryParse(payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) || Fail("A stored signed integer payload is invalid.", out message);
                    case PlayModeTuningValueKind.UnsignedInteger:
                        return ulong.TryParse(payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) || Fail("A stored unsigned integer payload is invalid.", out message);
                    case PlayModeTuningValueKind.Character:
                        return int.TryParse(payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out var character) && character >= char.MinValue && character <= char.MaxValue || Fail("A stored Character payload is invalid.", out message);
                    case PlayModeTuningValueKind.Float:
                        return TryValidateFloatToken(payload) || Fail("A stored Float payload is invalid or non-finite.", out message);
                    case PlayModeTuningValueKind.Double:
                        return TryValidateDoubleToken(payload) || Fail("A stored Double payload is invalid or non-finite.", out message);
                    case PlayModeTuningValueKind.String:
                        return TryValidateString(payload) || Fail("A stored String payload is invalid or exceeds 4096 UTF-8 bytes.", out message);
                    case PlayModeTuningValueKind.Enum:
                    case PlayModeTuningValueKind.LayerMask:
                        return int.TryParse(payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) || Fail("A stored integer payload is invalid.", out message);
                    case PlayModeTuningValueKind.Color:
                    case PlayModeTuningValueKind.Vector4:
                    case PlayModeTuningValueKind.Rect:
                    case PlayModeTuningValueKind.Quaternion:
                        return TryValidateFloatTuple(payload, 4) || Fail("A stored four-value payload is invalid or non-finite.", out message);
                    case PlayModeTuningValueKind.Vector2:
                        return TryValidateFloatTuple(payload, 2) || Fail("A stored Vector2 payload is invalid or non-finite.", out message);
                    case PlayModeTuningValueKind.Vector3:
                        return TryValidateFloatTuple(payload, 3) || Fail("A stored Vector3 payload is invalid or non-finite.", out message);
                    case PlayModeTuningValueKind.Bounds:
                        return TryValidateFloatTuple(payload, 6) || Fail("A stored Bounds payload is invalid or non-finite.", out message);
                    case PlayModeTuningValueKind.Vector2Int:
                        return TryValidateIntegerTuple(payload, 2) || Fail("A stored Vector2Int payload is invalid.", out message);
                    case PlayModeTuningValueKind.Vector3Int:
                        return TryValidateIntegerTuple(payload, 3) || Fail("A stored Vector3Int payload is invalid.", out message);
                    case PlayModeTuningValueKind.RectInt:
                        return TryValidateIntegerTuple(payload, 4) || Fail("A stored RectInt payload is invalid.", out message);
                    case PlayModeTuningValueKind.BoundsInt:
                        return TryValidateIntegerTuple(payload, 6) || Fail("A stored BoundsInt payload is invalid.", out message);
                    default:
                        return Fail("A stored value kind is unsupported.", out message);
                }
            }
            catch (Exception)
            {
                return Fail("A stored value payload could not be decoded.", out message);
            }
        }

        private static bool KindMatchesDescriptor(PlayModeTuningValueKind kind, string propertyType, string numericType)
        {
            switch (kind)
            {
                case PlayModeTuningValueKind.Boolean:
                    return MatchesNonNumeric(propertyType, numericType, "Boolean");
                case PlayModeTuningValueKind.SignedInteger:
                    return StringComparer.Ordinal.Equals(propertyType, "Integer") && new[] { "Int8", "Int16", "Int32", "Int64" }.Contains(numericType, StringComparer.Ordinal);
                case PlayModeTuningValueKind.UnsignedInteger:
                    return StringComparer.Ordinal.Equals(propertyType, "Integer") && new[] { "UInt8", "UInt16", "UInt32", "UInt64" }.Contains(numericType, StringComparer.Ordinal);
                case PlayModeTuningValueKind.Float:
                    return StringComparer.Ordinal.Equals(propertyType, "Float") && StringComparer.Ordinal.Equals(numericType, "Float");
                case PlayModeTuningValueKind.Double:
                    return StringComparer.Ordinal.Equals(propertyType, "Float") && StringComparer.Ordinal.Equals(numericType, "Double");
                default:
                    return MatchesNonNumeric(propertyType, numericType, kind.ToString());
            }
        }

        private static bool MatchesNonNumeric(string propertyType, string numericType, string expected)
        {
            return StringComparer.Ordinal.Equals(propertyType, expected) && string.IsNullOrEmpty(numericType);
        }

        private static bool TryValidateString(string payload)
        {
            var bytes = Convert.FromBase64String(payload);
            if (bytes.Length > PlayModeTuningValueCodec.MaximumStringUtf8Bytes)
                return false;
            var value = StrictUtf8.GetString(bytes);
            return StringComparer.Ordinal.Equals(Convert.ToBase64String(StrictUtf8.GetBytes(value)), payload);
        }

        private static bool TryValidateFloatTuple(string payload, int count)
        {
            var parts = payload.Split(',');
            return parts.Length == count && parts.All(TryValidateFloatToken);
        }

        private static bool TryValidateIntegerTuple(string payload, int count)
        {
            var parts = payload.Split(',');
            return parts.Length == count && parts.All(value => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _));
        }

        private static bool TryValidateFloatToken(string value)
        {
            return value != null && value.Length == 8 && uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _) && IsFinite(PlayModeTuningValueCodec.DecodeFloat(value));
        }

        private static bool TryValidateDoubleToken(string value)
        {
            return value != null && value.Length == 16 && ulong.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _) && IsFinite(PlayModeTuningValueCodec.DecodeDouble(value));
        }

        private static bool StringsArePresent(PlayModeTuningPersistedSession session)
        {
            return session.sessionId != null && session.message != null && session.startDomainToken != null && session.playDomainToken != null && session.planNonce != null && session.planRevision != null && session.planDomainToken != null;
        }

        private static bool PropertyStringsArePresent(PlayModeTuningPropertyRecord property)
        {
            return property.componentKey != null && property.globalObjectId != null && property.sceneGuid != null && property.scenePath != null && property.scriptGuid != null && property.typeName != null && property.targetName != null && property.propertyPath != null && property.propertyType != null && property.numericType != null && property.baselinePayload != null && property.baselineDisplay != null && property.capturedPayload != null && property.capturedDisplay != null;
        }

        private static bool IsNormalizedInvalidSession(Guid sessionId, PlayModeTuningPhase phase, PlayModeTuningError error, PlayModeTuningPersistedSession session)
        {
            return sessionId == Guid.Empty && phase == PlayModeTuningPhase.Stale && error == PlayModeTuningError.SessionDataInvalid && session.properties.Count == 0 && session.components.Count == 0;
        }

        private static bool IsSavedScenePath(string value)
        {
            return value.StartsWith("Assets/", StringComparison.Ordinal) && value.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLowerHex(string value, int length)
        {
            return value != null && value.Length == length && value.All(character => character >= '0' && character <= '9' || character >= 'a' && character <= 'f');
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool Fail(string value, out string message)
        {
            message = value;
            return false;
        }
    }
}
