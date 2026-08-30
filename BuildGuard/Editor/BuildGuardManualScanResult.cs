// SPDX-License-Identifier: MIT

using System.Collections.Generic;

namespace BuildGuard.Editor
{
    /// <summary>
    /// 手動のビルド対象シーン検査で得た、変更されない結果を保持します。
    /// </summary>
    internal readonly struct BuildGuardManualScanResult
    {
        internal BuildGuardManualScanResult(
            IReadOnlyList<BuildGuardScanIssue> issues,
            int scannedSceneCount,
            bool cancelled)
        {
            Issues = issues;
            ScannedSceneCount = scannedSceneCount;
            Cancelled = cancelled;
        }

        internal IReadOnlyList<BuildGuardScanIssue> Issues { get; }

        internal int ScannedSceneCount { get; }

        internal bool Cancelled { get; }
    }
}
