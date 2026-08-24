// SPDX-License-Identifier: MIT

namespace BuildGuard.Editor
{
    /// <summary>
    /// Identifies one structural Prefab instance override.
    /// </summary>
    internal enum BuildGuardPrefabOverrideKind
    {
        AddedGameObject = 0,
        RemovedGameObject = 1,
        AddedComponent = 2,
        RemovedComponent = 3,
    }
}
