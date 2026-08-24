// SPDX-License-Identifier: MIT

namespace BuildGuard.Editor
{
    /// <summary>
    /// Identifies the safe navigation action taken after refreshing one finding.
    /// </summary>
    internal enum BuildGuardPrefabOverrideNavigationOutcome
    {
        SelectedSceneObject = 0,
        PingedSceneAsset = 1,
        Stale = 2,
        SceneUnavailable = 3,
        ScanFailed = 4,
        SceneStateRestoreFailed = 5
    }
}
