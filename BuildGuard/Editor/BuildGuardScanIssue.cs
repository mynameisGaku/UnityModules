// SPDX-License-Identifier: MIT

namespace BuildGuard.Editor
{
    /// <summary>
    /// 手動検査で問題を検出したビルドガードの規則を識別します。
    /// </summary>
    internal enum BuildGuardIssueKind
    {
        MissingScript = 0,
        MissingObjectReference = 1
    }

    /// <summary>
    /// 手動のビルド対象シーン検査で見つかった、修正対象となる問題を1件表します。
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
