// SPDX-License-Identifier: MIT

using System.Collections.Generic;

namespace BuildGuard.Editor
{
    /// <summary>
    /// Stores the immutable outcome of a selected Prefab scan.
    /// </summary>
    internal readonly struct BuildGuardPrefabScanResult
    {
        internal BuildGuardPrefabScanResult(
            IReadOnlyList<BuildGuardPrefabScanIssue> issues,
            int scannedPrefabCount,
            bool cancelled)
        {
            Issues = issues;
            ScannedPrefabCount = scannedPrefabCount;
            Cancelled = cancelled;
        }

        internal IReadOnlyList<BuildGuardPrefabScanIssue> Issues { get; }

        internal int ScannedPrefabCount { get; }

        internal bool Cancelled { get; }
    }
}
