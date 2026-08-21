// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;

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
            BuildGuardScenePathVisitor.Visit(
                scenePaths,
                null,
                BuildGuardSceneProcessor.ValidateScene,
                out _);
        }
    }
}
