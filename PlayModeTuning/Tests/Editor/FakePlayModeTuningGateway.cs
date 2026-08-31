using System;
using System.Collections.Generic;
using System.Linq;

namespace PlayModeTuning.Editor.Tests
{
    internal sealed class FakePlayModeTuningGateway : IPlayModeTuningGateway
    {
        private readonly Dictionary<string, PlayModeTuningEncodedValue> values = new Dictionary<string, PlayModeTuningEncodedValue>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> unselected = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<string> firstApplyOrder = new List<string>();
        private Dictionary<string, PlayModeTuningEncodedValue> valuesBeforeApply;
        private Dictionary<string, string> unselectedBeforeApply;

        internal PlayModeTuningEnvironment Environment { get; set; } = EditEnvironment();
        internal int ApplyCalls { get; private set; }
        internal int CompleteApplyCalls { get; private set; }
        internal int ReleaseApplyCalls { get; private set; }
        internal int RevertApplyCalls { get; private set; }
        internal IReadOnlyList<string> FirstApplyOrder => Array.AsReadOnly(firstApplyOrder.ToArray());
        internal int MarkDirtyCalls { get; private set; }
        internal int FailApplyCall { get; set; }
        internal bool FailCapture { get; set; }
        internal bool FailMarkDirty { get; set; }
        internal bool FailCompleteApply { get; set; }
        internal bool ChangeUnselectedOnFirstApply { get; set; }
        internal bool KeepUnselectedResidualOnRollback { get; set; }
        internal string SelectedSideEffectComponent { get; set; } = string.Empty;
        internal string SelectedSideEffectProperty { get; set; } = string.Empty;
        internal PlayModeTuningEncodedValue SelectedSideEffectValue { get; set; }

        public PlayModeTuningEnvironment GetEnvironment()
        {
            return Environment;
        }

        public PlayModeTuningGatewayResult ResolveSelections(IReadOnlyList<PlayModeTuningPropertySelection> selections)
        {
            var records = new List<PlayModeTuningPropertyRecord>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var selection in selections ?? Array.Empty<PlayModeTuningPropertySelection>())
            {
                if (!TryParseSelector(selection?.PropertyPath, out var component, out var property))
                    return PlayModeTuningGatewayResult.Failure(PlayModeTuningError.InvalidSelection, "Invalid fake selector.");
                var componentKey = ComponentKey(component);
                var record = new PlayModeTuningPropertyRecord
                {
                    componentKey = componentKey,
                    globalObjectId = "GlobalObjectId_V1-2-" + component,
                    sceneGuid = "scene-guid",
                    scenePath = "Assets/FakeScene.unity",
                    scriptGuid = "script-guid-" + component,
                    typeName = "Fake." + component + ", FakeAssembly",
                    targetName = component,
                    propertyPath = property,
                    propertyType = "Float",
                    numericType = "Float"
                };
                var logical = LogicalKey(component, property);
                if (!values.TryGetValue(logical, out var value))
                {
                    value = FloatValue(0f);
                    values[logical] = value;
                }
                record.propertyType = value.Kind == PlayModeTuningValueKind.String ? "String" : "Float";
                record.numericType = value.Kind == PlayModeTuningValueKind.String ? string.Empty : "Float";
                if (!seen.Add(record.PropertyKey))
                    return PlayModeTuningGatewayResult.Failure(PlayModeTuningError.DuplicateProperty, "Duplicate fake property.");
                record.baselineKind = (int)value.Kind;
                record.baselinePayload = value.Payload;
                record.baselineDisplay = value.Display;
                records.Add(record);
                if (!unselected.ContainsKey(componentKey))
                    unselected[componentKey] = UnselectedBaseline(component);
            }
            return CreateSnapshot(records);
        }

