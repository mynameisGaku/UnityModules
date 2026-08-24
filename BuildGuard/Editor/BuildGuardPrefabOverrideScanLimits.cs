// SPDX-License-Identifier: MIT

namespace BuildGuard.Editor
{
    /// <summary>
    /// Bounds one loaded Scene structural Prefab override scan.
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

        /// <summary>Validates that every limit can admit at least one item.</summary>
        internal bool TryValidate(out string errorMessage)
        {
            if (MaxVisitedGameObjects <= 0)
            {
                errorMessage = "MaxVisitedGameObjects must be greater than zero.";
                return false;
            }

            if (MaxPrefabInstances <= 0)
            {
                errorMessage = "MaxPrefabInstances must be greater than zero.";
                return false;
            }

            if (MaxFindings <= 0)
            {
                errorMessage = "MaxFindings must be greater than zero.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
