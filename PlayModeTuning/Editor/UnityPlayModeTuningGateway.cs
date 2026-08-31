using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlayModeTuning.Editor
{
    /// <summary>シーン上の動作部品（MonoBehaviour）を厳密に解決し、範囲を限定したシリアル化操作を行います。</summary>
    internal sealed class UnityPlayModeTuningGateway : IPlayModeTuningGateway
    {
        private const string UndoName = "実行中調整を反映";

        private int activeUndoGroup = -1;

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
                        return PlayModeTuningGatewayResult.Failure(PlayModeTuningError.InvalidSelection, "各選択には、シーン上の動作部品（MonoBehaviour）と項目の識別名が一つずつ必要です。");
                    var identityResult = TryCreateIdentity(target, out var identity, out var identityError, out var identityMessage);
                    if (!identityResult)
                        return PlayModeTuningGatewayResult.Failure(identityError, identityMessage);

                    var serialized = new SerializedObject(target);
                    serialized.UpdateIfRequiredOrScript();
                    var property = serialized.FindProperty(selection.PropertyPath);
                    if (property == null)
                        return PlayModeTuningGatewayResult.Failure(PlayModeTuningError.UnsupportedProperty, "選んだ項目の識別名が存在しません：" + selection.PropertyPath);
                    if (!PlayModeTuningValueCodec.TryEncode(property, out var value, out var valueError, out var valueMessage))
                        return PlayModeTuningGatewayResult.Failure(valueError, valueMessage + " 対象項目：" + selection.PropertyPath);

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
                        return PlayModeTuningGatewayResult.Failure(PlayModeTuningError.DuplicateProperty, "同じコンポーネントの同じ項目が複数回選ばれています。");
                    records.Add(record);
                }
                return Capture(records);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return PlayModeTuningGatewayResult.Failure(PlayModeTuningError.CaptureFailed, "選択内容を解決できませんでした。詳しくはコンソールを確認してください。");
            }
        }

        public PlayModeTuningGatewayResult Capture(IReadOnlyList<PlayModeTuningPropertyRecord> properties)
        {
            try
            {
                var propertySnapshots = new List<PlayModeTuningGatewayPropertySnapshot>();
                var componentSnapshots = new List<PlayModeTuningGatewayComponentSnapshot>();
                var resolvedComponents = new List<ResolvedCaptureComponent>();
                foreach (var group in PlayModeTuningIdentityOrder.OrderProperties(properties, item => item).GroupBy(item => item.componentKey, StringComparer.Ordinal))
                {
                    var orderedRecords = PlayModeTuningIdentityOrder.OrderProperties(group, item => item).ToArray();
                    var first = orderedRecords[0];
                    if (!TryResolveExact(first, out var target, out var resolveError, out var resolveMessage))
                        return PlayModeTuningGatewayResult.Failure(resolveError, resolveMessage);
                    var serialized = new SerializedObject(target);
                    serialized.UpdateIfRequiredOrScript();
                    foreach (var record in orderedRecords)
                    {
                        var property = serialized.FindProperty(record.propertyPath);
                        if (property == null)
                            return PlayModeTuningGatewayResult.Failure(PlayModeTuningError.TargetMissing, "選んだ項目が見つからなくなりました：" + record.propertyPath);
                        if (!DescriptorMatches(record, property))
                            return PlayModeTuningGatewayResult.Failure(PlayModeTuningError.IdentityMismatch, "選んだ項目の型が変わりました：" + record.propertyPath);
                        if (!PlayModeTuningValueCodec.TryEncode(property, out var value, out var valueError, out var valueMessage))
                            return PlayModeTuningGatewayResult.Failure(valueError, valueMessage + " 対象項目：" + record.propertyPath);
                        propertySnapshots.Add(new PlayModeTuningGatewayPropertySnapshot(CopyRecordWithTargetName(record, target.gameObject.name), value));
                    }
                    resolvedComponents.Add(new ResolvedCaptureComponent(target, orderedRecords));
                }
                foreach (var sceneGroup in resolvedComponents.GroupBy(item => item.Target.gameObject.scene.path, StringComparer.Ordinal))
                {
                    var scene = sceneGroup.First().Target.gameObject.scene;
                    var selectedPropertyKeys = new HashSet<string>(sceneGroup.SelectMany(item => item.Records).Select(item => ScenePropertyKey(item.globalObjectId, item.propertyPath)), StringComparer.Ordinal);
                    var sceneFingerprint = ComputeSceneUnselectedFingerprint(scene, selectedPropertyKeys);
                    foreach (var component in sceneGroup)
                        componentSnapshots.Add(new PlayModeTuningGatewayComponentSnapshot(component.Records[0].componentKey, scene.path, sceneFingerprint));
                }
                return PlayModeTuningGatewayResult.Success(new PlayModeTuningGatewaySnapshot(propertySnapshots, componentSnapshots));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return PlayModeTuningGatewayResult.Failure(PlayModeTuningError.CaptureFailed, "選んだ値を記録できませんでした。詳しくはコンソールを確認してください。");
            }
        }

        public PlayModeTuningMutationResult Apply(IReadOnlyList<PlayModeTuningWrite> writes)
        {
            if (activeUndoGroup >= 0)
                return PlayModeTuningMutationResult.Failure(PlayModeTuningError.ApplyInProgress, "別の反映処理が完了していません。");
            try
            {
                var prepared = PrepareMutations(writes, out var prepareError, out var prepareMessage);
                if (prepared == null)
                    return PlayModeTuningMutationResult.Failure(prepareError, prepareMessage);

                BeginUndoTransaction(prepared);
                foreach (var mutation in prepared)
                {
                    var serialized = new SerializedObject(mutation.Target);
                    serialized.UpdateIfRequiredOrScript();
                    foreach (var write in mutation.Writes)
                    {
                        var property = serialized.FindProperty(write.Record.propertyPath);
                        if (property == null || !DescriptorMatches(write.Record, property))
                            return PlayModeTuningMutationResult.Failure(PlayModeTuningError.IdentityMismatch, "反映直前に対象項目の型または識別情報が変わりました：" + write.Record.propertyPath);
                        if (!PlayModeTuningValueCodec.TryWrite(property, write.Value, out var writeMessage))
                            return PlayModeTuningMutationResult.Failure(PlayModeTuningError.ApplyFailed, writeMessage + " 対象項目：" + write.Record.propertyPath);
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    serialized.UpdateIfRequiredOrScript();
                }
                return PlayModeTuningMutationResult.Success();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return PlayModeTuningMutationResult.Failure(PlayModeTuningError.ApplyFailed, "選んだ値を反映できませんでした。詳しくはコンソールを確認してください。");
            }
        }

        public PlayModeTuningMutationResult CompleteApply()
        {
            if (activeUndoGroup < 0)
                return PlayModeTuningMutationResult.Failure(PlayModeTuningError.ApplyFailed, "確定できる反映処理がありません。");
            try
            {
                Undo.FlushUndoRecordObjects();
                Undo.CollapseUndoOperations(activeUndoGroup);
                return PlayModeTuningMutationResult.Success();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return PlayModeTuningMutationResult.Failure(PlayModeTuningError.ApplyFailed, "取り消し履歴を確定できませんでした。詳しくはコンソールを確認してください。");
            }
        }

        public void ReleaseApply()
        {
            ClearUndoTransaction();
        }

        public PlayModeTuningMutationResult RevertApply()
        {
            if (activeUndoGroup < 0)
                return PlayModeTuningMutationResult.Success();
            try
            {
                Undo.FlushUndoRecordObjects();
                Undo.RevertAllDownToGroup(activeUndoGroup);
                ClearUndoTransaction();
                return PlayModeTuningMutationResult.Success();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ClearUndoTransaction();
                return PlayModeTuningMutationResult.Failure(PlayModeTuningError.RollbackFailed, "反映前の状態へ戻せませんでした。詳しくはコンソールを確認してください。");
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
                        return PlayModeTuningMutationResult.Failure(PlayModeTuningError.SceneDirtyFailed, "対象シーンが読み込まれていません：" + path);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!scene.isDirty)
                        return PlayModeTuningMutationResult.Failure(PlayModeTuningError.SceneDirtyFailed, "Unityが対象シーンを変更済みとして扱いませんでした：" + path);
                }
                return PlayModeTuningMutationResult.Success();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return PlayModeTuningMutationResult.Failure(PlayModeTuningError.SceneDirtyFailed, "対象シーンを変更済みにできませんでした。詳しくはコンソールを確認してください。");
            }
        }

        private static IReadOnlyList<PreparedMutation> PrepareMutations(IReadOnlyList<PlayModeTuningWrite> writes, out PlayModeTuningError error, out string message)
        {
            var prepared = new List<PreparedMutation>();
            error = PlayModeTuningError.None;
            message = string.Empty;
            foreach (var group in PlayModeTuningIdentityOrder.OrderProperties(writes ?? Array.Empty<PlayModeTuningWrite>(), item => item.Record).GroupBy(item => item.Record.componentKey, StringComparer.Ordinal))
            {
                var orderedWrites = PlayModeTuningIdentityOrder.OrderProperties(group, item => item.Record).ToArray();
                var first = orderedWrites[0].Record;
                if (!TryResolveExact(first, out var target, out error, out message))
                    return null;
                var serialized = new SerializedObject(target);
                serialized.UpdateIfRequiredOrScript();
                foreach (var write in orderedWrites)
                {
                    var property = serialized.FindProperty(write.Record.propertyPath);
                    if (property == null || !DescriptorMatches(write.Record, property))
                    {
                        error = PlayModeTuningError.IdentityMismatch;
                        message = "反映前に対象項目の型または識別情報が変わりました：" + write.Record.propertyPath;
                        return null;
                    }
                }
                prepared.Add(new PreparedMutation(target, orderedWrites));
            }
            return prepared;
        }

        private void BeginUndoTransaction(IReadOnlyList<PreparedMutation> prepared)
        {
            Undo.IncrementCurrentGroup();
            activeUndoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            var targets = prepared
                .Select(item => item.Target.gameObject.scene)
                .Distinct()
                .SelectMany(EnumerateSceneHierarchyObjects)
                .Distinct()
                .OrderBy(item => GlobalObjectId.GetGlobalObjectIdSlow(item).ToString(), StringComparer.Ordinal)
                .ToArray();
            Undo.RecordObjects(targets, UndoName);
        }

        private void ClearUndoTransaction()
        {
            activeUndoGroup = -1;
        }

        private static bool TryCreateIdentity(MonoBehaviour target, out ComponentIdentity identity, out PlayModeTuningError error, out string message)
        {
            identity = null;
            error = PlayModeTuningError.None;
            message = string.Empty;
            if (target == null || EditorUtility.IsPersistent(target) || PrefabUtility.IsPartOfAnyPrefab(target))
                return FailIdentity(PlayModeTuningError.UnsupportedTarget, "保存済みシーン上にあり、プレハブの一部ではない動作部品（MonoBehaviour）だけに対応しています。", out error, out message);
            var scene = target.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path) || !scene.path.StartsWith("Assets/", StringComparison.Ordinal) || !scene.path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                return FailIdentity(PlayModeTuningError.UnsupportedTarget, "対象はプロジェクト内（Assets）の保存済みかつ読み込み済みのシーンに置いてください。", out error, out message);
            var sceneGuid = AssetDatabase.AssetPathToGUID(scene.path);
            var script = MonoScript.FromMonoBehaviour(target);
            var scriptPath = script == null ? string.Empty : AssetDatabase.GetAssetPath(script);
            var scriptGuid = string.IsNullOrEmpty(scriptPath) ? string.Empty : AssetDatabase.AssetPathToGUID(scriptPath);
            if (string.IsNullOrEmpty(sceneGuid) || string.IsNullOrEmpty(scriptGuid) || !IsProjectScriptPath(scriptPath))
                return FailIdentity(PlayModeTuningError.UnsupportedTarget, "シーンと動作スクリプト（MonoScript）には、プロジェクト内で安定した識別子（GUID）が必要です。", out error, out message);
            var globalId = GlobalObjectId.GetGlobalObjectIdSlow(target);
            if (globalId.identifierType == 0 || globalId.targetObjectId == 0)
                return FailIdentity(PlayModeTuningError.UnsupportedTarget, "Unityから対象の安定した大域識別子（GlobalObjectId）を取得できませんでした。", out error, out message);
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
                return FailIdentity(PlayModeTuningError.SessionDataInvalid, "保存された大域識別子（GlobalObjectId）が無効です。", out error, out message);
            target = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) as MonoBehaviour;
            if (target == null)
                return FailIdentity(PlayModeTuningError.TargetMissing, "記録した対象コンポーネントを正確に解決できません。", out error, out message);
            if (!TryCreateIdentity(target, out var identity, out error, out message))
                return false;
            if (!StringComparer.Ordinal.Equals(identity.ComponentKey, record.componentKey) ||
                !StringComparer.Ordinal.Equals(identity.GlobalObjectId, record.globalObjectId) ||
                !StringComparer.Ordinal.Equals(identity.SceneGuid, record.sceneGuid) ||
                !StringComparer.Ordinal.Equals(identity.ScenePath, record.scenePath) ||
                !StringComparer.Ordinal.Equals(identity.ScriptGuid, record.scriptGuid) ||
                !StringComparer.Ordinal.Equals(identity.TypeName, record.typeName))
                return FailIdentity(PlayModeTuningError.IdentityMismatch, "解決した対象の識別情報が、開始時の調整内容と一致しません。", out error, out message);
            return true;
        }

        private static bool DescriptorMatches(PlayModeTuningPropertyRecord record, SerializedProperty property)
        {
            return PlayModeTuningValueCodec.IsSupportedShape(property.propertyType, property.depth, property.isArray) &&
                StringComparer.Ordinal.Equals(record.propertyPath, property.propertyPath) &&
                StringComparer.Ordinal.Equals(record.propertyType, PlayModeTuningValueCodec.PropertyTypeName(property)) &&
                StringComparer.Ordinal.Equals(record.numericType, PlayModeTuningValueCodec.NumericTypeName(property));
        }

        private static bool IsProjectScriptPath(string path)
        {
            return !string.IsNullOrEmpty(path) && (path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Packages/", StringComparison.Ordinal));
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

        private static string ComputeSceneUnselectedFingerprint(Scene scene, HashSet<string> selectedPropertyKeys)
        {
            var records = new List<string[]>();
            foreach (var root in scene.GetRootGameObjects().OrderBy(item => GlobalObjectId.GetGlobalObjectIdSlow(item).ToString(), StringComparer.Ordinal))
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true).OrderBy(item => GlobalObjectId.GetGlobalObjectIdSlow(item.gameObject).ToString(), StringComparer.Ordinal))
                {
                    AddObjectFingerprintRecords(transform.gameObject, selectedPropertyKeys, records);
                    var components = transform.gameObject.GetComponents<Component>();
                    for (var index = 0; index < components.Length; index++)
                    {
                        var component = components[index];
                        if (component == null)
                        {
                            records.Add(new[] { GlobalObjectId.GetGlobalObjectIdSlow(transform.gameObject).ToString(), "欠落したスクリプト", index.ToString() });
                            continue;
                        }
                        AddObjectFingerprintRecords(component, selectedPropertyKeys, records);
                    }
                }
            }
            var tokens = records
                .OrderBy(item => item[0], StringComparer.Ordinal)
                .ThenBy(item => item[1], StringComparer.Ordinal)
                .ThenBy(item => item[2], StringComparer.Ordinal)
                .ThenBy(item => item.Length > 3 ? item[3] : string.Empty, StringComparer.Ordinal)
                .ThenBy(item => item.Length > 4 ? item[4] : string.Empty, StringComparer.Ordinal)
                .ThenBy(item => item.Length > 5 ? item[5] : string.Empty, StringComparer.Ordinal)
                .SelectMany(item => item);
            return PlayModeTuningFingerprint.Compute(tokens);
        }

        private static IEnumerable<UnityEngine.Object> EnumerateSceneHierarchyObjects(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects().OrderBy(item => GlobalObjectId.GetGlobalObjectIdSlow(item).ToString(), StringComparer.Ordinal))
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true).OrderBy(item => GlobalObjectId.GetGlobalObjectIdSlow(item.gameObject).ToString(), StringComparer.Ordinal))
                {
                    yield return transform.gameObject;
                    foreach (var component in transform.gameObject.GetComponents<Component>())
                    {
                        if (component != null)
                            yield return component;
                    }
                }
            }
        }

        private static void AddObjectFingerprintRecords(UnityEngine.Object target, HashSet<string> selectedPropertyKeys, List<string[]> records)
        {
            var globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
            var typeName = target.GetType().AssemblyQualifiedName ?? string.Empty;
            records.Add(new[] { globalObjectId, typeName, "対象" });
            var serialized = new SerializedObject(target);
            serialized.UpdateIfRequiredOrScript();
            var iterator = serialized.GetIterator();
            if (!iterator.Next(true))
                return;
            do
            {
                if (iterator.depth != 0 || selectedPropertyKeys.Contains(ScenePropertyKey(globalObjectId, iterator.propertyPath)))
                    continue;
                records.Add(new[]
                {
                    globalObjectId,
                    typeName,
                    iterator.propertyPath,
                    iterator.propertyType.ToString(),
                    PlayModeTuningValueCodec.NumericTypeName(iterator),
                    iterator.contentHash.ToString()
                });
            }
            while (iterator.Next(false));
        }

        private static string ScenePropertyKey(string globalObjectId, string propertyPath)
        {
            return (globalObjectId ?? string.Empty) + "\n" + (propertyPath ?? string.Empty);
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

        private sealed class PreparedMutation
        {
            internal PreparedMutation(MonoBehaviour target, IReadOnlyList<PlayModeTuningWrite> writes)
            {
                Target = target;
                Writes = writes;
            }

            internal MonoBehaviour Target { get; }
            internal IReadOnlyList<PlayModeTuningWrite> Writes { get; }
        }

        private sealed class ResolvedCaptureComponent
        {
            internal ResolvedCaptureComponent(MonoBehaviour target, IReadOnlyList<PlayModeTuningPropertyRecord> records)
            {
                Target = target;
                Records = records;
            }

            internal MonoBehaviour Target { get; }
            internal IReadOnlyList<PlayModeTuningPropertyRecord> Records { get; }
        }
    }
}