        public PlayModeTuningGatewayResult Capture(IReadOnlyList<PlayModeTuningPropertyRecord> properties)
        {
            if (FailCapture)
                return PlayModeTuningGatewayResult.Failure(PlayModeTuningError.CaptureFailed, "Injected capture failure.");
            return CreateSnapshot(properties ?? Array.Empty<PlayModeTuningPropertyRecord>());
        }

        public PlayModeTuningMutationResult Apply(IReadOnlyList<PlayModeTuningWrite> writes)
        {
            if (valuesBeforeApply != null)
                return PlayModeTuningMutationResult.Failure(PlayModeTuningError.ApplyInProgress, "Injected transaction overlap.");
            ApplyCalls++;
            valuesBeforeApply = new Dictionary<string, PlayModeTuningEncodedValue>(values, StringComparer.Ordinal);
            unselectedBeforeApply = new Dictionary<string, string>(unselected, StringComparer.Ordinal);
            foreach (var write in writes ?? Array.Empty<PlayModeTuningWrite>())
            {
                if (ApplyCalls == 1)
                    firstApplyOrder.Add(write.Record.globalObjectId + "|" + write.Record.propertyPath);
                values[LogicalKey(write.Record.targetName, write.Record.propertyPath)] = write.Value;
            }
            if (ApplyCalls == 1 && SelectedSideEffectValue != null)
                values[LogicalKey(SelectedSideEffectComponent, SelectedSideEffectProperty)] = SelectedSideEffectValue;
            if (ApplyCalls == 1 && ChangeUnselectedOnFirstApply)
            {
                foreach (var component in (writes ?? Array.Empty<PlayModeTuningWrite>()).Select(item => item.Record.componentKey).Distinct(StringComparer.Ordinal))
                    unselected[component] = "onvalidate-side-effect";
            }
            if (FailApplyCall == ApplyCalls)
                return PlayModeTuningMutationResult.Failure(PlayModeTuningError.ApplyFailed, "Injected apply failure.");
            return PlayModeTuningMutationResult.Success();
        }

        public PlayModeTuningMutationResult CompleteApply()
        {
            CompleteApplyCalls++;
            if (FailCompleteApply)
                return PlayModeTuningMutationResult.Failure(PlayModeTuningError.ApplyFailed, "Injected completion failure.");
            return PlayModeTuningMutationResult.Success();
        }

        public void ReleaseApply()
        {
            ReleaseApplyCalls++;
            valuesBeforeApply = null;
            unselectedBeforeApply = null;
        }

        public PlayModeTuningMutationResult RevertApply()
        {
            RevertApplyCalls++;
            if (valuesBeforeApply == null)
                return PlayModeTuningMutationResult.Success();
            values.Clear();
            foreach (var pair in valuesBeforeApply)
                values.Add(pair.Key, pair.Value);
            if (!KeepUnselectedResidualOnRollback)
            {
                unselected.Clear();
                foreach (var pair in unselectedBeforeApply)
                    unselected.Add(pair.Key, pair.Value);
            }
            valuesBeforeApply = null;
            unselectedBeforeApply = null;
            return PlayModeTuningMutationResult.Success();
        }

        public PlayModeTuningMutationResult MarkScenesDirty(IReadOnlyList<string> scenePaths)
        {
            MarkDirtyCalls++;
            return FailMarkDirty ? PlayModeTuningMutationResult.Failure(PlayModeTuningError.SceneDirtyFailed, "Injected dirty failure.") : PlayModeTuningMutationResult.Success();
        }

        internal void SetValue(string component, string property, PlayModeTuningEncodedValue value)
        {
            values[LogicalKey(component, property)] = value;
        }

        internal PlayModeTuningEncodedValue GetValue(string component, string property)
        {
            return values[LogicalKey(component, property)];
        }

        internal void SetUnselected(string component, string fingerprint)
        {
            unselected[ComponentKey(component)] = fingerprint;
        }

        internal static PlayModeTuningPropertySelection Selection(string component, string property)
        {
            return new PlayModeTuningPropertySelection(null, component + "|" + property);
        }

