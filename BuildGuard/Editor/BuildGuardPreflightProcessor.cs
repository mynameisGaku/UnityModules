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
    /// Validates every scheduled Player Scene before incremental content can be reused.
    /// </summary>
    internal sealed class BuildGuardPreflightProcessor : BuildPlayerProcessor
    {
        /// <summary>
        /// Gets an early callback order for preflight validation.
        /// </summary>
        public override int callbackOrder => BuildGuardSceneProcessor.CallbackOrder;

        /// <summary>
        /// Validates the Scene paths scheduled by the actual Player build request.
        /// </summary>
        /// <param name="buildPlayerContext">The Player build context prepared by Unity.</param>
        public override void PrepareForBuild(BuildPlayerContext buildPlayerContext)
        {
            if (buildPlayerContext == null)
            {
                throw new ArgumentNullException(nameof(buildPlayerContext));
            }

            ValidateScenePaths(buildPlayerContext.BuildPlayerOptions.scenes);
        }

        /// <summary>
        /// Opens each specified Scene in order while preserving the original loaded and active Scene state.
        /// </summary>
        /// <param name="scenePaths">Scene asset paths passed to the Player build.</param>
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
