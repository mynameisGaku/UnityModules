// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;

namespace ProjectSetup.Editor
{
    internal sealed class UnityProjectSetupEnvironment : IProjectSetupEnvironment
    {
        private readonly ProjectSetupVersionControlFileStore _versionControlFileStore;

        internal UnityProjectSetupEnvironment()
            : this(new ProjectSetupVersionControlFileStore(GetProjectRoot()))
        {
        }

        internal UnityProjectSetupEnvironment(ProjectSetupVersionControlFileStore versionControlFileStore)
        {
            _versionControlFileStore = versionControlFileStore
                ?? throw new ArgumentNullException(nameof(versionControlFileStore));
        }

        public bool IsAvailable => !EditorApplication.isPlayingOrWillChangePlaymode
            && !EditorApplication.isCompiling
            && !EditorApplication.isUpdating;

        public ProjectSetupSnapshot Capture()
        {
            ProjectSetupTagManagerStore.Capture(out var tags, out var customTags, out var layers, out var sortingLayers, out var tagManagerFileText);
            CaptureBuildScenes(out var buildSceneTargetId, out var buildSceneTargetLabel, out var buildScenes);
            CapturePlayModeStartScene(out var playModeStartSceneGuid, out var playModeStartScenePath);
            CaptureScriptingDefines(out var hasScriptingDefineData, out var scriptingDefineTargetId, out var scriptingDefineTargetLabel, out var scriptingDefineSymbols);
            CaptureProjectFolders(out var projectFolders, out var projectAssetPaths);
            return new ProjectSetupSnapshot(
                EditorSettings.serializationMode,
                VersionControlSettings.mode,
                EditorSettings.enterPlayModeOptionsEnabled,
                EditorSettings.enterPlayModeOptions,
                PlayerSettings.colorSpace,
                PlayerSettings.runInBackground,
                PlayerSettings.companyName,
                PlayerSettings.productName,
                PlayerSettings.bundleVersion,
                true,
                tags,
                customTags,
                layers,
                sortingLayers,
                tagManagerFileText,
                true,
                buildSceneTargetId,
                buildSceneTargetLabel,
                buildScenes,
                true,
                playModeStartSceneGuid,
                playModeStartScenePath,
                hasScriptingDefineData,
                scriptingDefineTargetId,
                scriptingDefineTargetLabel,
                scriptingDefineSymbols,
                true,
                EditorSettings.projectGenerationRootNamespace,
                EditorSettings.lineEndingsForNewScripts,
                true,
                EditorSettings.gameObjectNamingScheme,
                EditorSettings.gameObjectNamingDigits,
                EditorSettings.assetNamingUsesSpace,
                projectFolders,
                projectAssetPaths,
                projectRootFilePaths: _versionControlFileStore.CapturePaths());
        }

