// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace BuildGuard.Editor
{
    /// <summary>
    /// シーンの読み込み状態と有効シーンを保ちながら、シーンアセットのパスを順に処理します。
    /// </summary>
    internal static class BuildGuardScenePathVisitor
    {
        /// <summary>有効かつ重複しない各シーンパスを、指定された順に処理します。</summary>
        internal static int Visit(
            IReadOnlyList<string> scenePaths,
            Func<int, int, string, bool> shouldCancel,
            Action<Scene> visitor,
            out bool cancelled)
        {
            if (scenePaths == null)
            {
                throw new ArgumentNullException(nameof(scenePaths));
            }

            if (visitor == null)
            {
                throw new ArgumentNullException(nameof(visitor));
            }

            var paths = NormalizePaths(scenePaths);
            var originalActiveScene = SceneManager.GetActiveScene();
            var visitedCount = 0;
            cancelled = false;

            try
            {
                for (var index = 0; index < paths.Count; index++)
                {
                    var scenePath = paths[index];
                    if (shouldCancel != null && shouldCancel(index, paths.Count, scenePath))
                    {
                        cancelled = true;
                        break;
                    }

                    var scene = SceneManager.GetSceneByPath(scenePath);
                    var closeAfterVisit = !scene.IsValid() || !scene.isLoaded;
                    if (closeAfterVisit)
                    {
                        scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    }

                    try
                    {
                        visitor(scene);
                        visitedCount++;
                    }
                    finally
                    {
                        if (closeAfterVisit && scene.IsValid() && scene.isLoaded)
                        {
                            if (!EditorSceneManager.CloseScene(scene, true))
                            {
                                throw new InvalidOperationException($"検査のために開いたシーンを閉じられませんでした: {scenePath}");
                            }
                        }
                    }
                }
            }
            finally
            {
                if (originalActiveScene.IsValid()
                    && originalActiveScene.isLoaded
                    && SceneManager.GetActiveScene() != originalActiveScene
                    && !SceneManager.SetActiveScene(originalActiveScene))
                {
                    throw new InvalidOperationException("元の有効シーンを復元できませんでした。");
                }
            }

            return visitedCount;
        }

        private static List<string> NormalizePaths(IReadOnlyList<string> scenePaths)
        {
            var result = new List<string>(scenePaths.Count);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < scenePaths.Count; index++)
            {
                var scenePath = scenePaths[index]?.Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(scenePath)
                    || !visited.Add(scenePath)
                    || AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                {
                    continue;
                }

                result.Add(scenePath);
            }

            return result;
        }
    }
}
