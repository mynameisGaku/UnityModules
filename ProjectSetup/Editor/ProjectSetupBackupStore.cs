// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Text;
using UnityEditor;
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
                if (data == null || (data.schemaVersion != 1 && data.schemaVersion != 2 && data.schemaVersion != 3 && data.schemaVersion != 4 && data.schemaVersion != 5 && data.schemaVersion != 6))
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
            public int schemaVersion = 6;
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
                    newScriptLineEndings = (int)snapshot.NewScriptLineEndings
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
                    schemaVersion >= 6 ? (LineEndingsMode)newScriptLineEndings : LineEndingsMode.OSNative);
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
    }
}