        public ProjectSetupEnvironmentApplyResult Apply(ProjectSetupProfile profile)
        {
            var createdProjectFolders = Array.Empty<string>();
            var createdProjectAssets = Array.Empty<ProjectSetupCreatedAsset>();
            var createdProjectRootFiles = Array.Empty<ProjectSetupCreatedRootFile>();
            if (profile.ConfigureAssetSerialization)
            {
                EditorSettings.serializationMode = profile.AssetSerialization;
            }

            if (profile.ConfigureVersionControl)
            {
                VersionControlSettings.mode = profile.VersionControlMode;
            }

            if (profile.ConfigureEnterPlayMode)
            {
                EditorSettings.enterPlayModeOptionsEnabled = profile.EnterPlayModeOptionsEnabled;
                EditorSettings.enterPlayModeOptions = profile.EnterPlayModeOptions;
            }

            if (profile.ConfigurePlayModeStartScene)
            {
                ApplyPlayModeStartScene(profile.PlayModeStartScene);
            }

            if (profile.ConfigureColorSpace)
            {
                PlayerSettings.colorSpace = profile.ColorSpace;
            }

            if (profile.ConfigureRunInBackground)
            {
                PlayerSettings.runInBackground = profile.RunInBackground;
            }

            if (profile.ConfigureCompanyName)
            {
                PlayerSettings.companyName = profile.CompanyName;
            }

            if (profile.ConfigureProductName)
            {
                PlayerSettings.productName = profile.ProductName;
            }

            if (profile.ConfigureBundleVersion)
            {
                PlayerSettings.bundleVersion = profile.BundleVersion;
            }

            if (profile.ConfigureBuildScenes)
            {
                ApplyBuildScenes(ToEditorBuildSettingsScenes(profile.BuildScenes));
            }

            if (profile.ConfigureScriptingDefineSymbols)
            {
                ApplyMissingScriptingDefines(profile.ScriptingDefineSymbols);
            }

            if (profile.ConfigureRootNamespace)
            {
                EditorSettings.projectGenerationRootNamespace = profile.RootNamespace;
            }

            if (profile.ConfigureNewScriptLineEndings)
            {
                EditorSettings.lineEndingsForNewScripts = profile.NewScriptLineEndings;
            }

            if (profile.ConfigureNamingDefaults)
            {
                EditorSettings.gameObjectNamingScheme = profile.GameObjectNamingScheme;
                EditorSettings.gameObjectNamingDigits = profile.GameObjectNamingDigits;
                EditorSettings.assetNamingUsesSpace = profile.AssetNamingUsesSpace;
            }

            if (profile.ConfigureProjectFolders || profile.ConfigureAssemblyDefinitions)
            {
                var current = Capture();
                createdProjectFolders = CreateProjectFolders(ProjectSetupPlanner.GetMissingProjectFolders(profile, current));
            }

            if (profile.ConfigureAssemblyDefinitions)
            {
                createdProjectAssets = CreateAssemblyDefinitions(
                    ProjectSetupPlanner.GetMissingAssemblyDefinitions(profile, Capture()));
            }

            ProjectSetupTagManagerStore.Apply(profile);
            AssetDatabase.SaveAssets();
            if (profile.ConfigureVersionControlFiles)
            {
                createdProjectRootFiles = _versionControlFileStore.Create(
                    ProjectSetupPlanner.GetMissingVersionControlFiles(profile, Capture()));
            }

            return new ProjectSetupEnvironmentApplyResult(
                createdProjectFolders,
                createdProjectAssets,
                createdProjectRootFiles);
        }

