// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectSetup.Editor
{
    [Serializable]
    internal readonly struct ProjectSetupSnapshot : IEquatable<ProjectSetupSnapshot>
    {
        internal ProjectSetupSnapshot(
            SerializationMode assetSerialization,
            string versionControlMode,
            bool enterPlayModeOptionsEnabled,
            EnterPlayModeOptions enterPlayModeOptions,
            ColorSpace colorSpace,
            bool runInBackground,
            string companyName,
            string productName,
            string bundleVersion,
            bool hasTagManagerData = false,
            string[] tags = null,
            string[] customTags = null,
            string[] layers = null,
            ProjectSetupSortingLayer[] sortingLayers = null,
            string tagManagerFileText = null,
            bool hasBuildSceneData = false,
            string buildSceneTargetId = null,
            string buildSceneTargetLabel = null,
            ProjectSetupBuildSceneState[] buildScenes = null,
            bool hasPlayModeStartSceneData = false,
            string playModeStartSceneGuid = null,
            string playModeStartScenePath = null,
            bool hasScriptingDefineData = false,
            string scriptingDefineTargetId = null,
            string scriptingDefineTargetLabel = null,
            string[] scriptingDefineSymbols = null,
            bool hasCodeGenerationData = false,
            string rootNamespace = null,
            LineEndingsMode newScriptLineEndings = LineEndingsMode.OSNative,
            bool hasNamingData = false,
            EditorSettings.NamingScheme gameObjectNamingScheme = EditorSettings.NamingScheme.SpaceParenthesis,
            int gameObjectNamingDigits = 1,
            bool assetNamingUsesSpace = true,
            string[] projectFolders = null,
            string[] projectAssetPaths = null,
            string[] createdProjectFolders = null,
            ProjectSetupCreatedAsset[] createdProjectAssets = null,
            string[] projectRootFilePaths = null,
            ProjectSetupCreatedRootFile[] createdProjectRootFiles = null,
            bool hasApplicationIdentifierData = false,
            string applicationIdentifierTargetId = null,
            string applicationIdentifierTargetLabel = null,
            string applicationIdentifier = null,
            bool hasScriptingBackendData = false,
            string scriptingBackendTargetId = null,
            string scriptingBackendTargetLabel = null,
            ScriptingImplementation scriptingBackend = ScriptingImplementation.Mono2x,
            bool hasApiCompatibilityLevelData = false,
            string apiCompatibilityLevelTargetId = null,
            string apiCompatibilityLevelTargetLabel = null,
            ApiCompatibilityLevel apiCompatibilityLevel = ApiCompatibilityLevel.NET_Standard,
            bool hasManagedStrippingLevelData = false,
            string managedStrippingLevelTargetId = null,
            string managedStrippingLevelTargetLabel = null,
            ManagedStrippingLevel managedStrippingLevel = ManagedStrippingLevel.Minimal)
        {
            AssetSerialization = assetSerialization;
            VersionControlMode = versionControlMode ?? string.Empty;
            EnterPlayModeOptionsEnabled = enterPlayModeOptionsEnabled;
            EnterPlayModeOptions = enterPlayModeOptions;
            ColorSpace = colorSpace;
            RunInBackground = runInBackground;
            CompanyName = companyName ?? string.Empty;
            ProductName = productName ?? string.Empty;
            BundleVersion = bundleVersion ?? string.Empty;
            HasTagManagerData = hasTagManagerData;
            Tags = Clone(tags);
            CustomTags = Clone(customTags);
            Layers = Clone(layers);
            SortingLayers = Clone(sortingLayers);
            TagManagerFileText = tagManagerFileText ?? string.Empty;
            HasBuildSceneData = hasBuildSceneData;
            BuildSceneTargetId = buildSceneTargetId ?? string.Empty;
            BuildSceneTargetLabel = buildSceneTargetLabel ?? string.Empty;
            BuildScenes = Clone(buildScenes);
            HasPlayModeStartSceneData = hasPlayModeStartSceneData;
            PlayModeStartSceneGuid = playModeStartSceneGuid ?? string.Empty;
            PlayModeStartScenePath = NormalizePath(playModeStartScenePath);
            HasScriptingDefineData = hasScriptingDefineData;
            ScriptingDefineTargetId = scriptingDefineTargetId ?? string.Empty;
            ScriptingDefineTargetLabel = scriptingDefineTargetLabel ?? string.Empty;
            ScriptingDefineSymbols = Clone(scriptingDefineSymbols);
            HasCodeGenerationData = hasCodeGenerationData;
            RootNamespace = rootNamespace ?? string.Empty;
            NewScriptLineEndings = newScriptLineEndings;
            HasNamingData = hasNamingData;
            GameObjectNamingScheme = gameObjectNamingScheme;
            GameObjectNamingDigits = gameObjectNamingDigits;
            AssetNamingUsesSpace = assetNamingUsesSpace;
            ProjectFolders = Clone(projectFolders);
            ProjectAssetPaths = Clone(projectAssetPaths);
            CreatedProjectFolders = Clone(createdProjectFolders);
            CreatedProjectAssets = Clone(createdProjectAssets);
            ProjectRootFilePaths = Clone(projectRootFilePaths);
            CreatedProjectRootFiles = Clone(createdProjectRootFiles);
            HasApplicationIdentifierData = hasApplicationIdentifierData;
            ApplicationIdentifierTargetId = applicationIdentifierTargetId ?? string.Empty;
            ApplicationIdentifierTargetLabel = applicationIdentifierTargetLabel ?? string.Empty;
            ApplicationIdentifier = applicationIdentifier ?? string.Empty;
            HasScriptingBackendData = hasScriptingBackendData;
            ScriptingBackendTargetId = scriptingBackendTargetId ?? string.Empty;
            ScriptingBackendTargetLabel = scriptingBackendTargetLabel ?? string.Empty;
            ScriptingBackend = scriptingBackend;
            HasApiCompatibilityLevelData = hasApiCompatibilityLevelData;
            ApiCompatibilityLevelTargetId = apiCompatibilityLevelTargetId ?? string.Empty;
            ApiCompatibilityLevelTargetLabel = apiCompatibilityLevelTargetLabel ?? string.Empty;
            ApiCompatibilityLevel = apiCompatibilityLevel;
            HasManagedStrippingLevelData = hasManagedStrippingLevelData;
            ManagedStrippingLevelTargetId = managedStrippingLevelTargetId ?? string.Empty;
            ManagedStrippingLevelTargetLabel = managedStrippingLevelTargetLabel ?? string.Empty;
            ManagedStrippingLevel = managedStrippingLevel;
        }

        internal SerializationMode AssetSerialization { get; }
        internal string VersionControlMode { get; }
        internal bool EnterPlayModeOptionsEnabled { get; }
        internal EnterPlayModeOptions EnterPlayModeOptions { get; }
        internal ColorSpace ColorSpace { get; }
        internal bool RunInBackground { get; }
        internal string CompanyName { get; }
        internal string ProductName { get; }
        internal string BundleVersion { get; }
        internal bool HasTagManagerData { get; }
        internal string[] Tags { get; }
        internal string[] CustomTags { get; }
        internal string[] Layers { get; }
        internal ProjectSetupSortingLayer[] SortingLayers { get; }
        internal string TagManagerFileText { get; }
        internal bool HasBuildSceneData { get; }
        internal string BuildSceneTargetId { get; }
        internal string BuildSceneTargetLabel { get; }
        internal ProjectSetupBuildSceneState[] BuildScenes { get; }
        internal bool HasPlayModeStartSceneData { get; }
        internal string PlayModeStartSceneGuid { get; }
        internal string PlayModeStartScenePath { get; }
        internal bool HasScriptingDefineData { get; }
        internal string ScriptingDefineTargetId { get; }
        internal string ScriptingDefineTargetLabel { get; }
        internal string[] ScriptingDefineSymbols { get; }
        internal bool HasCodeGenerationData { get; }
        internal string RootNamespace { get; }
        internal LineEndingsMode NewScriptLineEndings { get; }
        internal bool HasNamingData { get; }
        internal EditorSettings.NamingScheme GameObjectNamingScheme { get; }
        internal int GameObjectNamingDigits { get; }
        internal bool AssetNamingUsesSpace { get; }
        internal string[] ProjectFolders { get; }
        internal string[] ProjectAssetPaths { get; }
        internal string[] CreatedProjectFolders { get; }
        internal ProjectSetupCreatedAsset[] CreatedProjectAssets { get; }
        internal string[] ProjectRootFilePaths { get; }
        internal ProjectSetupCreatedRootFile[] CreatedProjectRootFiles { get; }
        internal bool HasApplicationIdentifierData { get; }
        internal string ApplicationIdentifierTargetId { get; }
        internal string ApplicationIdentifierTargetLabel { get; }
        internal string ApplicationIdentifier { get; }
        internal bool HasScriptingBackendData { get; }
        internal string ScriptingBackendTargetId { get; }
        internal string ScriptingBackendTargetLabel { get; }
        internal ScriptingImplementation ScriptingBackend { get; }
        internal bool HasApiCompatibilityLevelData { get; }
        internal string ApiCompatibilityLevelTargetId { get; }
        internal string ApiCompatibilityLevelTargetLabel { get; }
        internal ApiCompatibilityLevel ApiCompatibilityLevel { get; }
        internal bool HasManagedStrippingLevelData { get; }
        internal string ManagedStrippingLevelTargetId { get; }
        internal string ManagedStrippingLevelTargetLabel { get; }
        internal ManagedStrippingLevel ManagedStrippingLevel { get; }

        internal ProjectSetupSnapshot WithCreatedProjectFolders(string[] paths)
        {
            return Copy(ProjectFolders, ProjectAssetPaths, ProjectRootFilePaths, paths, CreatedProjectAssets, CreatedProjectRootFiles);
        }

        internal ProjectSetupSnapshot WithCreatedProjectState(
            string[] folders,
            ProjectSetupCreatedAsset[] assets,
            ProjectSetupCreatedRootFile[] rootFiles = null)
        {
            return Copy(
                ProjectFolders,
                ProjectAssetPaths,
                ProjectRootFilePaths,
                folders,
                assets,
                rootFiles ?? CreatedProjectRootFiles);
        }

        internal ProjectSetupSnapshot WithProjectFolderState(string[] folders, string[] assetPaths)
        {
            return Copy(
                folders,
                assetPaths,
                ProjectRootFilePaths,
                CreatedProjectFolders,
                CreatedProjectAssets,
                CreatedProjectRootFiles);
        }

        internal ProjectSetupSnapshot WithProjectRootFileState(string[] rootFilePaths)
        {
            return Copy(
                ProjectFolders,
                ProjectAssetPaths,
                rootFilePaths,
                CreatedProjectFolders,
                CreatedProjectAssets,
                CreatedProjectRootFiles);
        }

        public bool Equals(ProjectSetupSnapshot other)
        {
            return ScalarEquals(other)
                && HasTagManagerData == other.HasTagManagerData
                && SequenceEqual(Tags, other.Tags)
                && SequenceEqual(CustomTags, other.CustomTags)
                && SequenceEqual(Layers, other.Layers)
                && SequenceEqual(SortingLayers, other.SortingLayers)
                && string.Equals(TagManagerFileText, other.TagManagerFileText, StringComparison.Ordinal)
                && HasBuildSceneData == other.HasBuildSceneData
                && string.Equals(BuildSceneTargetId, other.BuildSceneTargetId, StringComparison.Ordinal)
                && SequenceEqual(BuildScenes, other.BuildScenes)
                && HasPlayModeStartSceneData == other.HasPlayModeStartSceneData
                && (!HasPlayModeStartSceneData
                    || ProjectSetupSceneReference.SameIdentity(
                        PlayModeStartSceneGuid,
                        PlayModeStartScenePath,
                        other.PlayModeStartSceneGuid,
                        other.PlayModeStartScenePath))
                && HasScriptingDefineData == other.HasScriptingDefineData
                && string.Equals(ScriptingDefineTargetId, other.ScriptingDefineTargetId, StringComparison.Ordinal)
                && SequenceEqual(ScriptingDefineSymbols, other.ScriptingDefineSymbols)
                && HasCodeGenerationData == other.HasCodeGenerationData
                && (!HasCodeGenerationData
                    || (string.Equals(RootNamespace, other.RootNamespace, StringComparison.Ordinal)
                        && NewScriptLineEndings == other.NewScriptLineEndings))
                && HasNamingData == other.HasNamingData
                && (!HasNamingData
                    || (GameObjectNamingScheme == other.GameObjectNamingScheme
                        && GameObjectNamingDigits == other.GameObjectNamingDigits
                && AssetNamingUsesSpace == other.AssetNamingUsesSpace))
                && SequenceEqual(CreatedProjectFolders, other.CreatedProjectFolders)
                && SequenceEqual(CreatedProjectAssets, other.CreatedProjectAssets)
                && SequenceEqual(CreatedProjectRootFiles, other.CreatedProjectRootFiles)
                && HasApplicationIdentifierData == other.HasApplicationIdentifierData
                && (!HasApplicationIdentifierData
                    || (string.Equals(ApplicationIdentifierTargetId, other.ApplicationIdentifierTargetId, StringComparison.Ordinal)
                        && string.Equals(ApplicationIdentifier, other.ApplicationIdentifier, StringComparison.Ordinal)))
                && HasScriptingBackendData == other.HasScriptingBackendData
                && (!HasScriptingBackendData
                    || (string.Equals(ScriptingBackendTargetId, other.ScriptingBackendTargetId, StringComparison.Ordinal)
                        && ScriptingBackend == other.ScriptingBackend))
                && HasApiCompatibilityLevelData == other.HasApiCompatibilityLevelData
                && (!HasApiCompatibilityLevelData
                    || (string.Equals(ApiCompatibilityLevelTargetId, other.ApiCompatibilityLevelTargetId, StringComparison.Ordinal)
                        && ApiCompatibilityLevel == other.ApiCompatibilityLevel))
                && HasManagedStrippingLevelData == other.HasManagedStrippingLevelData
                && (!HasManagedStrippingLevelData
                    || (string.Equals(ManagedStrippingLevelTargetId, other.ManagedStrippingLevelTargetId, StringComparison.Ordinal)
                        && ManagedStrippingLevel == other.ManagedStrippingLevel));
        }

        internal bool Matches(ProjectSetupSnapshot actual)
        {
            return ScalarEquals(actual)
                && (!HasTagManagerData
                    || (actual.HasTagManagerData
                        && SequenceEqual(Tags, actual.Tags)
                        && SequenceEqual(CustomTags, actual.CustomTags)
                        && SequenceEqual(Layers, actual.Layers)
                        && SequenceEqual(SortingLayers, actual.SortingLayers)
                        && string.Equals(TagManagerFileText, actual.TagManagerFileText, StringComparison.Ordinal)))
                && (!HasBuildSceneData
                    || (actual.HasBuildSceneData
                        && string.Equals(BuildSceneTargetId, actual.BuildSceneTargetId, StringComparison.Ordinal)
                        && SequenceEqual(BuildScenes, actual.BuildScenes)))
                && (!HasPlayModeStartSceneData
                    || (actual.HasPlayModeStartSceneData
                        && ProjectSetupSceneReference.SameIdentity(
                            PlayModeStartSceneGuid,
                            PlayModeStartScenePath,
                            actual.PlayModeStartSceneGuid,
                            actual.PlayModeStartScenePath)))
                && (!HasScriptingDefineData
                    || (actual.HasScriptingDefineData
                        && string.Equals(ScriptingDefineTargetId, actual.ScriptingDefineTargetId, StringComparison.Ordinal)
                        && SequenceEqual(ScriptingDefineSymbols, actual.ScriptingDefineSymbols)))
                && (!HasCodeGenerationData
                    || (actual.HasCodeGenerationData
                        && string.Equals(RootNamespace, actual.RootNamespace, StringComparison.Ordinal)
                        && NewScriptLineEndings == actual.NewScriptLineEndings))
                && (!HasNamingData
                    || (actual.HasNamingData
                        && GameObjectNamingScheme == actual.GameObjectNamingScheme
                        && GameObjectNamingDigits == actual.GameObjectNamingDigits
                        && AssetNamingUsesSpace == actual.AssetNamingUsesSpace))
                && (!HasApplicationIdentifierData
                    || (actual.HasApplicationIdentifierData
                        && string.Equals(ApplicationIdentifierTargetId, actual.ApplicationIdentifierTargetId, StringComparison.Ordinal)
                        && string.Equals(ApplicationIdentifier, actual.ApplicationIdentifier, StringComparison.Ordinal)))
                && (!HasScriptingBackendData
                    || (actual.HasScriptingBackendData
                        && string.Equals(ScriptingBackendTargetId, actual.ScriptingBackendTargetId, StringComparison.Ordinal)
                        && ScriptingBackend == actual.ScriptingBackend))
                && (!HasApiCompatibilityLevelData
                    || (actual.HasApiCompatibilityLevelData
                        && string.Equals(ApiCompatibilityLevelTargetId, actual.ApiCompatibilityLevelTargetId, StringComparison.Ordinal)
                        && ApiCompatibilityLevel == actual.ApiCompatibilityLevel))
                && (!HasManagedStrippingLevelData
                    || (actual.HasManagedStrippingLevelData
                        && string.Equals(ManagedStrippingLevelTargetId, actual.ManagedStrippingLevelTargetId, StringComparison.Ordinal)
                        && ManagedStrippingLevel == actual.ManagedStrippingLevel));
        }

        private bool ScalarEquals(ProjectSetupSnapshot other)
        {
            return AssetSerialization == other.AssetSerialization
                && string.Equals(VersionControlMode, other.VersionControlMode, StringComparison.Ordinal)
                && EnterPlayModeOptionsEnabled == other.EnterPlayModeOptionsEnabled
                && EnterPlayModeOptions == other.EnterPlayModeOptions
                && ColorSpace == other.ColorSpace
                && RunInBackground == other.RunInBackground
                && string.Equals(CompanyName, other.CompanyName, StringComparison.Ordinal)
                && string.Equals(ProductName, other.ProductName, StringComparison.Ordinal)
                && string.Equals(BundleVersion, other.BundleVersion, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ProjectSetupSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)AssetSerialization;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(VersionControlMode);
                hash = (hash * 397) ^ EnterPlayModeOptionsEnabled.GetHashCode();
                hash = (hash * 397) ^ (int)EnterPlayModeOptions;
                hash = (hash * 397) ^ (int)ColorSpace;
                hash = (hash * 397) ^ RunInBackground.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(CompanyName);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ProductName);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(BundleVersion);
                hash = (hash * 397) ^ HasTagManagerData.GetHashCode();
                hash = AddHash(hash, Tags);
                hash = AddHash(hash, CustomTags);
                hash = AddHash(hash, Layers);
                hash = AddHash(hash, SortingLayers);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(TagManagerFileText ?? string.Empty);
                hash = (hash * 397) ^ HasBuildSceneData.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(BuildSceneTargetId ?? string.Empty);
                hash = AddHash(hash, BuildScenes);
                hash = (hash * 397) ^ HasPlayModeStartSceneData.GetHashCode();
                if (HasPlayModeStartSceneData)
                {
                    hash = (hash * 397) ^ (!string.IsNullOrEmpty(PlayModeStartSceneGuid)
                        ? StringComparer.Ordinal.GetHashCode(PlayModeStartSceneGuid)
                        : StringComparer.OrdinalIgnoreCase.GetHashCode(PlayModeStartScenePath ?? string.Empty));
                }
                hash = (hash * 397) ^ HasScriptingDefineData.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ScriptingDefineTargetId ?? string.Empty);
                hash = AddHash(hash, ScriptingDefineSymbols);
                hash = (hash * 397) ^ HasCodeGenerationData.GetHashCode();
                if (HasCodeGenerationData)
                {
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(RootNamespace ?? string.Empty);
                    hash = (hash * 397) ^ (int)NewScriptLineEndings;
                }
                hash = (hash * 397) ^ HasNamingData.GetHashCode();
                if (HasNamingData)
                {
                    hash = (hash * 397) ^ (int)GameObjectNamingScheme;
                    hash = (hash * 397) ^ GameObjectNamingDigits;
                    hash = (hash * 397) ^ AssetNamingUsesSpace.GetHashCode();
                }
                hash = AddHash(hash, CreatedProjectFolders);
                hash = AddHash(hash, CreatedProjectAssets);
                hash = AddHash(hash, CreatedProjectRootFiles);
                hash = (hash * 397) ^ HasApplicationIdentifierData.GetHashCode();
                if (HasApplicationIdentifierData)
                {
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ApplicationIdentifierTargetId ?? string.Empty);
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ApplicationIdentifier ?? string.Empty);
                }
                hash = (hash * 397) ^ HasScriptingBackendData.GetHashCode();
                if (HasScriptingBackendData)
                {
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ScriptingBackendTargetId ?? string.Empty);
                    hash = (hash * 397) ^ (int)ScriptingBackend;
                }
                hash = (hash * 397) ^ HasApiCompatibilityLevelData.GetHashCode();
                if (HasApiCompatibilityLevelData)
                {
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ApiCompatibilityLevelTargetId ?? string.Empty);
                    hash = (hash * 397) ^ (int)ApiCompatibilityLevel;
                }
                hash = (hash * 397) ^ HasManagedStrippingLevelData.GetHashCode();
                if (HasManagedStrippingLevelData)
                {
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ManagedStrippingLevelTargetId ?? string.Empty);
                    hash = (hash * 397) ^ (int)ManagedStrippingLevel;
                }
                return hash;
            }
        }

        private ProjectSetupSnapshot Copy(
            string[] projectFolders,
            string[] projectAssetPaths,
            string[] projectRootFilePaths,
            string[] createdProjectFolders,
            ProjectSetupCreatedAsset[] createdProjectAssets,
            ProjectSetupCreatedRootFile[] createdProjectRootFiles)
        {
            return new ProjectSetupSnapshot(
                AssetSerialization,
                VersionControlMode,
                EnterPlayModeOptionsEnabled,
                EnterPlayModeOptions,
                ColorSpace,
                RunInBackground,
                CompanyName,
                ProductName,
                BundleVersion,
                HasTagManagerData,
                Tags,
                CustomTags,
                Layers,
                SortingLayers,
                TagManagerFileText,
                HasBuildSceneData,
                BuildSceneTargetId,
                BuildSceneTargetLabel,
                BuildScenes,
                HasPlayModeStartSceneData,
                PlayModeStartSceneGuid,
                PlayModeStartScenePath,
                HasScriptingDefineData,
                ScriptingDefineTargetId,
                ScriptingDefineTargetLabel,
                ScriptingDefineSymbols,
                HasCodeGenerationData,
                RootNamespace,
                NewScriptLineEndings,
                HasNamingData,
                GameObjectNamingScheme,
                GameObjectNamingDigits,
                AssetNamingUsesSpace,
                projectFolders,
                projectAssetPaths,
                createdProjectFolders,
                createdProjectAssets,
                projectRootFilePaths,
                createdProjectRootFiles,
                HasApplicationIdentifierData,
                ApplicationIdentifierTargetId,
                ApplicationIdentifierTargetLabel,
                ApplicationIdentifier,
                HasScriptingBackendData,
                ScriptingBackendTargetId,
                ScriptingBackendTargetLabel,
                ScriptingBackend,
                HasApiCompatibilityLevelData,
                ApiCompatibilityLevelTargetId,
                ApiCompatibilityLevelTargetLabel,
                ApiCompatibilityLevel,
                HasManagedStrippingLevelData,
                ManagedStrippingLevelTargetId,
                ManagedStrippingLevelTargetLabel,
                ManagedStrippingLevel);
        }

        private static string[] Clone(string[] values)
        {
            return values == null ? Array.Empty<string>() : (string[])values.Clone();
        }

        private static ProjectSetupSortingLayer[] Clone(ProjectSetupSortingLayer[] values)
        {
            return values == null ? Array.Empty<ProjectSetupSortingLayer>() : (ProjectSetupSortingLayer[])values.Clone();
        }

        private static ProjectSetupBuildSceneState[] Clone(ProjectSetupBuildSceneState[] values)
        {
            return values == null ? Array.Empty<ProjectSetupBuildSceneState>() : (ProjectSetupBuildSceneState[])values.Clone();
        }

        private static ProjectSetupCreatedAsset[] Clone(ProjectSetupCreatedAsset[] values)
        {
            return values == null ? Array.Empty<ProjectSetupCreatedAsset>() : (ProjectSetupCreatedAsset[])values.Clone();
        }

        private static ProjectSetupCreatedRootFile[] Clone(ProjectSetupCreatedRootFile[] values)
        {
            return values == null
                ? Array.Empty<ProjectSetupCreatedRootFile>()
                : (ProjectSetupCreatedRootFile[])values.Clone();
        }

        private static bool SequenceEqual<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            if (left.Count != right.Count)
            {
                return false;
            }

            var comparer = EqualityComparer<T>.Default;
            for (var index = 0; index < left.Count; index++)
            {
                if (!comparer.Equals(left[index], right[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static int AddHash<T>(int hash, IReadOnlyList<T> values)
        {
            unchecked
            {
                if (values == null)
                {
                    return hash * 397;
                }

                for (var index = 0; index < values.Count; index++)
                {
                    var value = values[index];
                    hash = (hash * 397) ^ ((object)value == null ? 0 : EqualityComparer<T>.Default.GetHashCode(value));
                }

                return hash;
            }
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }
    }
}
