// SPDX-License-Identifier: MIT

namespace BuildGuard.Editor
{
    /// <summary>
    /// プレハブアセットで見つかった、修正対象となる問題を1件表します。
    /// </summary>
    internal readonly struct BuildGuardPrefabScanIssue
    {
        /// <summary>安定識別値を持たない、表示専用の検出結果を作成します。</summary>
        internal BuildGuardPrefabScanIssue(
            BuildGuardIssueKind kind,
            string prefabPath,
            string hierarchyPath,
            string details)
            : this(kind, prefabPath, hierarchyPath, string.Empty, details)
        {
        }

        /// <summary>問題種別、プレハブ、階層、対象識別値、詳細から検出結果を作成します。</summary>
        internal BuildGuardPrefabScanIssue(
            BuildGuardIssueKind kind,
            string prefabPath,
            string hierarchyPath,
            string targetGlobalObjectId,
            string details)
        {
            Kind = kind;
            PrefabPath = prefabPath;
            HierarchyPath = hierarchyPath;
            TargetGlobalObjectId = targetGlobalObjectId ?? string.Empty;
            Details = details;
        }

        /// <summary>問題の種別を取得します。</summary>
        internal BuildGuardIssueKind Kind { get; }

        /// <summary>問題があるプレハブアセットのパスを取得します。</summary>
        internal string PrefabPath { get; }

        /// <summary>同じ親の中での順番を含むゲームオブジェクト階層のパスを取得します。</summary>
        internal string HierarchyPath { get; }

        /// <summary>対象ゲームオブジェクトを資産内で識別する安定識別値を取得します。</summary>
        internal string TargetGlobalObjectId { get; }

        /// <summary>欠落件数、またはコンポーネントとプロパティのパスを取得します。</summary>
        internal string Details { get; }
    }
}
