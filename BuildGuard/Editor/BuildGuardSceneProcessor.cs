// SPDX-License-Identifier: MIT

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace BuildGuard.Editor
{
    /// <summary>
    /// Validates every Scene processed by a Player build and blocks missing references.
    /// </summary>
    [BuildCallbackVersion(1)]
    internal sealed class BuildGuardSceneProcessor : IProcessSceneWithReport
    {
        /// <summary>
        /// Runs before ordinary Scene processing callbacks.
        /// </summary>
        internal const int CallbackOrder = -10000;

        /// <summary>
        /// Gets the Unity build callback order.
        /// </summary>
        public int callbackOrder => CallbackOrder;

        /// <summary>
        /// Validates only Player build processing and ignores ordinary Play Mode loads.
        /// </summary>
        /// <param name="scene">The Scene Unity is processing for a build.</param>
        /// <param name="report">The Player build report, or null outside a Player build.</param>
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null || !BuildPipeline.isBuildingPlayer)
            {
                return;
            }

            ValidateScene(scene);
        }

        /// <summary>
        /// Validates one loaded Scene and throws when any build-blocking issue exists.
        /// </summary>
        internal static void ValidateScene(Scene scene)
        {
            var findings = MissingScriptSceneScanner.Scan(scene);
            var missingObjectReferences = MissingObjectReferenceSceneScanner.Scan(scene);
            if (findings.Count == 0 && missingObjectReferences.Count == 0)
            {
                return;
            }

            throw new BuildFailedException(BuildGuardMessageFormatter.Format(
                scene,
                findings,
                missingObjectReferences));
        }
    }
}
