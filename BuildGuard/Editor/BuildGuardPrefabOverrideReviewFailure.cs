// SPDX-License-Identifier: MIT

namespace BuildGuard.Editor
{
    /// <summary>
    /// Describes one Scene whose structural Prefab override snapshot could not be produced.
    /// </summary>
    internal readonly struct BuildGuardPrefabOverrideReviewFailure
    {
        internal BuildGuardPrefabOverrideReviewFailure(
            string scenePath,
            BuildGuardPrefabOverrideScanError error,
            string message)
        {
            ScenePath = scenePath ?? string.Empty;
            Error = error;
            Message = message ?? string.Empty;
        }

        internal string ScenePath { get; }

        internal BuildGuardPrefabOverrideScanError Error { get; }

        internal string Message { get; }
    }
}
