// SPDX-License-Identifier: MIT

namespace BuildGuard.Editor
{
    /// <summary>
    /// プレハブ実体にある1件の構造差分の種類を表します。
    /// </summary>
    internal enum BuildGuardPrefabOverrideKind
    {
        AddedGameObject = 0,
        RemovedGameObject = 1,
        AddedComponent = 2,
        RemovedComponent = 3,
    }
}