        internal static PlayModeTuningEncodedValue FloatValue(float value)
        {
            return new PlayModeTuningEncodedValue(PlayModeTuningValueKind.Float, PlayModeTuningValueCodec.EncodeFloat(value), value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        }

        internal static PlayModeTuningEncodedValue TextValue(string payload)
        {
            return new PlayModeTuningEncodedValue(PlayModeTuningValueKind.String, Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload)), payload);
        }

        internal static PlayModeTuningEnvironment EditEnvironment(bool disableDomainReload = false, bool disableSceneReload = false)
        {
            return new PlayModeTuningEnvironment(false, false, false, false, disableSceneReload, disableDomainReload);
        }

        internal static PlayModeTuningEnvironment PlayEnvironment(bool disableDomainReload = false, bool disableSceneReload = false)
        {
            return new PlayModeTuningEnvironment(true, true, false, false, disableSceneReload, disableDomainReload);
        }

        private PlayModeTuningGatewayResult CreateSnapshot(IEnumerable<PlayModeTuningPropertyRecord> source)
        {
            var records = source.ToArray();
            var properties = new List<PlayModeTuningGatewayPropertySnapshot>();
            foreach (var record in records)
            {
                var component = FindComponentName(record.componentKey);
                var logical = LogicalKey(component, record.propertyPath);
                if (!values.TryGetValue(logical, out var value))
                    return PlayModeTuningGatewayResult.Failure(PlayModeTuningError.TargetMissing, "Missing fake value.");
                properties.Add(new PlayModeTuningGatewayPropertySnapshot(CopyRecordWithTargetName(record, component), value));
            }
            var components = records.GroupBy(item => item.componentKey, StringComparer.Ordinal).Select(group =>
            {
                var first = group.First();
                var fingerprint = unselected.TryGetValue(group.Key, out var value) ? value : UnselectedBaseline(first.targetName);
                return new PlayModeTuningGatewayComponentSnapshot(group.Key, first.scenePath, fingerprint);
            });
            return PlayModeTuningGatewayResult.Success(new PlayModeTuningGatewaySnapshot(properties, components));
        }

        private static bool TryParseSelector(string selector, out string component, out string property)
        {
            component = string.Empty;
            property = string.Empty;
            if (string.IsNullOrEmpty(selector))
                return false;
            var separator = selector.IndexOf('|');
            if (separator <= 0 || separator >= selector.Length - 1)
                return false;
            component = selector.Substring(0, separator);
            property = selector.Substring(separator + 1);
            return true;
        }

        private static string LogicalKey(string component, string property)
        {
            return component + "|" + property;
        }

        private static string ComponentKey(string component)
        {
            return PlayModeTuningFingerprint.Compute(new[] { "GlobalObjectId_V1-2-" + component, "scene-guid", "Assets/FakeScene.unity", "script-guid-" + component, "Fake." + component + ", FakeAssembly" });
        }

        private static string UnselectedBaseline(string component)
        {
            return PlayModeTuningFingerprint.Compute(new[] { "unselected-baseline", component });
        }

        private static PlayModeTuningPropertyRecord CopyRecordWithTargetName(PlayModeTuningPropertyRecord source, string targetName)
        {
            return new PlayModeTuningPropertyRecord
            {
                componentKey = source.componentKey,
                globalObjectId = source.globalObjectId,
                sceneGuid = source.sceneGuid,
                scenePath = source.scenePath,
                scriptGuid = source.scriptGuid,
                typeName = source.typeName,
                targetName = targetName,
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

        private static string FindComponentName(string componentKey)
        {
            for (var index = 0; index < 1024; index++)
            {
                var candidate = "c" + index;
                if (StringComparer.Ordinal.Equals(ComponentKey(candidate), componentKey))
                    return candidate;
            }
            return "c0";
        }
    }
}
