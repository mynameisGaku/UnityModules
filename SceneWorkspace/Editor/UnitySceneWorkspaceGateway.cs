using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SceneWorkspace.Editor
{
    /// <summary>Reads and restores only the Unity Editor scene-manager setup owned by this module.</summary>
    internal sealed class UnitySceneWorkspaceGateway : ISceneWorkspaceGateway
    {
        public SceneWorkspaceSnapshot CaptureCurrentSetup()
        {
            var dirtyScenes = CaptureLoadedScenesByPath();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var scenes = new SceneWorkspaceSceneState[setup.Length];
            for (var index = 0; index < setup.Length; index++)
            {
                var path = setup[index].path ?? string.Empty;
                var exists = path.Length == 0 || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;
                var guid = path.Length == 0 ? string.Empty : AssetDatabase.AssetPathToGUID(path);
                var dirty = setup[index].isLoaded && TryTakeDirtyScene(dirtyScenes, path, out var scene) && scene.isDirty;
                scenes[index] = new SceneWorkspaceSceneState(index, guid, path, exists, setup[index].isLoaded, setup[index].isActive, dirty);
            }

            return new SceneWorkspaceSnapshot(
                EditorApplication.isPlayingOrWillChangePlaymode,
                EditorApplication.isCompiling,
                EditorApplication.isUpdating,
                PrefabStageUtility.GetCurrentPrefabStage() != null,
                scenes);
        }

        public SceneWorkspaceProfileSnapshot CaptureProfile(SceneWorkspaceProfile profile)
        {
            if (profile == null)
                return new SceneWorkspaceProfileSnapshot(false, string.Empty, string.Empty, string.Empty, Array.Empty<SceneWorkspaceSceneState>());

            var profilePath = AssetDatabase.GetAssetPath(profile) ?? string.Empty;
            var profileGuid = profilePath.Length == 0 ? string.Empty : AssetDatabase.AssetPathToGUID(profilePath);
            var entries = profile.Entries;
            var scenes = new SceneWorkspaceSceneState[entries.Count];
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                var sceneAsset = entry?.Scene;
                var path = sceneAsset == null ? string.Empty : AssetDatabase.GetAssetPath(sceneAsset) ?? string.Empty;
                var exists = sceneAsset != null && path.Length > 0 && AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;
                var guid = exists ? AssetDatabase.AssetPathToGUID(path) : string.Empty;
                scenes[index] = new SceneWorkspaceSceneState(index, guid, path, exists, entry?.Loaded ?? false, entry?.Active ?? false, false);
            }

            return new SceneWorkspaceProfileSnapshot(true, profileGuid, profilePath, profile.name, scenes);
        }

        public void RestoreSetup(IReadOnlyList<SceneWorkspaceSceneState> scenes)
        {
            if (scenes == null)
                throw new ArgumentNullException(nameof(scenes));

            var setup = scenes.Select(scene => new SceneSetup
            {
                path = scene.Path,
                isLoaded = scene.Loaded,
                isActive = scene.Active
            }).ToArray();
            EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        private static Dictionary<string, Queue<Scene>> CaptureLoadedScenesByPath()
        {
            var result = new Dictionary<string, Queue<Scene>>(StringComparer.Ordinal);
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                var path = scene.path ?? string.Empty;
                if (!result.TryGetValue(path, out var scenes))
                {
                    scenes = new Queue<Scene>();
                    result.Add(path, scenes);
                }
                scenes.Enqueue(scene);
            }
            return result;
        }

        private static bool TryTakeDirtyScene(Dictionary<string, Queue<Scene>> scenesByPath, string path, out Scene scene)
        {
            if (scenesByPath.TryGetValue(path, out var scenes) && scenes.Count > 0)
            {
                scene = scenes.Dequeue();
                return true;
            }
            scene = default;
            return false;
        }
    }
}
