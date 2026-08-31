// SPDX-License-Identifier: MIT

namespace BuildGuard.Editor
{
    /// <summary>
    /// 読込済みシーン1件のプレハブ構造差分検査に上限を設けます。
    /// </summary>
    internal readonly struct BuildGuardPrefabOverrideScanLimits
    {
        internal const int DefaultMaxVisitedGameObjects = 250000;
        internal const int DefaultMaxPrefabInstances = 25000;
        internal const int DefaultMaxFindings = 10000;

        internal BuildGuardPrefabOverrideScanLimits(
            int maxVisitedGameObjects,
            int maxPrefabInstances,
            int maxFindings)
        {
            MaxVisitedGameObjects = maxVisitedGameObjects;
            MaxPrefabInstances = maxPrefabInstances;
            MaxFindings = maxFindings;
        }

        internal static BuildGuardPrefabOverrideScanLimits Default => new BuildGuardPrefabOverrideScanLimits(
            DefaultMaxVisitedGameObjects,
            DefaultMaxPrefabInstances,
            DefaultMaxFindings);

        internal int MaxVisitedGameObjects { get; }

        internal int MaxPrefabInstances { get; }

        internal int MaxFindings { get; }

        /// <summary>すべての上限が1件以上を受け付けられるか確認します。</summary>
        internal bool TryValidate(out string errorMessage)
        {
            if (MaxVisitedGameObjects <= 0)
            {
                errorMessage = "検査するゲームオブジェクト数の上限は1以上である必要があります。";
                return false;
            }

            if (MaxPrefabInstances <= 0)
            {
                errorMessage = "検査するプレハブ実体数の上限は1以上である必要があります。";
                return false;
            }

            if (MaxFindings <= 0)
            {
                errorMessage = "取得する構造差分数の上限は1以上である必要があります。";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
