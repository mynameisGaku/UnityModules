using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SceneWorkspace.Editor
{
    /// <summary>このモジュールが所有するUnityエディターのシーン管理構成だけを読み取り、復元します。</summary>
    internal sealed class UnitySceneWorkspaceGateway : ISceneWorkspaceGateway
    {
        /// <summary>エディター状態、シーン順、読込状態、使用中状態、未保存状態を取得します。</summary>
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

        /// <summary>設定アセットの識別情報と順序付き目標構成を独立した値へ変換します。</summary>
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

        /// <summary>指定構成の順番、読込状態、使用中状態をUnityエディターへ復元します。</summary>
        public void RestoreSetup(IReadOnlyList<SceneWorkspaceSceneState> scenes)
        {
            if (scenes == null)
                throw new ArgumentNullException(nameof(scenes), "復元するシーン構成を指定してください。");

            var setup = scenes.Select(scene => new SceneSetup
            {
                path = scene.Path,
                isLoaded = scene.Loaded,
                isActive = scene.Active
            }).ToArray();
            EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        /// <summary>開いているシーンをパスごとの取得順に保持し、未保存状態の照合に使います。</summary>
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

        /// <summary>指定パスで次の開いているシーンを取り出します。見つからない場合は失敗します。</summary>
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
