// SPDX-License-Identifier: MIT

using UnityEditor;

namespace BuildGuard.Editor
{
    /// <summary>
    /// Describes one read-only structural override on a connected Scene Prefab instance.
    /// </summary>
    internal readonly struct BuildGuardPrefabOverrideFinding
    {
        internal BuildGuardPrefabOverrideFinding(
            BuildGuardPrefabOverrideKind kind,
            string scenePath,
            string sceneGuid,
            string prefabAssetPath,
            string prefabAssetGuid,
            PrefabAssetType prefabAssetType,
            string nearestPrefabAssetPath,
            PrefabAssetType nearestPrefabAssetType,
            bool isNestedPrefabObject,
            string instanceRootHierarchyPath,
            string targetHierarchyPath,
            string sourceObjectPath,
            string componentTypeName,
            int componentIndex,
            string instanceRootGlobalObjectId,
            string navigationTargetGlobalObjectId,
            string sourceObjectGlobalObjectId)
        {
            Kind = kind;
            ScenePath = scenePath ?? string.Empty;
            SceneGuid = sceneGuid ?? string.Empty;
            PrefabAssetPath = prefabAssetPath ?? string.Empty;
            PrefabAssetGuid = prefabAssetGuid ?? string.Empty;
            PrefabAssetType = prefabAssetType;
            NearestPrefabAssetPath = nearestPrefabAssetPath ?? string.Empty;
            NearestPrefabAssetType = nearestPrefabAssetType;
            IsNestedPrefabObject = isNestedPrefabObject;
            InstanceRootHierarchyPath = instanceRootHierarchyPath ?? string.Empty;
            TargetHierarchyPath = targetHierarchyPath ?? string.Empty;
            SourceObjectPath = sourceObjectPath ?? string.Empty;
            ComponentTypeName = componentTypeName ?? string.Empty;
            ComponentIndex = componentIndex;
            InstanceRootGlobalObjectId = instanceRootGlobalObjectId ?? string.Empty;
            NavigationTargetGlobalObjectId = navigationTargetGlobalObjectId ?? string.Empty;
            SourceObjectGlobalObjectId = sourceObjectGlobalObjectId ?? string.Empty;
        }

        internal BuildGuardPrefabOverrideKind Kind { get; }

        internal string ScenePath { get; }

        internal string SceneGuid { get; }

        internal string PrefabAssetPath { get; }

        internal string PrefabAssetGuid { get; }

        internal PrefabAssetType PrefabAssetType { get; }

        internal string NearestPrefabAssetPath { get; }

        internal PrefabAssetType NearestPrefabAssetType { get; }

        internal bool IsNestedPrefabObject { get; }

        internal string InstanceRootHierarchyPath { get; }

        internal string TargetHierarchyPath { get; }

        internal string SourceObjectPath { get; }

        internal string ComponentTypeName { get; }

        internal int ComponentIndex { get; }

        internal string InstanceRootGlobalObjectId { get; }

        internal string NavigationTargetGlobalObjectId { get; }

        internal string SourceObjectGlobalObjectId { get; }
    }
}
