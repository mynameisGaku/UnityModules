// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildGuard.Editor
{
    /// <summary>
    /// 接続済みの最上位プレハブ実体を変更せず、その構造差分を検査します。
    /// </summary>
    internal static class BuildGuardPrefabOverrideSceneScanner
    {
        /// <summary>製品用の上限を使って、読込済みシーン1件を検査します。</summary>
        internal static BuildGuardPrefabOverrideScanResult Scan(Scene scene)
        {
            return Scan(scene, BuildGuardPrefabOverrideScanLimits.Default);
        }

        /// <summary>決定論的な試験で差し替えられる上限を使い、読込済みシーン1件を検査します。</summary>
        internal static BuildGuardPrefabOverrideScanResult Scan(
            Scene scene,
            BuildGuardPrefabOverrideScanLimits limits)
        {
            if (!scene.IsValid())
            {
                return BuildGuardPrefabOverrideScanResult.Failure(
                    BuildGuardPrefabOverrideScanError.InvalidScene,
                    "検査対象のシーンが無効です。",
                    0,
                    0);
            }

            if (!scene.isLoaded)
            {
                return BuildGuardPrefabOverrideScanResult.Failure(
                    BuildGuardPrefabOverrideScanError.SceneNotLoaded,
                    "検査対象のシーンが読み込まれていません。",
                    0,
                    0);
            }

            if (!limits.TryValidate(out var limitError))
            {
                return BuildGuardPrefabOverrideScanResult.Failure(
                    BuildGuardPrefabOverrideScanError.InvalidLimits,
                    limitError,
                    0,
                    0);
            }

            var state = new ScanState(limits);
            try
            {
                ScanHierarchy(scene, state);
                state.Findings.Sort(CompareFindings);
                return BuildGuardPrefabOverrideScanResult.Success(
                    state.Findings,
                    state.VisitedGameObjectCount,
                    state.ScannedPrefabInstanceCount);
            }
            catch (ScanFailedException exception)
            {
                return BuildGuardPrefabOverrideScanResult.Failure(
                    exception.Error,
                    exception.Message,
                    state.VisitedGameObjectCount,
                    state.ScannedPrefabInstanceCount);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return BuildGuardPrefabOverrideScanResult.Failure(
                    BuildGuardPrefabOverrideScanError.UnityApiFailure,
                    "プレハブ構造差分を検査できませんでした。Unityのログで原因を確認してください。",
                    state.VisitedGameObjectCount,
                    state.ScannedPrefabInstanceCount);
            }
        }

        /// <summary>構造差分を受け付ける前に、プレハブ参照元の状態を分類します。</summary>
        internal static BuildGuardPrefabOverrideScanError ClassifyPrefabSource(
            PrefabInstanceStatus instanceStatus,
            PrefabAssetType assetType,
            string prefabAssetPath)
        {
            if (instanceStatus != PrefabInstanceStatus.Connected)
            {
                return BuildGuardPrefabOverrideScanError.UnsupportedPrefabInstanceStatus;
            }

            return assetType == PrefabAssetType.MissingAsset
                || assetType == PrefabAssetType.NotAPrefab
                || string.IsNullOrEmpty(prefabAssetPath)
                ? BuildGuardPrefabOverrideScanError.MissingPrefabSource
                : BuildGuardPrefabOverrideScanError.None;
        }

        /// <summary>Unityの列挙順に依存せず、表示用の安定項目で構造差分を比較します。</summary>
        internal static int CompareFindings(
            BuildGuardPrefabOverrideFinding left,
            BuildGuardPrefabOverrideFinding right)
        {
            var comparison = string.CompareOrdinal(left.ScenePath, right.ScenePath);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = string.CompareOrdinal(left.InstanceRootHierarchyPath, right.InstanceRootHierarchyPath);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.Kind.CompareTo(right.Kind);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = string.CompareOrdinal(left.TargetHierarchyPath, right.TargetHierarchyPath);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = string.CompareOrdinal(left.SourceObjectPath, right.SourceObjectPath);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = string.CompareOrdinal(left.ComponentTypeName, right.ComponentTypeName);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.ComponentIndex.CompareTo(right.ComponentIndex);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = string.CompareOrdinal(left.NavigationTargetGlobalObjectId, right.NavigationTargetGlobalObjectId);
            return comparison != 0
                ? comparison
                : string.CompareOrdinal(left.SourceObjectGlobalObjectId, right.SourceObjectGlobalObjectId);
        }

        private static void ScanHierarchy(Scene scene, ScanState state)
        {
            var roots = BuildGuardHierarchyPath.GetSortedRoots(scene);
            var pending = new Stack<GameObject>(roots.Length);
            for (var rootIndex = roots.Length - 1; rootIndex >= 0; rootIndex--)
            {
                pending.Push(roots[rootIndex]);
            }

            while (pending.Count > 0)
            {
                var current = pending.Pop();
                state.VisitGameObject();
                ScanPrefabInstanceRoot(scene, current, state);

                for (var childIndex = current.transform.childCount - 1; childIndex >= 0; childIndex--)
                {
                    pending.Push(current.transform.GetChild(childIndex).gameObject);
                }
            }
        }

        private static void ScanPrefabInstanceRoot(Scene scene, GameObject instanceRoot, ScanState state)
        {
            var instanceStatus = PrefabUtility.GetPrefabInstanceStatus(instanceRoot);
            if (instanceStatus != PrefabInstanceStatus.Connected
                && instanceStatus != PrefabInstanceStatus.NotAPrefab)
            {
                throw new ScanFailedException(
                    BuildGuardPrefabOverrideScanError.UnsupportedPrefabInstanceStatus,
                    $"プレハブオブジェクト {BuildGuardHierarchyPath.Create(instanceRoot.transform)} の状態には対応していません（状態値: {(int)instanceStatus}）。");
            }

            if (!PrefabUtility.IsOutermostPrefabInstanceRoot(instanceRoot))
            {
                return;
            }

            if (instanceStatus == PrefabInstanceStatus.Connected
                && !PrefabUtility.IsPartOfNonAssetPrefabInstance(instanceRoot))
            {
                return;
            }

            state.VisitPrefabInstance();
            var prefabAssetPath = NormalizeAssetPath(
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot));
            var prefabAssetType = PrefabUtility.GetPrefabAssetType(instanceRoot);
            var sourceError = ClassifyPrefabSource(instanceStatus, prefabAssetType, prefabAssetPath);
            if (sourceError != BuildGuardPrefabOverrideScanError.None)
            {
                var message = sourceError == BuildGuardPrefabOverrideScanError.MissingPrefabSource
                    ? $"プレハブ参照元を利用できません: {BuildGuardHierarchyPath.Create(instanceRoot.transform)}"
                    : $"プレハブ実体 {BuildGuardHierarchyPath.Create(instanceRoot.transform)} の状態には対応していません（状態値: {(int)instanceStatus}）。";
                throw new ScanFailedException(sourceError, message);
            }

            var context = new InstanceContext(scene, instanceRoot, prefabAssetPath, prefabAssetType);
            AppendInstanceFindings(instanceRoot, context, state);
        }

        private static void AppendInstanceFindings(
            GameObject instanceRoot,
            InstanceContext context,
            ScanState state)
        {
            var addedGameObjects = AppendAddedGameObjectFindings(instanceRoot, context, state);
            var removedGameObjects = AppendRemovedGameObjectFindings(instanceRoot, context, state);
            AppendAddedComponentFindings(instanceRoot, context, addedGameObjects, state);
            AppendRemovedComponentFindings(instanceRoot, context, removedGameObjects, state);
        }

        private static void AppendAddedComponentFindings(
            GameObject instanceRoot,
            InstanceContext context,
            ISet<GameObject> addedGameObjects,
            ScanState state)
        {
            var overrides = PrefabUtility.GetAddedComponents(instanceRoot);
            if (overrides == null)
            {
                throw new ScanFailedException(
                    BuildGuardPrefabOverrideScanError.UnityApiFailure,
                    $"Unityから追加コンポーネントの一覧を取得できませんでした: {context.InstanceRootHierarchyPath}");
            }

            for (var index = 0; index < overrides.Count; index++)
            {
                var component = overrides[index]?.instanceComponent;
                if (component == null || IsSelfOrDescendantOfAny(component.gameObject, addedGameObjects))
                {
                    continue;
                }

                var componentIndex = FindComponentIndex(component);
                if (componentIndex < 0)
                {
                    throw new ScanFailedException(
                        BuildGuardPrefabOverrideScanError.UnityApiFailure,
                        $"Unityが、ゲームオブジェクトに属さない追加コンポーネントを返しました: {context.InstanceRootHierarchyPath}");
                }

                state.AppendFinding(CreateFinding(
                    context,
                    BuildGuardPrefabOverrideKind.AddedComponent,
                    component.gameObject,
                    BuildGuardHierarchyPath.Create(component.transform),
                    null,
                    string.Empty,
                    GetComponentTypeName(component),
                    componentIndex));
            }
        }

        private static void AppendRemovedComponentFindings(
            GameObject instanceRoot,
            InstanceContext context,
            RemovedGameObjectIndex removedGameObjects,
            ScanState state)
        {
            var overrides = PrefabUtility.GetRemovedComponents(instanceRoot);
            if (overrides == null)
            {
                throw new ScanFailedException(
                    BuildGuardPrefabOverrideScanError.UnityApiFailure,
                    $"Unityから削除コンポーネントの一覧を取得できませんでした: {context.InstanceRootHierarchyPath}");
            }

            for (var index = 0; index < overrides.Count; index++)
            {
                var removed = overrides[index];
                var component = removed?.assetComponent;
                var containingGameObject = removed?.containingInstanceGameObject;
                if (component == null
                    || containingGameObject == null
                    || removedGameObjects.ContainsSelfOrAncestor(component.gameObject, containingGameObject))
                {
                    continue;
                }

                var componentIndex = FindComponentIndex(component);
                if (componentIndex < 0)
                {
                    throw new ScanFailedException(
                        BuildGuardPrefabOverrideScanError.UnityApiFailure,
                        $"Unityが、ゲームオブジェクトに属さない削除コンポーネントを返しました: {context.InstanceRootHierarchyPath}");
                }

                state.AppendFinding(CreateFinding(
                    context,
                    BuildGuardPrefabOverrideKind.RemovedComponent,
                    containingGameObject,
                    BuildGuardHierarchyPath.Create(containingGameObject.transform),
                    component,
                    BuildGuardHierarchyPath.Create(component.transform),
                    GetComponentTypeName(component),
                    componentIndex));
            }
        }

        private static HashSet<GameObject> AppendAddedGameObjectFindings(
            GameObject instanceRoot,
            InstanceContext context,
            ScanState state)
        {
            var overrides = PrefabUtility.GetAddedGameObjects(instanceRoot);
            if (overrides == null)
            {
                throw new ScanFailedException(
                    BuildGuardPrefabOverrideScanError.UnityApiFailure,
                    $"Unityから追加ゲームオブジェクトの一覧を取得できませんでした: {BuildGuardHierarchyPath.Create(instanceRoot.transform)}");
            }

            var candidates = new List<GameObject>(overrides.Count);
            var candidateSet = new HashSet<GameObject>();
            for (var index = 0; index < overrides.Count; index++)
            {
                var candidate = overrides[index]?.instanceGameObject;
                if (candidate == null)
                {
                    throw new ScanFailedException(
                        BuildGuardPrefabOverrideScanError.UnityApiFailure,
                        $"Unityが無効な追加ゲームオブジェクトを返しました: {BuildGuardHierarchyPath.Create(instanceRoot.transform)}");
                }

                candidates.Add(candidate);
                candidateSet.Add(candidate);
            }

            var canonical = new HashSet<GameObject>();
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (HasTransformInSet(candidate.transform.parent, candidateSet)
                    || !canonical.Add(candidate))
                {
                    continue;
                }

                state.AppendFinding(CreateFinding(
                    context,
                    BuildGuardPrefabOverrideKind.AddedGameObject,
                    candidate,
                    BuildGuardHierarchyPath.Create(candidate.transform),
                    null,
                    string.Empty,
                    string.Empty,
                    -1));
            }

            return canonical;
        }

        private static RemovedGameObjectIndex AppendRemovedGameObjectFindings(
            GameObject instanceRoot,
            InstanceContext context,
            ScanState state)
        {
            var overrides = PrefabUtility.GetRemovedGameObjects(instanceRoot);
            if (overrides == null)
            {
                throw new ScanFailedException(
                    BuildGuardPrefabOverrideScanError.UnityApiFailure,
                    $"Unityから削除ゲームオブジェクトの一覧を取得できませんでした: {BuildGuardHierarchyPath.Create(instanceRoot.transform)}");
            }

            var candidates = new List<RemovedGameObjectEntry>(overrides.Count);
            var candidateIndex = new RemovedGameObjectIndex();
            for (var index = 0; index < overrides.Count; index++)
            {
                var removed = overrides[index];
                if (removed?.assetGameObject == null || removed.parentOfRemovedGameObjectInInstance == null)
                {
                    throw new ScanFailedException(
                        BuildGuardPrefabOverrideScanError.MissingPrefabSource,
                        $"Unityが参照元を解決できない削除ゲームオブジェクトを返しました: {BuildGuardHierarchyPath.Create(instanceRoot.transform)}");
                }

                var candidate = new RemovedGameObjectEntry(
                    removed.assetGameObject,
                    removed.parentOfRemovedGameObjectInInstance);
                candidates.Add(candidate);
                candidateIndex.Add(candidate);
            }

            var canonical = new RemovedGameObjectIndex();
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (candidateIndex.ContainsAncestor(
                        candidate.AssetGameObject,
                        candidate.ContextInstanceRoot)
                    || !canonical.Add(candidate))
                {
                    continue;
                }

                state.AppendFinding(CreateFinding(
                    context,
                    BuildGuardPrefabOverrideKind.RemovedGameObject,
                    candidate.ParentInstanceGameObject,
                    BuildGuardHierarchyPath.Create(candidate.ParentInstanceGameObject.transform),
                    candidate.AssetGameObject,
                    BuildGuardHierarchyPath.Create(candidate.AssetGameObject.transform),
                    string.Empty,
                    -1));
            }

            return canonical;
        }

        private static BuildGuardPrefabOverrideFinding CreateFinding(
            InstanceContext context,
            BuildGuardPrefabOverrideKind kind,
            GameObject navigationTarget,
            string targetHierarchyPath,
            UnityEngine.Object sourceObject,
            string sourceObjectPath,
            string componentTypeName,
            int componentIndex)
        {
            var nearestPrefabAssetPath = NormalizeAssetPath(
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(navigationTarget));
            var nearestPrefabAssetType = PrefabUtility.GetPrefabAssetType(navigationTarget);
            if (string.IsNullOrEmpty(nearestPrefabAssetPath))
            {
                nearestPrefabAssetPath = context.PrefabAssetPath;
                nearestPrefabAssetType = context.PrefabAssetType;
            }

            return new BuildGuardPrefabOverrideFinding(
                kind,
                context.ScenePath,
                context.SceneGuid,
                context.PrefabAssetPath,
                context.PrefabAssetGuid,
                context.PrefabAssetType,
                nearestPrefabAssetPath,
                nearestPrefabAssetType,
                !string.Equals(nearestPrefabAssetPath, context.PrefabAssetPath, StringComparison.Ordinal),
                context.InstanceRootHierarchyPath,
                targetHierarchyPath,
                sourceObjectPath,
                componentTypeName,
                componentIndex,
                context.InstanceRootGlobalObjectId,
                GetGlobalObjectId(navigationTarget),
                GetGlobalObjectId(sourceObject));
        }

        private static bool IsSelfOrDescendantOfAny(GameObject candidate, ISet<GameObject> roots)
        {
            return candidate != null && HasTransformInSet(candidate.transform, roots);
        }

        private static bool HasTransformInSet(Transform candidate, ISet<GameObject> values)
        {
            for (var current = candidate; current != null; current = current.parent)
            {
                if (values.Contains(current.gameObject))
                {
                    return true;
                }
            }

            return false;
        }

        private static int FindComponentIndex(Component component)
        {
            var components = component.gameObject.GetComponents<Component>();
            for (var index = 0; index < components.Length; index++)
            {
                if (components[index] == component)
                {
                    return index;
                }
            }

            return -1;
        }

        private static string GetComponentTypeName(Component component)
        {
            return component.GetType().FullName ?? component.GetType().Name;
        }

        private static string GetGlobalObjectId(UnityEngine.Object value)
        {
            return value == null
                ? string.Empty
                : GlobalObjectId.GetGlobalObjectIdSlow(value).ToString();
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        private sealed class ScanState
        {
            internal ScanState(BuildGuardPrefabOverrideScanLimits limits)
            {
                Limits = limits;
                Findings = new List<BuildGuardPrefabOverrideFinding>();
            }

            internal BuildGuardPrefabOverrideScanLimits Limits { get; }

            internal List<BuildGuardPrefabOverrideFinding> Findings { get; }

            internal int VisitedGameObjectCount { get; private set; }

            internal int ScannedPrefabInstanceCount { get; private set; }

            internal void VisitGameObject()
            {
                VisitedGameObjectCount++;
                if (VisitedGameObjectCount > Limits.MaxVisitedGameObjects)
                {
                    throw new ScanFailedException(
                        BuildGuardPrefabOverrideScanError.TooManyGameObjects,
                        $"シーン内のゲームオブジェクトが検査上限{Limits.MaxVisitedGameObjects}件を超えています。");
                }
            }

            internal void VisitPrefabInstance()
            {
                ScannedPrefabInstanceCount++;
                if (ScannedPrefabInstanceCount > Limits.MaxPrefabInstances)
                {
                    throw new ScanFailedException(
                        BuildGuardPrefabOverrideScanError.TooManyPrefabInstances,
                        $"シーン内の最上位プレハブ実体が検査上限{Limits.MaxPrefabInstances}件を超えています。");
                }
            }

            internal void AppendFinding(BuildGuardPrefabOverrideFinding finding)
            {
                if (Findings.Count >= Limits.MaxFindings)
                {
                    throw new ScanFailedException(
                        BuildGuardPrefabOverrideScanError.TooManyFindings,
                        $"シーン内のプレハブ構造差分が検査上限{Limits.MaxFindings}件を超えています。");
                }

                Findings.Add(finding);
            }
        }

        private readonly struct InstanceContext
        {
            internal InstanceContext(
                Scene scene,
                GameObject instanceRoot,
                string prefabAssetPath,
                PrefabAssetType prefabAssetType)
            {
                ScenePath = NormalizeAssetPath(scene.path);
                SceneGuid = AssetDatabase.AssetPathToGUID(ScenePath);
                PrefabAssetPath = prefabAssetPath;
                PrefabAssetGuid = AssetDatabase.AssetPathToGUID(prefabAssetPath);
                PrefabAssetType = prefabAssetType;
                InstanceRootHierarchyPath = BuildGuardHierarchyPath.Create(instanceRoot.transform);
                InstanceRootGlobalObjectId = GetGlobalObjectId(instanceRoot);
            }

            internal string ScenePath { get; }

            internal string SceneGuid { get; }

            internal string PrefabAssetPath { get; }

            internal string PrefabAssetGuid { get; }

            internal PrefabAssetType PrefabAssetType { get; }

            internal string InstanceRootHierarchyPath { get; }

            internal string InstanceRootGlobalObjectId { get; }
        }

        private sealed class RemovedGameObjectIndex
        {
            private readonly Dictionary<GameObject, HashSet<GameObject>> _rootsByContext =
                new Dictionary<GameObject, HashSet<GameObject>>();

            internal bool Add(RemovedGameObjectEntry entry)
            {
                if (!_rootsByContext.TryGetValue(entry.ContextInstanceRoot, out var roots))
                {
                    roots = new HashSet<GameObject>();
                    _rootsByContext.Add(entry.ContextInstanceRoot, roots);
                }

                return roots.Add(entry.AssetGameObject);
            }

            internal bool ContainsAncestor(GameObject candidate, GameObject contextInstanceRoot)
            {
                return candidate != null
                    && _rootsByContext.TryGetValue(contextInstanceRoot, out var roots)
                    && HasTransformInSet(candidate.transform.parent, roots);
            }

            internal bool ContainsSelfOrAncestor(GameObject candidate, GameObject candidateContextObject)
            {
                if (candidate == null || candidateContextObject == null)
                {
                    return false;
                }

                var contextInstanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(candidateContextObject)
                    ?? candidateContextObject;
                return _rootsByContext.TryGetValue(contextInstanceRoot, out var roots)
                    && HasTransformInSet(candidate.transform, roots);
            }
        }

        private sealed class RemovedGameObjectEntry
        {
            internal RemovedGameObjectEntry(
                GameObject assetGameObject,
                GameObject parentInstanceGameObject)
            {
                AssetGameObject = assetGameObject;
                ParentInstanceGameObject = parentInstanceGameObject;
                ContextInstanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(parentInstanceGameObject)
                    ?? parentInstanceGameObject;
            }

            internal GameObject AssetGameObject { get; }

            internal GameObject ParentInstanceGameObject { get; }

            internal GameObject ContextInstanceRoot { get; }
        }

        private sealed class ScanFailedException : Exception
        {
            internal ScanFailedException(BuildGuardPrefabOverrideScanError error, string message)
                : base(message)
            {
                Error = error;
            }

            internal BuildGuardPrefabOverrideScanError Error { get; }
        }
    }
}
