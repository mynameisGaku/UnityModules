// SPDX-License-Identifier: MIT

using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;

namespace ProjectSetup.Editor
{
    internal sealed class UnityProjectSetupEnvironment : IProjectSetupEnvironment
    {
        public bool IsAvailable => !EditorApplication.isPlayingOrWillChangePlaymode
            && !EditorApplication.isCompiling
            && !EditorApplication.isUpdating;

        public ProjectSetupSnapshot Capture()
        {
            ProjectSetupTagManagerStore.Capture(out var tags, out var customTags, out var layers, out var sortingLayers, out var tagManagerFileText);
            CaptureBuildScenes(out var buildSceneTargetId, out var buildSceneTargetLabel, out var buildScenes);
            CapturePlayModeStartScene(out var playModeStartSceneGuid, out var playModeStartScenePath);
            CaptureScriptingDefines(out var hasScriptingDefineData, out var scriptingDefineTargetId, out var scriptingDefineTargetLabel, out var scriptingDefineSymbols);
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
                EditorSettings.assetNamingUsesSpace);
        }

        public void Apply(ProjectSetupProfile profile)
        {
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

            ProjectSetupTagManagerStore.Apply(profile);
            AssetDatabase.SaveAssets();
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

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }
    }
}
