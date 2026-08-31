// SPDX-License-Identifier: MIT

namespace BuildGuard.Editor
{
    /// <summary>
    /// 1件の差分を再検査した後に行った、安全な移動結果を表します。
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
