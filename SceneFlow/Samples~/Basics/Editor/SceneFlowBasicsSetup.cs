using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SceneFlow.Samples.Editor
{
    /// <summary>Basics用Sceneだけを現在のBuild Profileへ追加し、Bootstrapを開く明示設定。</summary>
    public static class SceneFlowBasicsSetup
    {
        private const string SetupScriptGuid = "96cf482400534b6fa555715f77957df7";
        private const string BootstrapFileName = "SceneFlowBasicsBootstrap.unity";
        private const string TargetAFileName = "SceneFlowBasicsTargetA.unity";
        private const string TargetBFileName = "SceneFlowBasicsTargetB.unity";

        /// <summary>
        /// 現在の実効Scene一覧を維持したまま不足Sceneを末尾へ追加し、Bootstrapを開く。
        /// 既にあるサンプルSceneが無効な場合は同じ位置で有効にする。
        /// </summary>
        [MenuItem("Tools/Scene Flow/Setup Basics Sample")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Scene Flow Basics] Play Modeを終了してからSetupしてください。");
                return;
            }

            if (!TryGetScenePaths(out var scenePaths)) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var activeProfile = BuildProfile.GetActiveBuildProfile();
            var currentScenes = GetEffectiveScenes(activeProfile);
            var updatedScenes = AddOrEnableSampleScenes(currentScenes, scenePaths, out var changed);
            if (changed) ApplyScenes(activeProfile, updatedScenes);

            var bootstrap = EditorSceneManager.OpenScene(scenePaths[0], OpenSceneMode.Single);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(bootstrap.path);
            Debug.Log(changed
                ? "[Scene Flow Basics] 3 Sceneを現在のBuild Profileで利用できる状態にし、Bootstrapを開きました。"
                : "[Scene Flow Basics] Scene一覧は設定済みです。Bootstrapを開きました。");
        }

        /// <summary>現在のBuild Profileが実際に使うScene一覧を返す。</summary>
        /// <param name="activeProfile">現在選択されているBuild Profile。platform profileではnull。</param>
        /// <returns>順序と有効状態を含む実効Scene一覧。</returns>
        private static EditorBuildSettingsScene[] GetEffectiveScenes(BuildProfile activeProfile)
        {
            if (activeProfile != null)
            {
                var profileScenes = activeProfile.GetScenesForBuild();
                if (profileScenes != null) return profileScenes;
            }

            return EditorBuildSettings.scenes ?? Array.Empty<EditorBuildSettingsScene>();
        }

        /// <summary>現在の設定所有先へScene一覧を反映する。</summary>
        /// <param name="activeProfile">現在選択されているBuild Profile。platform profileではnull。</param>
        /// <param name="scenes">既存順序を保って更新したScene一覧。</param>
        private static void ApplyScenes(BuildProfile activeProfile, EditorBuildSettingsScene[] scenes)
        {
            if (activeProfile != null && activeProfile.overrideGlobalScenes)
            {
                Undo.RecordObject(activeProfile, "Setup Scene Flow Basics");
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

        /// <summary>既存順序を維持し、サンプルSceneだけを有効化または末尾追加する。</summary>
        /// <param name="currentScenes">現在の実効Scene一覧。</param>
        /// <param name="samplePaths">Bootstrap、Target A、Target Bの順のパス。</param>
        /// <param name="changed">Scene一覧を変更した場合はtrue。</param>
        /// <returns>再実行しても内容が変わらない更新後一覧。</returns>
        private static EditorBuildSettingsScene[] AddOrEnableSampleScenes(
            EditorBuildSettingsScene[] currentScenes,
            IReadOnlyList<string> samplePaths,
            out bool changed)
        {
            var scenes = new List<EditorBuildSettingsScene>(currentScenes ?? Array.Empty<EditorBuildSettingsScene>());
            changed = false;

            for (var sampleIndex = 0; sampleIndex < samplePaths.Count; sampleIndex++)
            {
                var samplePath = samplePaths[sampleIndex];
                var existingIndex = FindSceneIndex(scenes, samplePath);
                if (existingIndex < 0)
                {
                    scenes.Add(new EditorBuildSettingsScene(samplePath, true));
                    changed = true;
                    continue;
                }

                if (scenes[existingIndex].enabled) continue;
                scenes[existingIndex] = new EditorBuildSettingsScene(samplePath, true);
                changed = true;
            }

            return scenes.ToArray();
        }

        private static int FindSceneIndex(IReadOnlyList<EditorBuildSettingsScene> scenes, string path)
        {
            for (var i = 0; i < scenes.Count; i++)
            {
                if (string.Equals(NormalizePath(scenes[i].path), path, StringComparison.OrdinalIgnoreCase)) return i;
            }

            return -1;
        }

        private static bool TryGetScenePaths(out string[] scenePaths)
        {
            var setupPath = NormalizePath(AssetDatabase.GUIDToAssetPath(SetupScriptGuid));
            var editorDirectory = Path.GetDirectoryName(setupPath)?.Replace('\\', '/');
            var basicsDirectory = string.IsNullOrEmpty(editorDirectory)
                ? string.Empty
                : Path.GetDirectoryName(editorDirectory)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(basicsDirectory))
            {
                Debug.LogError("[Scene Flow Basics] Sampleの配置先を解決できません。Sampleを再Importしてください。");
                scenePaths = Array.Empty<string>();
                return false;
            }

            scenePaths = new[]
            {
                basicsDirectory + "/" + BootstrapFileName,
                basicsDirectory + "/" + TargetAFileName,
                basicsDirectory + "/" + TargetBFileName,
            };

            for (var i = 0; i < scenePaths.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePaths[i]) != null) continue;
                Debug.LogError($"[Scene Flow Basics] Sceneがありません: {scenePaths[i]}");
                return false;
            }

            return true;
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }
    }
}
