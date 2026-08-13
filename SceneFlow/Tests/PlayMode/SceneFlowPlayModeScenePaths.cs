using UnityEngine;

namespace SceneFlow.Tests.PlayMode
{
    /// <summary>テストSceneをUPMまたは埋込moduleの現在の配置へ解決する。</summary>
    internal static class SceneFlowPlayModeScenePaths
    {
        private static readonly string[] SceneFileNames =
        {
            "SceneFlowTestHarness.unity",
            "SceneFlowTestTargetA.unity",
            "SceneFlowTestTargetB.unity",
        };

        private static readonly string[] SceneDirectories =
        {
            "Packages/com.studiogaku.scene-flow/Tests/PlayMode/Scenes",
            "Assets/Modules/SceneFlow/Tests/PlayMode/Scenes",
        };

        /// <summary>Harness、Target A、Target Bの順で、現在のBuildへ登録された完全パスを返す。</summary>
        internal static bool TryResolve(out string[] scenePaths, out string error)
        {
            for (var directoryIndex = 0; directoryIndex < SceneDirectories.Length; directoryIndex++)
            {
                var candidatePaths = CreatePaths(SceneDirectories[directoryIndex]);
                var allLoadable = true;
                for (var pathIndex = 0; pathIndex < candidatePaths.Length; pathIndex++)
                {
                    if (Application.CanStreamedLevelBeLoaded(candidatePaths[pathIndex])) continue;
                    allLoadable = false;
                    break;
                }

                if (!allLoadable) continue;
                scenePaths = candidatePaths;
                error = string.Empty;
                return true;
            }

            scenePaths = System.Array.Empty<string>();
            error = "PlayMode用3 SceneがBuild Profileへ同じ配置元から有効登録されていません。Tools/Scene Flow/Register PlayMode Test Scenesを実行してください。";
            return false;
        }

        /// <summary>指定した配置元から3 Sceneの完全パスを作る。</summary>
        private static string[] CreatePaths(string directory)
        {
            var paths = new string[SceneFileNames.Length];
            for (var i = 0; i < SceneFileNames.Length; i++) paths[i] = directory + "/" + SceneFileNames[i];
            return paths;
        }
    }
}
