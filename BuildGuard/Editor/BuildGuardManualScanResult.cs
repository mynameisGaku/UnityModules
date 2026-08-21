// SPDX-License-Identifier: MIT

using System.Collections.Generic;

namespace BuildGuard.Editor
{
    /// <summary>
    /// Stores the immutable outcome of a manual build Scene scan.
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
