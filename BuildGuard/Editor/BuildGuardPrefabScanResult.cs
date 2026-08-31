// SPDX-License-Identifier: MIT

using System.Collections.Generic;

namespace BuildGuard.Editor
{
    /// <summary>
    /// 選択プレハブの検査で得た、変更されない結果を保持します。
    /// </summary>
    internal readonly struct BuildGuardPrefabScanResult
    {
        /// <summary>問題一覧、検査済み件数、中止状態から結果を作成します。</summary>
        internal BuildGuardPrefabScanResult(
            IReadOnlyList<BuildGuardPrefabScanIssue> issues,
            int scannedPrefabCount,
            bool cancelled)
        {
            Issues = issues;
            ScannedPrefabCount = scannedPrefabCount;
            Cancelled = cancelled;
        }

        /// <summary>検出した問題の一覧を取得します。</summary>
        internal IReadOnlyList<BuildGuardPrefabScanIssue> Issues { get; }

        /// <summary>検査を完了したプレハブの件数を取得します。</summary>
        internal int ScannedPrefabCount { get; }

        /// <summary>利用者の操作によって検査を中止したか取得します。</summary>
        internal bool Cancelled { get; }
    }
}
