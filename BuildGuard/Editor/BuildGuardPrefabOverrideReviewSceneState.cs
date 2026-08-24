// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;

namespace BuildGuard.Editor
{
    /// <summary>
    /// Stores the exact loaded Scene order, active Scene and dirty flags around a review scan.
    /// </summary>
    internal readonly struct BuildGuardPrefabOverrideReviewSceneState
    {
        internal BuildGuardPrefabOverrideReviewSceneState(
            IReadOnlyList<SceneEntry> scenes,
            ulong activeSceneHandle,
            string activeScenePath)
        {
            if (scenes == null)
            {
                throw new ArgumentNullException(nameof(scenes));
            }

            var snapshot = new SceneEntry[scenes.Count];
            for (var index = 0; index < scenes.Count; index++)
            {
                snapshot[index] = scenes[index];
            }

            Scenes = Array.AsReadOnly(snapshot);
            ActiveSceneHandle = activeSceneHandle;
            ActiveScenePath = activeScenePath ?? string.Empty;
        }

        internal IReadOnlyList<SceneEntry> Scenes { get; }

        internal ulong ActiveSceneHandle { get; }

        internal string ActiveScenePath { get; }

        /// <summary>Compares two captured states without reading or changing Unity state.</summary>
        internal static bool TryValidate(
            BuildGuardPrefabOverrideReviewSceneState expected,
            BuildGuardPrefabOverrideReviewSceneState current,
            out string message)
        {
            if (expected.Scenes == null || current.Scenes == null)
            {
                message = "Loaded Scene state capture was incomplete.";
                return false;
            }

            if (expected.Scenes.Count != current.Scenes.Count)
            {
                message = $"Loaded Scene count changed from {expected.Scenes.Count} to {current.Scenes.Count}.";
                return false;
            }

            for (var index = 0; index < expected.Scenes.Count; index++)
            {
                var expectedScene = expected.Scenes[index];
                var currentScene = current.Scenes[index];
                if (expectedScene.Handle != currentScene.Handle)
                {
                    message = $"Loaded Scene handle or order changed at index {index}.";
                    return false;
                }

                if (!string.Equals(expectedScene.Path, currentScene.Path, StringComparison.Ordinal))
                {
                    message = $"Loaded Scene path changed at index {index}.";
                    return false;
                }

                if (expectedScene.IsDirty != currentScene.IsDirty)
                {
                    var identity = string.IsNullOrEmpty(expectedScene.Path)
                        ? $"handle {expectedScene.Handle}"
                        : expectedScene.Path;
                    message = $"Loaded Scene dirty state changed for {identity}.";
                    return false;
                }
            }

            if (expected.ActiveSceneHandle != current.ActiveSceneHandle
                || !string.Equals(
                    expected.ActiveScenePath,
                    current.ActiveScenePath,
                    StringComparison.Ordinal))
            {
                message = "Active Scene changed during the review scan.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        /// <summary>Stores one loaded Scene identity and its unsaved-change flag.</summary>
        internal readonly struct SceneEntry
        {
            internal SceneEntry(ulong handle, string path, bool isDirty)
            {
                Handle = handle;
                Path = path ?? string.Empty;
                IsDirty = isDirty;
            }

            internal ulong Handle { get; }

            internal string Path { get; }

            internal bool IsDirty { get; }
        }
    }
}
