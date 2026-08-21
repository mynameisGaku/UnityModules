// SPDX-License-Identifier: MIT

using UnityEngine.SceneManagement;

namespace BuildGuard.Editor
{
    /// <summary>
    /// Runs every Build Guard rule against one loaded Scene.
    /// </summary>
    internal static class BuildGuardSceneInspector
    {
        /// <summary>Collects missing scripts and broken serialized object references.</summary>
        internal static BuildGuardSceneInspection Inspect(Scene scene)
        {
            return new BuildGuardSceneInspection(
                MissingScriptSceneScanner.Scan(scene),
                MissingObjectReferenceSceneScanner.Scan(scene));
        }
    }
}
