// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ProjectSetup.Editor
{
    internal sealed class ProjectSetupBackupStore : IProjectSetupBackupStore
    {
        private const string RelativePath = "ProjectSettings/ProjectSetupLastBackup.json";
        private readonly string _path;

        internal ProjectSetupBackupStore()
            : this(Path.GetFullPath(RelativePath))
        {
        }

        internal ProjectSetupBackupStore(string path)
        {
            _path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
        }

        public bool Exists => File.Exists(_path);

        public void Save(ProjectSetupSnapshot snapshot)
        {
            var data = ProjectSetupSnapshotData.FromSnapshot(snapshot);
            var json = JsonUtility.ToJson(data, true) + "\n";
            var directory = Path.GetDirectoryName(_path) ?? throw new InvalidOperationException("Backup path has no directory.");
            Directory.CreateDirectory(directory);
            var temporaryPath = _path + ".tmp";
            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(_path))
                {
                    File.Replace(temporaryPath, _path, null);
                }
                else
                {
                    File.Move(temporaryPath, _path);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        public bool TryLoad(out ProjectSetupSnapshot snapshot, out string error)
        {
            snapshot = default;
            error = string.Empty;
            if (!File.Exists(_path))
            {
                error = "No Project Setup backup exists.";
                return false;
            }

            try
            {
                var json = File.ReadAllText(_path, new UTF8Encoding(false, true));
                var data = JsonUtility.FromJson<ProjectSetupSnapshotData>(json);
                if (data == null || data.schemaVersion < 1 || data.schemaVersion > 15)
                {
                    error = "The Project Setup backup schema is unsupported.";
                    return false;
                }

                snapshot = data.ToSnapshot();
                return true;
            }
            catch (Exception exception)
            {
                error = $"Backup could not be read: {exception.Message}";
                return false;
            }
        }

        [Serializable]
        private sealed class ProjectSetupSnapshotData
        {
            public int schemaVersion = 15;
            public int assetSerialization;
            public string versionControlMode;
            public bool enterPlayModeOptionsEnabled;
            public int enterPlayModeOptions;
            public int colorSpace;
            public bool runInBackground;
            public string companyName;
            public string productName;
            public string bundleVersion;
            public string[] tags;
            public string[] customTags;
            public string[] layers;
            public SortingLayerData[] sortingLayers;
            public string tagManagerFileText;
            public bool hasBuildSceneData;
            public string buildSceneTargetId;
            public string buildSceneTargetLabel;
            public BuildSceneData[] buildScenes;
            public bool hasPlayModeStartSceneData;
            public string playModeStartSceneGuid;
            public string playModeStartScenePath;
            public bool hasScriptingDefineData;
            public string scriptingDefineTargetId;
            public string scriptingDefineTargetLabel;
            public string[] scriptingDefineSymbols;
            public bool hasCodeGenerationData;
            public string rootNamespace;
            public int newScriptLineEndings;
            public bool hasNamingData;
            public int gameObjectNamingScheme;
            public int gameObjectNamingDigits;
            public bool assetNamingUsesSpace;
            public string[] createdProjectFolders;
            public CreatedAssetData[] createdProjectAssets;
            public CreatedRootFileData[] createdProjectRootFiles;
            public bool hasApplicationIdentifierData;
            public string applicationIdentifierTargetId;
            public string applicationIdentifierTargetLabel;
            public string applicationIdentifier;
            public bool hasScriptingBackendData;
            public string scriptingBackendTargetId;
            public string scriptingBackendTargetLabel;
            public int scriptingBackend;
            public bool hasApiCompatibilityLevelData;
            public string apiCompatibilityLevelTargetId;
            public string apiCompatibilityLevelTargetLabel;
            public int apiCompatibilityLevel;
            public bool hasManagedStrippingLevelData;
            public string managedStrippingLevelTargetId;
            public string managedStrippingLevelTargetLabel;
            public int managedStrippingLevel;
            public bool hasIl2CppCodeGenerationData;
            public string il2CppCodeGenerationTargetId;
            public string il2CppCodeGenerationTargetLabel;
            public int il2CppCodeGeneration;

            internal static ProjectSetupSnapshotData FromSnapshot(ProjectSetupSnapshot snapshot)
            {
                return new ProjectSetupSnapshotData
                {
                    assetSerialization = (int)snapshot.AssetSerialization,
                    versionControlMode = snapshot.VersionControlMode,
                    enterPlayModeOptionsEnabled = snapshot.EnterPlayModeOptionsEnabled,
                    enterPlayModeOptions = (int)snapshot.EnterPlayModeOptions,
                    colorSpace = (int)snapshot.ColorSpace,
                    runInBackground = snapshot.RunInBackground,
                    companyName = snapshot.CompanyName,
                    productName = snapshot.ProductName,
                    bundleVersion = snapshot.BundleVersion,
                    tags = snapshot.Tags,
                    customTags = snapshot.CustomTags,
                    layers = snapshot.Layers,
                    sortingLayers = Array.ConvertAll(
                        snapshot.SortingLayers,
                        layer => new SortingLayerData
                        {
                            name = layer.Name,
                            uniqueId = layer.UniqueId,
                            locked = layer.Locked
                        }),
                    tagManagerFileText = snapshot.TagManagerFileText,
                    hasBuildSceneData = snapshot.HasBuildSceneData,
                    buildSceneTargetId = snapshot.BuildSceneTargetId,
                    buildSceneTargetLabel = snapshot.BuildSceneTargetLabel,
                    buildScenes = Array.ConvertAll(
                        snapshot.BuildScenes,
                        scene => new BuildSceneData
                        {
                            sceneGuid = scene.SceneGuid,
                            path = scene.Path,
                            enabled = scene.Enabled
                        }),
                    hasPlayModeStartSceneData = snapshot.HasPlayModeStartSceneData,
                    playModeStartSceneGuid = snapshot.PlayModeStartSceneGuid,
                    playModeStartScenePath = snapshot.PlayModeStartScenePath,
                    hasScriptingDefineData = snapshot.HasScriptingDefineData,
                    scriptingDefineTargetId = snapshot.ScriptingDefineTargetId,
                    scriptingDefineTargetLabel = snapshot.ScriptingDefineTargetLabel,
                    scriptingDefineSymbols = snapshot.ScriptingDefineSymbols,
                    hasCodeGenerationData = snapshot.HasCodeGenerationData,
                    rootNamespace = snapshot.RootNamespace,
                    newScriptLineEndings = (int)snapshot.NewScriptLineEndings,
                    hasNamingData = snapshot.HasNamingData,
                    gameObjectNamingScheme = (int)snapshot.GameObjectNamingScheme,
                    gameObjectNamingDigits = snapshot.GameObjectNamingDigits,
                    assetNamingUsesSpace = snapshot.AssetNamingUsesSpace,
                    createdProjectFolders = snapshot.CreatedProjectFolders,
                    createdProjectAssets = Array.ConvertAll(
                        snapshot.CreatedProjectAssets,
                        asset => new CreatedAssetData
                        {
                            path = asset.Path,
                            contentHash = asset.ContentHash
                        }),
                    createdProjectRootFiles = Array.ConvertAll(
                        snapshot.CreatedProjectRootFiles,
                        file => new CreatedRootFileData
                        {
                            path = file.Path,
                            contentHash = file.ContentHash
                        }),
                    hasApplicationIdentifierData = snapshot.HasApplicationIdentifierData,
                    applicationIdentifierTargetId = snapshot.ApplicationIdentifierTargetId,
                    applicationIdentifierTargetLabel = snapshot.ApplicationIdentifierTargetLabel,
                    applicationIdentifier = snapshot.ApplicationIdentifier,
                    hasScriptingBackendData = snapshot.HasScriptingBackendData,
                    scriptingBackendTargetId = snapshot.ScriptingBackendTargetId,
                    scriptingBackendTargetLabel = snapshot.ScriptingBackendTargetLabel,
                    scriptingBackend = (int)snapshot.ScriptingBackend,
                    hasApiCompatibilityLevelData = snapshot.HasApiCompatibilityLevelData,
                    apiCompatibilityLevelTargetId = snapshot.ApiCompatibilityLevelTargetId,
                    apiCompatibilityLevelTargetLabel = snapshot.ApiCompatibilityLevelTargetLabel,
                    apiCompatibilityLevel = (int)snapshot.ApiCompatibilityLevel,
                    hasManagedStrippingLevelData = snapshot.HasManagedStrippingLevelData,
                    managedStrippingLevelTargetId = snapshot.ManagedStrippingLevelTargetId,
                    managedStrippingLevelTargetLabel = snapshot.ManagedStrippingLevelTargetLabel,
                    managedStrippingLevel = (int)snapshot.ManagedStrippingLevel,
                    hasIl2CppCodeGenerationData = snapshot.HasIl2CppCodeGenerationData,
                    il2CppCodeGenerationTargetId = snapshot.Il2CppCodeGenerationTargetId,
                    il2CppCodeGenerationTargetLabel = snapshot.Il2CppCodeGenerationTargetLabel,
                    il2CppCodeGeneration = (int)snapshot.Il2CppCodeGeneration
                };
            }

            internal ProjectSetupSnapshot ToSnapshot()
            {
                return new ProjectSetupSnapshot(
                    (SerializationMode)assetSerialization,
                    versionControlMode,
                    enterPlayModeOptionsEnabled,
                    (EnterPlayModeOptions)enterPlayModeOptions,
                    (ColorSpace)colorSpace,
                    runInBackground,
                    companyName,
                    productName,
                    bundleVersion,
                    schemaVersion >= 2,
                    schemaVersion >= 2 ? tags : Array.Empty<string>(),
                    schemaVersion >= 2 ? customTags : Array.Empty<string>(),
                    schemaVersion >= 2 ? layers : Array.Empty<string>(),
                    schemaVersion >= 2 && sortingLayers != null
                        ? Array.ConvertAll(
                            sortingLayers,
                            layer => new ProjectSetupSortingLayer(layer.name, layer.uniqueId, layer.locked))
                        : Array.Empty<ProjectSetupSortingLayer>(),
                    schemaVersion >= 2 ? tagManagerFileText : string.Empty,
                    schemaVersion >= 3 && hasBuildSceneData,
                    schemaVersion >= 3 ? buildSceneTargetId : string.Empty,
                    schemaVersion >= 3 ? buildSceneTargetLabel : string.Empty,
                    schemaVersion >= 3 && buildScenes != null
                        ? Array.ConvertAll(
                            buildScenes,
                            scene => new ProjectSetupBuildSceneState(scene.sceneGuid, scene.path, scene.enabled))
                        : Array.Empty<ProjectSetupBuildSceneState>(),
                    schemaVersion >= 4 && hasPlayModeStartSceneData,
                    schemaVersion >= 4 ? playModeStartSceneGuid : string.Empty,
                    schemaVersion >= 4 ? playModeStartScenePath : string.Empty,
                    schemaVersion >= 5 && hasScriptingDefineData,
                    schemaVersion >= 5 ? scriptingDefineTargetId : string.Empty,
                    schemaVersion >= 5 ? scriptingDefineTargetLabel : string.Empty,
                    schemaVersion >= 5 ? scriptingDefineSymbols : Array.Empty<string>(),
                    schemaVersion >= 6 && hasCodeGenerationData,
                    schemaVersion >= 6 ? rootNamespace : string.Empty,
                    schemaVersion >= 6 ? (LineEndingsMode)newScriptLineEndings : LineEndingsMode.OSNative,
                    schemaVersion >= 7 && hasNamingData,
                    schemaVersion >= 7 ? (EditorSettings.NamingScheme)gameObjectNamingScheme : EditorSettings.NamingScheme.SpaceParenthesis,
                    schemaVersion >= 7 ? gameObjectNamingDigits : 1,
                    schemaVersion >= 7 && assetNamingUsesSpace,
                    createdProjectFolders: schemaVersion >= 8 ? createdProjectFolders : Array.Empty<string>(),
                    createdProjectAssets: schemaVersion >= 9 && createdProjectAssets != null
                        ? Array.ConvertAll(
                            createdProjectAssets,
                            asset => new ProjectSetupCreatedAsset(asset.path, asset.contentHash))
                        : Array.Empty<ProjectSetupCreatedAsset>(),
                    createdProjectRootFiles: schemaVersion >= 10 && createdProjectRootFiles != null
                        ? Array.ConvertAll(
                            createdProjectRootFiles,
                            file => new ProjectSetupCreatedRootFile(file.path, file.contentHash))
                        : Array.Empty<ProjectSetupCreatedRootFile>(),
                    hasApplicationIdentifierData: schemaVersion >= 11 && hasApplicationIdentifierData,
                    applicationIdentifierTargetId: schemaVersion >= 11 ? applicationIdentifierTargetId : string.Empty,
                    applicationIdentifierTargetLabel: schemaVersion >= 11 ? applicationIdentifierTargetLabel : string.Empty,
                    applicationIdentifier: schemaVersion >= 11 ? applicationIdentifier : string.Empty,
                    hasScriptingBackendData: schemaVersion >= 12 && hasScriptingBackendData,
                    scriptingBackendTargetId: schemaVersion >= 12 ? scriptingBackendTargetId : string.Empty,
                    scriptingBackendTargetLabel: schemaVersion >= 12 ? scriptingBackendTargetLabel : string.Empty,
                    scriptingBackend: schemaVersion >= 12
                        ? (ScriptingImplementation)scriptingBackend
                        : ScriptingImplementation.Mono2x,
                    hasApiCompatibilityLevelData: schemaVersion >= 13 && hasApiCompatibilityLevelData,
                    apiCompatibilityLevelTargetId: schemaVersion >= 13 ? apiCompatibilityLevelTargetId : string.Empty,
                    apiCompatibilityLevelTargetLabel: schemaVersion >= 13 ? apiCompatibilityLevelTargetLabel : string.Empty,
                    apiCompatibilityLevel: schemaVersion >= 13
                        ? (ApiCompatibilityLevel)apiCompatibilityLevel
                        : ApiCompatibilityLevel.NET_Standard,
                    hasManagedStrippingLevelData: schemaVersion >= 14 && hasManagedStrippingLevelData,
                    managedStrippingLevelTargetId: schemaVersion >= 14 ? managedStrippingLevelTargetId : string.Empty,
                    managedStrippingLevelTargetLabel: schemaVersion >= 14 ? managedStrippingLevelTargetLabel : string.Empty,
                    managedStrippingLevel: schemaVersion >= 14
                        ? (ManagedStrippingLevel)managedStrippingLevel
                        : ManagedStrippingLevel.Minimal,
                    hasIl2CppCodeGenerationData: schemaVersion >= 15 && hasIl2CppCodeGenerationData,
                    il2CppCodeGenerationTargetId: schemaVersion >= 15 ? il2CppCodeGenerationTargetId : string.Empty,
                    il2CppCodeGenerationTargetLabel: schemaVersion >= 15 ? il2CppCodeGenerationTargetLabel : string.Empty,
                    il2CppCodeGeneration: schemaVersion >= 15
                        ? (Il2CppCodeGeneration)il2CppCodeGeneration
                        : Il2CppCodeGeneration.OptimizeSpeed);
            }
        }

        [Serializable]
        private sealed class SortingLayerData
        {
            public string name;
            public int uniqueId;
            public bool locked;
        }

        [Serializable]
        private sealed class BuildSceneData
        {
            public string sceneGuid;
            public string path;
            public bool enabled;
        }

        [Serializable]
        private sealed class CreatedAssetData
        {
            public string path;
            public string contentHash;
        }

        [Serializable]
        private sealed class CreatedRootFileData
        {
            public string path;
            public string contentHash;
        }
    }
}
