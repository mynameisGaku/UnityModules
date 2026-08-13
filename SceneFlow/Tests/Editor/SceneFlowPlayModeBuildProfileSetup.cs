using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;

namespace SceneFlow.Tests.PlayMode
{
    /// <summary>実SceneManager回帰に必要な3 Sceneを現在のBuild Profileへ明示登録する。</summary>
    public static class SceneFlowPlayModeBuildProfileSetup
    {
        /// <summary>既存の順序を維持し、不足Sceneを追加して無効な対象Sceneを有効にする。</summary>
        [MenuItem("Tools/Scene Flow/Register PlayMode Test Scenes")]
        public static void Register()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Scene Flow Tests] Play Modeを終了してからSceneを登録してください。");
                return;
            }

            if (!SceneFlowPlayModeSceneAssetPaths.TryResolve(out var scenePaths, out var resolveError))
            {
                Debug.LogError($"[Scene Flow Tests] {resolveError}");
                return;
            }

            var activeProfile = BuildProfile.GetActiveBuildProfile();
            var currentScenes = GetEffectiveScenes(activeProfile);
            var updatedScenes = AddOrEnableScenes(currentScenes, scenePaths, out var changed);
            if (changed) ApplyScenes(activeProfile, updatedScenes);

            Debug.Log(changed
                ? "[Scene Flow Tests] 3 Sceneを現在のBuild Profileで利用できる状態にしました。"
                : "[Scene Flow Tests] 3 Sceneは現在のBuild Profileへ登録済みです。");
        }

        /// <summary>現在の配置にあるテストSceneだけを除去し、他のScene順序と有効状態を維持する。</summary>
        [MenuItem("Tools/Scene Flow/Unregister PlayMode Test Scenes")]
        public static void Unregister()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Scene Flow Tests] Play Modeを終了してからScene登録を解除してください。");
                return;
            }

            if (!SceneFlowPlayModeSceneAssetPaths.TryResolve(out var scenePaths, out var resolveError))
            {
                Debug.LogError($"[Scene Flow Tests] {resolveError}");
                return;
            }

            var activeProfile = BuildProfile.GetActiveBuildProfile();
            var currentScenes = GetEffectiveScenes(activeProfile);
            var updatedScenes = RemoveScenes(currentScenes, scenePaths, out var changed);
            if (changed) ApplyScenes(activeProfile, updatedScenes);

            Debug.Log(changed
                ? "[Scene Flow Tests] 3 SceneのBuild Profile登録を解除しました。"
                : "[Scene Flow Tests] 解除対象のScene登録はありませんでした。");
        }

        /// <summary>現在のBuild Profileが実際に使うScene一覧を返す。</summary>
        private static EditorBuildSettingsScene[] GetEffectiveScenes(BuildProfile activeProfile)
        {
            if (activeProfile != null)
            {
                var profileScenes = activeProfile.GetScenesForBuild();
                if (profileScenes != null) return profileScenes;
            }

            return EditorBuildSettings.scenes ?? Array.Empty<EditorBuildSettingsScene>();
        }

        /// <summary>現在の設定所有先へ更新後のScene一覧を反映する。</summary>
        private static void ApplyScenes(BuildProfile activeProfile, EditorBuildSettingsScene[] scenes)
        {
            if (activeProfile != null && activeProfile.overrideGlobalScenes)
            {
                Undo.RecordObject(activeProfile, "Register Scene Flow PlayMode Test Scenes");
                activeProfile.scenes = scenes;
                EditorUtility.SetDirty(activeProfile);
                AssetDatabase.SaveAssets();
                return;
            }

            if (activeProfile != null)
            {
                EditorBuildSettings.globalScenes = scenes;
                return;
            }

            EditorBuildSettings.scenes = scenes;
        }

        /// <summary>既存順序を保ち、対象Sceneだけを有効化または末尾追加する。</summary>
        private static EditorBuildSettingsScene[] AddOrEnableScenes(EditorBuildSettingsScene[] currentScenes, IReadOnlyList<string> requiredPaths, out bool changed)
        {
            var scenes = new List<EditorBuildSettingsScene>(currentScenes ?? Array.Empty<EditorBuildSettingsScene>());
            changed = false;

            for (var pathIndex = 0; pathIndex < requiredPaths.Count; pathIndex++)
            {
                var path = requiredPaths[pathIndex];
                var existingIndex = FindSceneIndex(scenes, path);
                if (existingIndex < 0)
                {
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                    changed = true;
                    continue;
                }

                if (scenes[existingIndex].enabled) continue;
                scenes[existingIndex] = new EditorBuildSettingsScene(path, true);
                changed = true;
            }

            return scenes.ToArray();
        }

        /// <summary>大文字小文字を区別せず、指定Sceneの現在位置を返す。</summary>
        private static int FindSceneIndex(IReadOnlyList<EditorBuildSettingsScene> scenes, string path)
        {
            for (var i = 0; i < scenes.Count; i++)
            {
                if (string.Equals(NormalizePath(scenes[i].path), path, StringComparison.OrdinalIgnoreCase)) return i;
            }

            return -1;
        }

        /// <summary>対象Sceneだけを除き、それ以外の順序と有効状態を保つ。</summary>
        private static EditorBuildSettingsScene[] RemoveScenes(EditorBuildSettingsScene[] currentScenes, IReadOnlyList<string> removedPaths, out bool changed)
        {
            var current = currentScenes ?? Array.Empty<EditorBuildSettingsScene>();
            var scenes = new List<EditorBuildSettingsScene>(current.Length);
            changed = false;

            for (var sceneIndex = 0; sceneIndex < current.Length; sceneIndex++)
            {
                var scene = current[sceneIndex];
                if (FindPathIndex(removedPaths, scene.path) >= 0)
                {
                    changed = true;
                    continue;
                }

                scenes.Add(scene);
            }

            return scenes.ToArray();
        }

        /// <summary>候補完全パスから指定Sceneの位置を返す。</summary>
        private static int FindPathIndex(IReadOnlyList<string> paths, string targetPath)
        {
            for (var i = 0; i < paths.Count; i++)
            {
                if (string.Equals(paths[i], NormalizePath(targetPath), StringComparison.OrdinalIgnoreCase)) return i;
            }

            return -1;
        }

        private static string NormalizePath(string path) => string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
    }
}
