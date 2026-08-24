// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildGuard.Editor
{
    /// <summary>
    /// Scans connected outermost Scene Prefab instances for structural overrides without modifying them.
    /// </summary>
    internal static class BuildGuardPrefabOverrideSceneScanner
    {
        /// <summary>Scans one loaded Scene with production limits.</summary>
        internal static BuildGuardPrefabOverrideScanResult Scan(Scene scene)
        {
            return Scan(scene, BuildGuardPrefabOverrideScanLimits.Default);
        }

        /// <summary>Scans one loaded Scene with injectable limits used by deterministic tests.</summary>
        internal static BuildGuardPrefabOverrideScanResult Scan(
            Scene scene,
            BuildGuardPrefabOverrideScanLimits limits)
        {
            if (!scene.IsValid())
            {
                return BuildGuardPrefabOverrideScanResult.Failure(
                    BuildGuardPrefabOverrideScanError.InvalidScene,
                    "The Scene to scan is invalid.",
                    0,
                    0);
            }

            if (!scene.isLoaded)
            {
                return BuildGuardPrefabOverrideScanResult.Failure(
                    BuildGuardPrefabOverrideScanError.SceneNotLoaded,
                    "The Scene to scan is not loaded.",
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
                return BuildGuardPrefabOverrideScanResult.Failure(
                    BuildGuardPrefabOverrideScanError.UnityApiFailure,
                    $"Unity could not inspect the Scene Prefab overrides: {exception.Message}",
                    state.VisitedGameObjectCount,
                    state.ScannedPrefabInstanceCount);
            }
        }

        /// <summary>Classifies the source state before any override data is accepted.</summary>
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

        /// <summary>Compares findings by stable display fields without depending on Unity enumeration order.</summary>
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
                    $"Prefab object {BuildGuardHierarchyPath.Create(instanceRoot.transform)} has unsupported status {instanceStatus}.");
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
                    ? $"Prefab source is unavailable for {BuildGuardHierarchyPath.Create(instanceRoot.transform)}."
                    : $"Prefab instance {BuildGuardHierarchyPath.Create(instanceRoot.transform)} has unsupported status {instanceStatus}.";
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
                    $"Unity returned no added-component collection for {context.InstanceRootHierarchyPath}.");
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
                        $"Unity returned an unattached added component for {context.InstanceRootHierarchyPath}.");
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
                    $"Unity returned no removed-component collection for {context.InstanceRootHierarchyPath}.");
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
                        $"Unity returned an unattached removed component for {context.InstanceRootHierarchyPath}.");
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
                    $"Unity returned no added-GameObject collection for {BuildGuardHierarchyPath.Create(instanceRoot.transform)}.");
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
                        $"Unity returned an invalid added GameObject for {BuildGuardHierarchyPath.Create(instanceRoot.transform)}.");
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
                    $"Unity returned no removed-GameObject collection for {BuildGuardHierarchyPath.Create(instanceRoot.transform)}.");
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
                        $"Unity returned an unresolved removed GameObject for {BuildGuardHierarchyPath.Create(instanceRoot.transform)}.");
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
                        $"Scene contains more than {Limits.MaxVisitedGameObjects} GameObjects.");
                }
            }

            internal void VisitPrefabInstance()
            {
                ScannedPrefabInstanceCount++;
                if (ScannedPrefabInstanceCount > Limits.MaxPrefabInstances)
                {
                    throw new ScanFailedException(
                        BuildGuardPrefabOverrideScanError.TooManyPrefabInstances,
                        $"Scene contains more than {Limits.MaxPrefabInstances} outermost Prefab instances.");
                }
            }

            internal void AppendFinding(BuildGuardPrefabOverrideFinding finding)
            {
                if (Findings.Count >= Limits.MaxFindings)
                {
                    throw new ScanFailedException(
                        BuildGuardPrefabOverrideScanError.TooManyFindings,
                        $"Scene contains more than {Limits.MaxFindings} structural Prefab overrides.");
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
