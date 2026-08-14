// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildGuard.Editor
{
    /// <summary>
    /// incremental buildのcontent再利用に関係なく、予定された全Player Sceneをbuild開始前に検査します。
    /// </summary>
    internal sealed class BuildGuardPreflightProcessor : BuildPlayerProcessor
    {
        /// <summary>
        /// 他のbuild準備処理より先に検査するためのcallback順序を取得します。
        /// </summary>
        public override int callbackOrder => BuildGuardSceneProcessor.CallbackOrder;

        /// <summary>
        /// 実際に予定されたScene pathを取得し、Missing Scriptがあればbuildを中止します。
        /// </summary>
        /// <param name="buildPlayerContext">Unityが予定したPlayer build context。</param>
        public override void PrepareForBuild(BuildPlayerContext buildPlayerContext)
        {
            if (buildPlayerContext == null)
            {
                throw new ArgumentNullException(nameof(buildPlayerContext));
            }

            ValidateScenePaths(buildPlayerContext.BuildPlayerOptions.scenes);
        }

        /// <summary>
        /// 指定Scene pathを順番に開いて検査し、元のScene開閉状態を維持します。
        /// </summary>
        /// <param name="scenePaths">Player buildへ渡されたScene asset path。</param>
        internal static void ValidateScenePaths(IReadOnlyList<string> scenePaths)
        {
            if (scenePaths == null)
            {
                throw new ArgumentNullException(nameof(scenePaths));
            }

            var originalActiveScene = SceneManager.GetActiveScene();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                for (var index = 0; index < scenePaths.Count; index++)
                {
                    var scenePath = scenePaths[index]?.Replace('\\', '/');
                    if (string.IsNullOrWhiteSpace(scenePath) || !visited.Add(scenePath))
                    {
                        continue;
                    }

                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                    {
                        continue;
                    }

                    var scene = SceneManager.GetSceneByPath(scenePath);
                    var closeAfterValidation = !scene.IsValid() || !scene.isLoaded;
                    if (closeAfterValidation)
                    {
                        scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    }

                    try
                    {
                        BuildGuardSceneProcessor.ValidateScene(scene);
                    }
                    finally
                    {
                        if (closeAfterValidation && scene.IsValid() && scene.isLoaded)
                        {
                            EditorSceneManager.CloseScene(scene, true);
                        }
                    }
                }
            }
            finally
            {
                if (originalActiveScene.IsValid() && originalActiveScene.isLoaded && SceneManager.GetActiveScene() != originalActiveScene)
                {
                    SceneManager.SetActiveScene(originalActiveScene);
                }
            }
        }
    }
}
