// SPDX-License-Identifier: MIT

using System;
using UnityEditor;
using UnityEditor.Build.Profile;

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
                buildScenes);
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

            EditorSettings.serializationMode = snapshot.AssetSerialization;
            VersionControlSettings.mode = snapshot.VersionControlMode;
            EditorSettings.enterPlayModeOptionsEnabled = snapshot.EnterPlayModeOptionsEnabled;
            EditorSettings.enterPlayModeOptions = snapshot.EnterPlayModeOptions;
            PlayerSettings.colorSpace = snapshot.ColorSpace;
            PlayerSettings.runInBackground = snapshot.RunInBackground;
            PlayerSettings.companyName = snapshot.CompanyName;
            PlayerSettings.productName = snapshot.ProductName;
            PlayerSettings.bundleVersion = snapshot.BundleVersion;
            if (snapshot.HasBuildSceneData)
            {
                ApplyBuildScenes(ToEditorBuildSettingsScenes(snapshot.BuildScenes));
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
