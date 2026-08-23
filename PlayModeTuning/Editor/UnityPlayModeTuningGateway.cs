using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlayModeTuning.Editor
{
    /// <summary>Resolves exact scene MonoBehaviour identities and performs bounded SerializedObject operations.</summary>
    internal sealed class UnityPlayModeTuningGateway : IPlayModeTuningGateway
    {
        public PlayModeTuningEnvironment GetEnvironment()
        {
            var options = EditorSettings.enterPlayModeOptionsEnabled ? EditorSettings.enterPlayModeOptions : EnterPlayModeOptions.None;
            return new PlayModeTuningEnvironment(
                EditorApplication.isPlaying,
                EditorApplication.isPlayingOrWillChangePlaymode,
                EditorApplication.isCompiling,
                EditorApplication.isUpdating,
                (options & EnterPlayModeOptions.DisableSceneReload) != 0,
                (options & EnterPlayModeOptions.DisableDomainReload) != 0);
        }

        public PlayModeTuningGatewayResult ResolveSelections(IReadOnlyList<PlayModeTuningPropertySelection> selections)
        {
            try
            {
                var records = new List<PlayModeTuningPropertyRecord>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var selection in selections ?? Array.Empty<PlayModeTuningPropertySelection>())
                {
                    if (selection == null || !(selection.Target is MonoBehaviour target) || string.IsNullOrWhiteSpace(selection.PropertyPath))
                        return PlayModeTuningGatewayResult.Failure(PlayModeTuningError.InvalidSelection, "Each selection requires one scene MonoBehaviour and one property path.");
                    var identityResult = TryCreateIdentity(target, out var identity, out var identityError, out var identityMessage);
                    if (!identityResult)
                        return PlayModeTuningGatewayResult.Failure(identityError, identityMessage);

                    var serialized = new SerializedObject(target);
                    serialized.UpdateIfRequiredOrScript();
                    var property = serialized.FindProperty(selection.PropertyPath);
                    if (property == null)
                        return PlayModeTuningGatewayResult.Failure(PlayModeTuningError.UnsupportedProperty, "A selected property path does not exist: " + selection.PropertyPath);
                    if (!PlayModeTuningValueCodec.TryEncode(property, out var value, out var valueError, out var valueMessage))
                        return PlayModeTuningGatewayResult.Failure(valueError, valueMessage + " Property: " + selection.PropertyPath);

                    var record = new PlayModeTuningPropertyRecord
                    {
                        componentKey = identity.ComponentKey,
                        globalObjectId = identity.GlobalObjectId,
                        sceneGuid = identity.SceneGuid,
                        scenePath = identity.ScenePath,
                        scriptGuid = identity.ScriptGuid,
                        typeName = identity.TypeName,
                        targetName = target.gameObject.name,
                        propertyPath = property.propertyPath,
                        propertyType = PlayModeTuningValueCodec.PropertyTypeName(property),
                        numericType = PlayModeTuningValueCodec.NumericTypeName(property),
                        baselineKind = (int)value.Kind,
                        baselinePayload = value.Payload,
                        baselineDisplay = value.Display
                    };
                    if (!seen.Add(record.PropertyKey))
                        return PlayModeTuningGatewayResult.Failure(PlayModeTuningError.DuplicateProperty, "The same component property was selected more than once.");
                    records.Add(record);
                }
                return Capture(records);
            }
            catch (Exception exception)
            {
                return PlayModeTuningGatewayResult.Failure(PlayModeTuningError.CaptureFailed, "The selections could not be resolved: " + exception.Message);
            }
        }

        public PlayModeTuningGatewayResult Capture(IReadOnlyList<PlayModeTuningPropertyRecord> properties)
        {
            try
            {
                var propertySnapshots = new List<PlayModeTuningGatewayPropertySnapshot>();
                var componentSnapshots = new List<PlayModeTuningGatewayComponentSnapshot>();
                foreach (var group in PlayModeTuningIdentityOrder.OrderProperties(properties, item => item).GroupBy(item => item.componentKey, StringComparer.Ordinal))
                {
                    var first = group.First();
                    if (!TryResolveExact(first, out var target, out var resolveError, out var resolveMessage))
                        return PlayModeTuningGatewayResult.Failure(resolveError, resolveMessage);
                    var serialized = new SerializedObject(target);
                    serialized.UpdateIfRequiredOrScript();
                    var selectedPaths = new HashSet<string>(group.Select(item => item.propertyPath), StringComparer.Ordinal);
                    foreach (var record in PlayModeTuningIdentityOrder.OrderProperties(group, item => item))
                    {
                        var property = serialized.FindProperty(record.propertyPath);
                        if (property == null)
                            return PlayModeTuningGatewayResult.Failure(PlayModeTuningError.TargetMissing, "A selected property no longer exists: " + record.propertyPath);
                        if (!DescriptorMatches(record, property))
                            return PlayModeTuningGatewayResult.Failure(PlayModeTuningError.IdentityMismatch, "A selected property type changed: " + record.propertyPath);
                        if (!PlayModeTuningValueCodec.TryEncode(property, out var value, out var valueError, out var valueMessage))
                            return PlayModeTuningGatewayResult.Failure(valueError, valueMessage + " Property: " + record.propertyPath);
                        propertySnapshots.Add(new PlayModeTuningGatewayPropertySnapshot(CopyRecordWithTargetName(record, target.gameObject.name), value));
                    }
                    componentSnapshots.Add(new PlayModeTuningGatewayComponentSnapshot(group.Key, first.scenePath, ComputeUnselectedFingerprint(serialized, selectedPaths)));
                }
                return PlayModeTuningGatewayResult.Success(new PlayModeTuningGatewaySnapshot(propertySnapshots, componentSnapshots));
            }
            catch (Exception exception)
            {
                return PlayModeTuningGatewayResult.Failure(PlayModeTuningError.CaptureFailed, "The selected values could not be captured: " + exception.Message);
            }
        }

        public PlayModeTuningMutationResult Apply(IReadOnlyList<PlayModeTuningWrite> writes)
        {
            try
            {
                foreach (var group in PlayModeTuningIdentityOrder.OrderProperties(writes, item => item.Record).GroupBy(item => item.Record.componentKey, StringComparer.Ordinal))
                {
                    var first = group.First().Record;
                    if (!TryResolveExact(first, out var target, out var resolveError, out var resolveMessage))
                        return PlayModeTuningMutationResult.Failure(resolveError, resolveMessage);
                    var serialized = new SerializedObject(target);
                    serialized.UpdateIfRequiredOrScript();
                    foreach (var write in PlayModeTuningIdentityOrder.OrderProperties(group, item => item.Record))
                    {
                        var property = serialized.FindProperty(write.Record.propertyPath);
                        if (property == null || !DescriptorMatches(write.Record, property))
                            return PlayModeTuningMutationResult.Failure(PlayModeTuningError.IdentityMismatch, "A destination property changed before apply: " + write.Record.propertyPath);
                        if (!PlayModeTuningValueCodec.TryWrite(property, write.Value, out var writeMessage))
                            return PlayModeTuningMutationResult.Failure(PlayModeTuningError.ApplyFailed, writeMessage + " Property: " + write.Record.propertyPath);
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    serialized.UpdateIfRequiredOrScript();
                }
                return PlayModeTuningMutationResult.Success();
            }
            catch (Exception exception)
            {
                return PlayModeTuningMutationResult.Failure(PlayModeTuningError.ApplyFailed, "The selected values could not be applied: " + exception.Message);
            }
        }

        public PlayModeTuningMutationResult MarkScenesDirty(IReadOnlyList<string> scenePaths)
        {
            try
            {
                foreach (var path in (scenePaths ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
                {
                    var scene = SceneManager.GetSceneByPath(path);
                    if (!scene.IsValid() || !scene.isLoaded)
                        return PlayModeTuningMutationResult.Failure(PlayModeTuningError.SceneDirtyFailed, "A target scene is not loaded: " + path);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!scene.isDirty)
                        return PlayModeTuningMutationResult.Failure(PlayModeTuningError.SceneDirtyFailed, "Unity did not report the target scene as dirty: " + path);
                }
                return PlayModeTuningMutationResult.Success();
            }
            catch (Exception exception)
            {
                return PlayModeTuningMutationResult.Failure(PlayModeTuningError.SceneDirtyFailed, "The target scenes could not be marked dirty: " + exception.Message);
            }
        }

        private static bool TryCreateIdentity(MonoBehaviour target, out ComponentIdentity identity, out PlayModeTuningError error, out string message)
        {
            identity = null;
            error = PlayModeTuningError.None;
            message = string.Empty;
            if (target == null || EditorUtility.IsPersistent(target) || PrefabUtility.IsPartOfAnyPrefab(target))
                return FailIdentity(PlayModeTuningError.UnsupportedTarget, "Only non-prefab MonoBehaviour instances in a saved scene are supported.", out error, out message);
            var scene = target.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path) || !scene.path.StartsWith("Assets/", StringComparison.Ordinal) || !scene.path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                return FailIdentity(PlayModeTuningError.UnsupportedTarget, "The target must be in a loaded saved scene under Assets.", out error, out message);
            var sceneGuid = AssetDatabase.AssetPathToGUID(scene.path);
            var script = MonoScript.FromMonoBehaviour(target);
            var scriptPath = script == null ? string.Empty : AssetDatabase.GetAssetPath(script);
            var scriptGuid = string.IsNullOrEmpty(scriptPath) ? string.Empty : AssetDatabase.AssetPathToGUID(scriptPath);
            if (string.IsNullOrEmpty(sceneGuid) || string.IsNullOrEmpty(scriptGuid) || !scriptPath.StartsWith("Assets/", StringComparison.Ordinal))
                return FailIdentity(PlayModeTuningError.UnsupportedTarget, "The scene and MonoScript must have stable Assets GUIDs.", out error, out message);
            var globalId = GlobalObjectId.GetGlobalObjectIdSlow(target);
            if (globalId.identifierType == 0 || globalId.targetObjectId == 0)
                return FailIdentity(PlayModeTuningError.UnsupportedTarget, "Unity did not provide a stable GlobalObjectId for the target.", out error, out message);
            var typeName = target.GetType().AssemblyQualifiedName ?? string.Empty;
            var globalIdText = globalId.ToString();
            var componentKey = PlayModeTuningFingerprint.Compute(new[] { globalIdText, sceneGuid, scene.path, scriptGuid, typeName });
            identity = new ComponentIdentity(componentKey, globalIdText, sceneGuid, scene.path, scriptGuid, typeName);
            return true;
        }

        private static bool TryResolveExact(PlayModeTuningPropertyRecord record, out MonoBehaviour target, out PlayModeTuningError error, out string message)
        {
            target = null;
            error = PlayModeTuningError.None;
            message = string.Empty;
            if (record == null || !GlobalObjectId.TryParse(record.globalObjectId, out var globalId))
                return FailIdentity(PlayModeTuningError.SessionDataInvalid, "The stored GlobalObjectId is invalid.", out error, out message);
            target = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) as MonoBehaviour;
            if (target == null)
                return FailIdentity(PlayModeTuningError.TargetMissing, "The exact target component no longer resolves.", out error, out message);
            if (!TryCreateIdentity(target, out var identity, out error, out message))
                return false;
            if (!StringComparer.Ordinal.Equals(identity.ComponentKey, record.componentKey) ||
                !StringComparer.Ordinal.Equals(identity.GlobalObjectId, record.globalObjectId) ||
                !StringComparer.Ordinal.Equals(identity.SceneGuid, record.sceneGuid) ||
                !StringComparer.Ordinal.Equals(identity.ScenePath, record.scenePath) ||
                !StringComparer.Ordinal.Equals(identity.ScriptGuid, record.scriptGuid) ||
                !StringComparer.Ordinal.Equals(identity.TypeName, record.typeName))
                return FailIdentity(PlayModeTuningError.IdentityMismatch, "The resolved target identity no longer matches the armed session.", out error, out message);
            return true;
        }

        private static bool DescriptorMatches(PlayModeTuningPropertyRecord record, SerializedProperty property)
        {
            return PlayModeTuningValueCodec.IsSupportedShape(property.propertyType, property.depth, property.isArray) &&
                StringComparer.Ordinal.Equals(record.propertyPath, property.propertyPath) &&
                StringComparer.Ordinal.Equals(record.propertyType, PlayModeTuningValueCodec.PropertyTypeName(property)) &&
                StringComparer.Ordinal.Equals(record.numericType, PlayModeTuningValueCodec.NumericTypeName(property));
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
                targetName = targetName ?? string.Empty,
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

        private static string ComputeUnselectedFingerprint(SerializedObject serialized, HashSet<string> selectedPaths)
        {
            var records = new List<string[]>();
            var iterator = serialized.GetIterator();
            if (iterator.Next(true))
            {
                do
                {
                    if (iterator.depth == 0 && !selectedPaths.Contains(iterator.propertyPath))
                    {
                        records.Add(new[]
                        {
                            iterator.propertyPath,
                            iterator.propertyType.ToString(),
                            PlayModeTuningValueCodec.NumericTypeName(iterator),
                            iterator.contentHash.ToString()
                        });
                    }
                }
                while (iterator.Next(false));
            }
            var tokens = records
                .OrderBy(item => item[0], StringComparer.Ordinal)
                .ThenBy(item => item[1], StringComparer.Ordinal)
                .ThenBy(item => item[2], StringComparer.Ordinal)
                .ThenBy(item => item[3], StringComparer.Ordinal)
                .SelectMany(item => item);
            return PlayModeTuningFingerprint.Compute(tokens);
        }

        private static bool FailIdentity(PlayModeTuningError value, string text, out PlayModeTuningError error, out string message)
        {
            error = value;
            message = text;
            return false;
        }

        private sealed class ComponentIdentity
        {
            internal ComponentIdentity(string componentKey, string globalObjectId, string sceneGuid, string scenePath, string scriptGuid, string typeName)
            {
                ComponentKey = componentKey;
                GlobalObjectId = globalObjectId;
                SceneGuid = sceneGuid;
                ScenePath = scenePath;
                ScriptGuid = scriptGuid;
                TypeName = typeName;
            }

            internal string ComponentKey { get; }
            internal string GlobalObjectId { get; }
            internal string SceneGuid { get; }
            internal string ScenePath { get; }
            internal string ScriptGuid { get; }
            internal string TypeName { get; }
        }
    }
}
