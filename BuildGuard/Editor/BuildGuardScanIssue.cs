// SPDX-License-Identifier: MIT

namespace BuildGuard.Editor
{
    /// <summary>
    /// Identifies the Build Guard rule that produced a manual scan issue.
    /// </summary>
    internal enum BuildGuardIssueKind
    {
        MissingScript = 0,
        MissingObjectReference = 1
    }

    /// <summary>
    /// Describes one actionable issue found by a manual build Scene scan.
    /// </summary>
    internal readonly struct BuildGuardScanIssue
    {
        internal BuildGuardScanIssue(
            BuildGuardIssueKind kind,
            string scenePath,
            string hierarchyPath,
            string details)
        {
            Kind = kind;
            ScenePath = scenePath;
            HierarchyPath = hierarchyPath;
            Details = details;
        }

        internal BuildGuardIssueKind Kind { get; }

        internal string ScenePath { get; }

        internal string HierarchyPath { get; }

        internal string Details { get; }
    }
}
