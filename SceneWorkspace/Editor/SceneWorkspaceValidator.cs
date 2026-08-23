using System;
using System.Collections.Generic;

namespace SceneWorkspace.Editor
{
    /// <summary>Applies the fail-closed editor, scene, and profile boundary before preview or mutation.</summary>
    internal static class SceneWorkspaceValidator
    {
        internal static SceneWorkspaceValidation ValidateCurrent(SceneWorkspaceSnapshot snapshot)
        {
            if (snapshot == null)
                return Failure(SceneWorkspaceError.CaptureFailed, "The current scene setup could not be captured.");
            if (snapshot.PlayModeActive)
                return Failure(SceneWorkspaceError.PlayModeActive, "Exit Play Mode before using Scene Workspace.");
            if (snapshot.Compiling || snapshot.Updating)
                return Failure(SceneWorkspaceError.EditorBusy, "Wait for compilation or asset updating to finish.");
            if (snapshot.PrefabStageOpen)
                return Failure(SceneWorkspaceError.PrefabStageOpen, "Close Prefab Mode before using Scene Workspace.");
            return ValidateScenes(snapshot.Scenes, true);
        }

        internal static SceneWorkspaceValidation ValidateProfile(SceneWorkspaceProfileSnapshot profile)
        {
            if (profile == null || !profile.Exists)
                return Failure(SceneWorkspaceError.InvalidProfile, "Select a workspace profile.");
            if (!IsSupportedAssetPath(profile.Path) || string.IsNullOrEmpty(profile.Guid))
                return Failure(SceneWorkspaceError.ProfileNotSaved, "Save the workspace profile under Assets before previewing it.");
            return ValidateScenes(profile.Scenes, false);
        }

        private static SceneWorkspaceValidation ValidateScenes(IReadOnlyList<SceneWorkspaceSceneState> scenes, bool current)
        {
            if (scenes == null || scenes.Count == 0)
                return Failure(SceneWorkspaceError.NoScenes, current ? "Open at least one saved scene." : "Add at least one saved scene to the profile.");

            var guids = new HashSet<string>(StringComparer.Ordinal);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            var loadedCount = 0;
            var activeCount = 0;
            for (var index = 0; index < scenes.Count; index++)
            {
                var scene = scenes[index];
                if (scene == null || !scene.Exists)
                    return Failure(SceneWorkspaceError.MissingScene, "A scene reference is missing or no longer exists.");
                if (string.IsNullOrEmpty(scene.Path))
                    return Failure(SceneWorkspaceError.UntitledScene, "Save every untitled scene before using Scene Workspace.");
                if (!IsSupportedScenePath(scene.Path))
                    return Failure(SceneWorkspaceError.UnsupportedScenePath, "Every scene must be a .unity asset under Assets.");
                if (string.IsNullOrEmpty(scene.Guid))
                    return Failure(SceneWorkspaceError.MissingScene, "A scene has no valid asset GUID.");
                if (!guids.Add(scene.Guid) || !paths.Add(scene.Path))
                    return Failure(SceneWorkspaceError.DuplicateScene, "The scene setup contains a duplicate scene.");
                if (current && scene.Dirty)
                    return Failure(SceneWorkspaceError.DirtyScene, "Save or revert every modified scene before switching workspaces.");
                if (scene.Loaded)
                    loadedCount++;
                if (scene.Active)
                {
                    activeCount++;
                    if (!scene.Loaded)
                        return Failure(SceneWorkspaceError.InvalidActiveScene, "The active scene must also be loaded.");
                }
            }

            if (loadedCount == 0)
                return Failure(SceneWorkspaceError.NoLoadedScene, "At least one scene must be loaded.");
            if (activeCount != 1)
                return Failure(SceneWorkspaceError.InvalidActiveScene, "Exactly one loaded scene must be active.");
            return SceneWorkspaceValidation.Success;
        }

        private static bool IsSupportedScenePath(string path)
        {
            return IsSupportedAssetPath(path) && path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSupportedAssetPath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal);
        }

        private static SceneWorkspaceValidation Failure(SceneWorkspaceError error, string message)
        {
            return new SceneWorkspaceValidation(error, message);
        }
    }
}
