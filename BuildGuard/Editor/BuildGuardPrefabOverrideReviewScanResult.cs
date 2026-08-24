// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;

namespace BuildGuard.Editor
{
    /// <summary>
    /// Stores an immutable all-or-nothing snapshot of enabled build Scene Prefab overrides.
    /// </summary>
    internal readonly struct BuildGuardPrefabOverrideReviewScanResult
    {
        private BuildGuardPrefabOverrideReviewScanResult(
            bool succeeded,
            bool cancelled,
            IReadOnlyList<BuildGuardPrefabOverrideFinding> findings,
            IReadOnlyList<BuildGuardPrefabOverrideReviewFailure> failures,
            int scannedSceneCount,
            long totalFindingCount,
            bool wasTruncated)
        {
            Succeeded = succeeded;
            Cancelled = cancelled;
            Findings = findings;
            Failures = failures;
            ScannedSceneCount = scannedSceneCount;
            TotalFindingCount = totalFindingCount;
            WasTruncated = wasTruncated;
        }

        internal bool Succeeded { get; }

        internal bool Cancelled { get; }

        internal IReadOnlyList<BuildGuardPrefabOverrideFinding> Findings { get; }

        internal IReadOnlyList<BuildGuardPrefabOverrideReviewFailure> Failures { get; }

        internal int ScannedSceneCount { get; }

        internal long TotalFindingCount { get; }

        internal bool WasTruncated { get; }

        /// <summary>Creates a successful detached snapshot.</summary>
        internal static BuildGuardPrefabOverrideReviewScanResult Success(
            IReadOnlyList<BuildGuardPrefabOverrideFinding> findings,
            int scannedSceneCount,
            long totalFindingCount)
        {
            if (findings == null)
            {
                throw new ArgumentNullException(nameof(findings));
            }

            if (scannedSceneCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(scannedSceneCount));
            }

            if (totalFindingCount < findings.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(totalFindingCount));
            }

            return new BuildGuardPrefabOverrideReviewScanResult(
                true,
                false,
                CopyFindings(findings),
                Array.Empty<BuildGuardPrefabOverrideReviewFailure>(),
                scannedSceneCount,
                totalFindingCount,
                totalFindingCount > findings.Count);
        }

        /// <summary>Creates a cancelled result without exposing partial findings.</summary>
        internal static BuildGuardPrefabOverrideReviewScanResult Cancellation(int scannedSceneCount)
        {
            if (scannedSceneCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(scannedSceneCount));
            }

            return new BuildGuardPrefabOverrideReviewScanResult(
                false,
                true,
                Array.Empty<BuildGuardPrefabOverrideFinding>(),
                Array.Empty<BuildGuardPrefabOverrideReviewFailure>(),
                scannedSceneCount,
                0,
                false);
        }

        /// <summary>Creates a failed result without exposing partial findings.</summary>
        internal static BuildGuardPrefabOverrideReviewScanResult Failure(
            IReadOnlyList<BuildGuardPrefabOverrideReviewFailure> failures,
            int scannedSceneCount)
        {
            if (failures == null)
            {
                throw new ArgumentNullException(nameof(failures));
            }

            if (failures.Count == 0)
            {
                throw new ArgumentException("A failed review requires at least one failure.", nameof(failures));
            }

            if (scannedSceneCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(scannedSceneCount));
            }

            return new BuildGuardPrefabOverrideReviewScanResult(
                false,
                false,
                Array.Empty<BuildGuardPrefabOverrideFinding>(),
                CopyFailures(failures),
                scannedSceneCount,
                0,
                false);
        }

        private static IReadOnlyList<BuildGuardPrefabOverrideFinding> CopyFindings(
            IReadOnlyList<BuildGuardPrefabOverrideFinding> source)
        {
            var snapshot = new BuildGuardPrefabOverrideFinding[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                snapshot[index] = source[index];
            }

            return Array.AsReadOnly(snapshot);
        }

        private static IReadOnlyList<BuildGuardPrefabOverrideReviewFailure> CopyFailures(
            IReadOnlyList<BuildGuardPrefabOverrideReviewFailure> source)
        {
            var snapshot = new BuildGuardPrefabOverrideReviewFailure[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                snapshot[index] = source[index];
            }

            return Array.AsReadOnly(snapshot);
        }
    }
}
