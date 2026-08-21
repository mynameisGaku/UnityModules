// SPDX-License-Identifier: MIT

namespace BuildGuard.Editor
{
    /// <summary>
    /// Describes one actionable issue found in a Prefab asset.
    /// </summary>
    internal readonly struct BuildGuardPrefabScanIssue
    {
        internal BuildGuardPrefabScanIssue(
            BuildGuardIssueKind kind,
            string prefabPath,
            string hierarchyPath,
            string details)
        {
            Kind = kind;
            PrefabPath = prefabPath;
            HierarchyPath = hierarchyPath;
            Details = details;
        }

        internal BuildGuardIssueKind Kind { get; }

        internal string PrefabPath { get; }

        internal string HierarchyPath { get; }

        internal string Details { get; }
    }
}
