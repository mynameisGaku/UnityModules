// SPDX-License-Identifier: MIT

namespace BuildGuard.Editor
{
    /// <summary>
    /// プレハブ構造差分の検査が完全な結果を作れなかった原因を表します。
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