        public void Apply(ProjectSetupSnapshot snapshot)
        {
            if (snapshot.HasBuildSceneData)
            {
                CaptureBuildScenes(out var currentTargetId, out _, out _);
                if (!string.Equals(currentTargetId, snapshot.BuildSceneTargetId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("The active Build Scene target changed after the backup was created.");
                }
            }

            if (snapshot.HasScriptingDefineData)
            {
                CaptureScriptingDefines(out var available, out var currentTargetId, out _, out _);
                if (!available || !string.Equals(currentTargetId, snapshot.ScriptingDefineTargetId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("The active scripting define target changed after the backup was created.");
                }
            }

            EditorSettings.serializationMode = snapshot.AssetSerialization;
            VersionControlSettings.mode = snapshot.VersionControlMode;
            EditorSettings.enterPlayModeOptionsEnabled = snapshot.EnterPlayModeOptionsEnabled;
            EditorSettings.enterPlayModeOptions = snapshot.EnterPlayModeOptions;
            if (snapshot.HasPlayModeStartSceneData)
            {
                ApplyPlayModeStartScene(new ProjectSetupSceneReference(snapshot.PlayModeStartSceneGuid, snapshot.PlayModeStartScenePath));
            }
            PlayerSettings.colorSpace = snapshot.ColorSpace;
            PlayerSettings.runInBackground = snapshot.RunInBackground;
            PlayerSettings.companyName = snapshot.CompanyName;
            PlayerSettings.productName = snapshot.ProductName;
            PlayerSettings.bundleVersion = snapshot.BundleVersion;
            if (snapshot.HasBuildSceneData)
            {
                ApplyBuildScenes(ToEditorBuildSettingsScenes(snapshot.BuildScenes));
            }

            if (snapshot.HasScriptingDefineData)
            {
                SetScriptingDefines(snapshot.ScriptingDefineSymbols);
            }

            if (snapshot.HasCodeGenerationData)
            {
                EditorSettings.projectGenerationRootNamespace = snapshot.RootNamespace;
                EditorSettings.lineEndingsForNewScripts = snapshot.NewScriptLineEndings;
            }

            if (snapshot.HasNamingData)
            {
                EditorSettings.gameObjectNamingScheme = snapshot.GameObjectNamingScheme;
                EditorSettings.gameObjectNamingDigits = snapshot.GameObjectNamingDigits;
                EditorSettings.assetNamingUsesSpace = snapshot.AssetNamingUsesSpace;
            }

            CreateProjectFolders(snapshot.ProjectFolders
                .OrderBy(ProjectSetupFolderUtility.GetDepth)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray());
            RestoreCreatedProjectAssets(snapshot.CreatedProjectAssets);
            RestoreCreatedProjectFolders(snapshot.CreatedProjectFolders);
            _versionControlFileStore.Restore(snapshot.CreatedProjectRootFiles);

            ProjectSetupTagManagerStore.Restore(snapshot);
            AssetDatabase.SaveAssets();
        }

        private static void CaptureBuildScenes(
            out string targetId,
            out string targetLabel,
            out ProjectSetupBuildSceneState[] buildScenes)
        {
            var activeProfile = BuildProfile.GetActiveBuildProfile();
            var scenes = activeProfile != null
                ? activeProfile.GetScenesForBuild() ?? Array.Empty<EditorBuildSettingsScene>()
                : EditorBuildSettings.scenes ?? Array.Empty<EditorBuildSettingsScene>();
            if (activeProfile != null && activeProfile.overrideGlobalScenes)
            {
                var path = NormalizePath(AssetDatabase.GetAssetPath(activeProfile));
                var guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
                targetId = !string.IsNullOrEmpty(guid) ? "profile:" + guid : "profile-path:" + path;
                targetLabel = $"Build Profile: {activeProfile.name}";
            }
            else
            {
                targetId = "global";
                targetLabel = "Global Build Scenes";
            }

            buildScenes = Array.ConvertAll(
                scenes,
                scene =>
                {
                    var path = NormalizePath(scene.path);
                    return new ProjectSetupBuildSceneState(AssetDatabase.AssetPathToGUID(path), path, scene.enabled);
                });
        }

        private static void CapturePlayModeStartScene(out string sceneGuid, out string scenePath)
        {
            var scene = EditorSceneManager.playModeStartScene;
            scenePath = scene == null ? string.Empty : NormalizePath(AssetDatabase.GetAssetPath(scene));
            sceneGuid = string.IsNullOrEmpty(scenePath) ? string.Empty : AssetDatabase.AssetPathToGUID(scenePath);
        }

        private static void CaptureScriptingDefines(
            out bool available,
            out string targetId,
            out string targetLabel,
            out string[] symbols)
        {
            var group = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            available = group != BuildTargetGroup.Unknown;
            targetId = available ? group.ToString() : string.Empty;
            targetLabel = targetId;
            if (!available)
            {
                symbols = Array.Empty<string>();
                return;
            }

            var raw = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(group));
            symbols = SplitScriptingDefines(raw);
        }

        private static void ApplyMissingScriptingDefines(string[] requested)
        {
            CaptureScriptingDefines(out var available, out _, out _, out var current);
            if (!available)
            {
                throw new InvalidOperationException("Scripting Define Symbols are unavailable for the active build target.");
            }

            var existing = new System.Collections.Generic.HashSet<string>(current, StringComparer.Ordinal);
            var merged = new System.Collections.Generic.List<string>(current);
            foreach (var symbol in requested ?? Array.Empty<string>())
            {
                if (existing.Add(symbol))
                {
                    merged.Add(symbol);
                }
            }

            SetScriptingDefines(merged.ToArray());
        }

        private static void SetScriptingDefines(string[] symbols)
        {
            var group = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            if (group == BuildTargetGroup.Unknown)
            {
                throw new InvalidOperationException("Scripting Define Symbols are unavailable for the active build target.");
            }

            PlayerSettings.SetScriptingDefineSymbols(
                NamedBuildTarget.FromBuildTargetGroup(group),
                string.Join(";", symbols ?? Array.Empty<string>()));
        }

        private static string[] SplitScriptingDefines(string value)
        {
            return string.IsNullOrEmpty(value)
                ? Array.Empty<string>()
                : value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static void ApplyPlayModeStartScene(ProjectSetupSceneReference sceneReference)
        {
            if (sceneReference == null || sceneReference.IsEmpty)
            {
                EditorSceneManager.playModeStartScene = null;
                return;
            }

            if (!sceneReference.TryResolve(out var path))
            {
                throw new InvalidOperationException("The Play Mode Start Scene could not be resolved.");
            }

            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            if (scene == null)
            {
                throw new InvalidOperationException("The Play Mode Start Scene is not a Scene Asset.");
            }

            EditorSceneManager.playModeStartScene = scene;
        }

        private static EditorBuildSettingsScene[] ToEditorBuildSettingsScenes(ProjectSetupBuildScene[] scenes)
        {
            var result = new EditorBuildSettingsScene[scenes?.Length ?? 0];
            for (var index = 0; index < result.Length; index++)
            {
                if (scenes[index] == null || !scenes[index].TryResolve(out var path))
                {
                    throw new InvalidOperationException($"Build Scene {index + 1} could not be resolved.");
                }

                result[index] = new EditorBuildSettingsScene(path, scenes[index].Enabled);
            }

            return result;
        }

        private static EditorBuildSettingsScene[] ToEditorBuildSettingsScenes(ProjectSetupBuildSceneState[] scenes)
        {
            return Array.ConvertAll(
                scenes ?? Array.Empty<ProjectSetupBuildSceneState>(),
                scene =>
                {
                    var guidPath = string.IsNullOrEmpty(scene.SceneGuid)
                        ? string.Empty
                        : NormalizePath(AssetDatabase.GUIDToAssetPath(scene.SceneGuid));
                    var path = string.IsNullOrEmpty(guidPath) ? scene.Path : guidPath;
                    return new EditorBuildSettingsScene(path, scene.Enabled);
                });
        }

        private static void ApplyBuildScenes(EditorBuildSettingsScene[] scenes)
        {
            var activeProfile = BuildProfile.GetActiveBuildProfile();
            if (activeProfile != null && activeProfile.overrideGlobalScenes)
            {
                Undo.RecordObject(activeProfile, "Apply Project Setup Build Scenes");
                activeProfile.scenes = scenes;
                EditorUtility.SetDirty(activeProfile);
                return;
            }

            if (activeProfile != null)
            {
                EditorBuildSettings.globalScenes = scenes;
                return;
            }

            EditorBuildSettings.scenes = scenes;
        }

        private static void CaptureProjectFolders(out string[] folders, out string[] assetPaths)
        {
            assetPaths = AssetDatabase.GetAllAssetPaths()
                .Select(NormalizePath)
                .Where(path => string.Equals(path, "Assets", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            folders = assetPaths
                .Where(AssetDatabase.IsValidFolder)
                .ToArray();
        }

        private static string[] CreateProjectFolders(string[] paths)
        {
            var created = new System.Collections.Generic.List<string>();
            foreach (var path in paths ?? Array.Empty<string>())
            {
                if (AssetDatabase.IsValidFolder(path))
                {
                    continue;
                }

                var separator = path.LastIndexOf('/');
                var parent = path.Substring(0, separator);
                var name = path.Substring(separator + 1);
                var guid = AssetDatabase.CreateFolder(parent, name);
                var createdPath = NormalizePath(AssetDatabase.GUIDToAssetPath(guid));
                if (!string.Equals(createdPath, path, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(createdPath))
                    {
                        AssetDatabase.DeleteAsset(createdPath);
                    }

                    throw new InvalidOperationException($"Unity could not create the exact folder path '{path}'.");
                }

                created.Add(path);
            }

            return created.ToArray();
        }

        private static ProjectSetupCreatedAsset[] CreateAssemblyDefinitions(ProjectSetupAssemblyDefinitionPlan[] plans)
        {
            var created = new List<ProjectSetupCreatedAsset>();
            foreach (var plan in plans ?? Array.Empty<ProjectSetupAssemblyDefinitionPlan>())
            {
                var fullPath = GetFullAssetPath(plan.Path);
                if (File.Exists(fullPath) || Directory.Exists(fullPath))
                {
                    throw new InvalidOperationException($"The Assembly Definition target '{plan.Path}' already exists.");
                }

                try
                {
                    using (var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        writer.Write(plan.Content);
                        writer.Flush();
                        stream.Flush(true);
                    }

                    AssetDatabase.ImportAsset(plan.Path, ImportAssetOptions.ForceSynchronousImport);
                    if (!File.Exists(fullPath))
                    {
                        throw new InvalidOperationException($"Unity could not import the Assembly Definition '{plan.Path}'.");
                    }

                    var asset = new ProjectSetupCreatedAsset(
                        plan.Path,
                        ProjectSetupAssemblyDefinitionUtility.ComputeContentHash(File.ReadAllBytes(fullPath)));
                    created.Add(asset);
                }
                catch
                {
                    if (File.Exists(fullPath))
                    {
                        if (!AssetDatabase.DeleteAsset(plan.Path))
                        {
                            File.Delete(fullPath);
                            var metaPath = fullPath + ".meta";
                            if (File.Exists(metaPath))
                            {
                                File.Delete(metaPath);
                            }

                            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                        }
                    }

                    throw;
                }
            }

            return created.ToArray();
        }

        private static void RestoreCreatedProjectAssets(ProjectSetupCreatedAsset[] createdAssets)
        {
            foreach (var asset in createdAssets ?? Array.Empty<ProjectSetupCreatedAsset>())
            {
                var fullPath = GetFullAssetPath(asset.Path);
                if (!File.Exists(fullPath))
                {
                    continue;
                }

                var currentHash = ProjectSetupAssemblyDefinitionUtility.ComputeContentHash(File.ReadAllBytes(fullPath));
                if (!string.Equals(currentHash, asset.ContentHash, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!AssetDatabase.DeleteAsset(asset.Path))
                {
                    throw new InvalidOperationException($"Unity could not remove the unchanged Assembly Definition '{asset.Path}'.");
                }
            }
        }

        private static void RestoreCreatedProjectFolders(string[] createdFolders)
        {
            CaptureProjectFolders(out var currentFolders, out var currentAssetPaths);
            var removable = ProjectSetupFolderUtility.GetRestorableFolders(createdFolders, currentFolders, currentAssetPaths);
            foreach (var path in removable)
            {
                if (!AssetDatabase.IsValidFolder(path) || !IsDirectoryEmpty(path))
                {
                    continue;
                }

                if (!AssetDatabase.DeleteAsset(path))
                {
                    throw new InvalidOperationException($"Unity could not remove the empty folder '{path}'.");
                }
            }
        }

        private static bool IsDirectoryEmpty(string assetPath)
        {
            var projectRoot = Path.GetDirectoryName(UnityEngine.Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
            {
                return false;
            }

            var fullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
            return Directory.Exists(fullPath) && !Directory.EnumerateFileSystemEntries(fullPath).Any();
        }

        private static string GetFullAssetPath(string assetPath)
        {
            var projectRoot = GetProjectRoot();
            var fullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
            var prefix = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Asset path '{assetPath}' escapes the Unity project.");
            }

            var parent = Path.GetDirectoryName(fullPath);
            while (!string.IsNullOrEmpty(parent) && parent.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                if (Directory.Exists(parent)
                    && (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidOperationException($"Asset path '{assetPath}' crosses a reparse point.");
                }

                parent = Path.GetDirectoryName(parent);
            }

            return fullPath;
        }

        private static string GetProjectRoot()
        {
            return Path.GetFullPath(Path.GetDirectoryName(UnityEngine.Application.dataPath)
                ?? throw new InvalidOperationException("The Unity project root is unavailable."));
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }
    }
}
