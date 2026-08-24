// SPDX-License-Identifier: MIT

namespace BuildGuard.Editor
{
    /// <summary>
    /// Identifies why a structural Prefab override scan could not produce a complete result.
    /// </summary>
    internal enum BuildGuardPrefabOverrideScanError
    {
        None = 0,
        InvalidScene = 1,
        SceneNotLoaded = 2,
        InvalidLimits = 3,
        UnsupportedPrefabInstanceStatus = 4,
        MissingPrefabSource = 5,
        TooManyGameObjects = 6,
        TooManyPrefabInstances = 7,
        TooManyFindings = 8,
        UnityApiFailure = 9,
    }
}
