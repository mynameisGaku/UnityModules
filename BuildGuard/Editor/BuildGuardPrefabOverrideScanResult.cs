// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;

namespace BuildGuard.Editor
{
    /// <summary>
    /// Stores the immutable, all-or-nothing outcome of one structural Prefab override scan.
    /// </summary>
    internal readonly struct BuildGuardPrefabOverrideScanResult
    {
        private BuildGuardPrefabOverrideScanResult(
            bool succeeded,
            BuildGuardPrefabOverrideScanError error,
            string errorMessage,
            IReadOnlyList<BuildGuardPrefabOverrideFinding> findings,
            int visitedGameObjectCount,
            int scannedPrefabInstanceCount)
        {
            Succeeded = succeeded;
            Error = error;
            ErrorMessage = errorMessage ?? string.Empty;
            Findings = findings ?? Array.Empty<BuildGuardPrefabOverrideFinding>();
            VisitedGameObjectCount = visitedGameObjectCount;
            ScannedPrefabInstanceCount = scannedPrefabInstanceCount;
        }

        internal bool Succeeded { get; }

        internal BuildGuardPrefabOverrideScanError Error { get; }

        internal string ErrorMessage { get; }

        internal IReadOnlyList<BuildGuardPrefabOverrideFinding> Findings { get; }

        internal int VisitedGameObjectCount { get; }

        internal int ScannedPrefabInstanceCount { get; }

        /// <summary>Creates a successful result with a detached finding snapshot.</summary>
        internal static BuildGuardPrefabOverrideScanResult Success(
            IReadOnlyList<BuildGuardPrefabOverrideFinding> findings,
            int visitedGameObjectCount,
            int scannedPrefabInstanceCount)
        {
            if (findings == null)
            {
                throw new ArgumentNullException(nameof(findings));
            }

            var snapshot = new BuildGuardPrefabOverrideFinding[findings.Count];
            for (var index = 0; index < findings.Count; index++)
            {
                snapshot[index] = findings[index];
            }

            return new BuildGuardPrefabOverrideScanResult(
                true,
                BuildGuardPrefabOverrideScanError.None,
                string.Empty,
                Array.AsReadOnly(snapshot),
                visitedGameObjectCount,
                scannedPrefabInstanceCount);
        }

        /// <summary>Creates a failed result without exposing partial findings.</summary>
        internal static BuildGuardPrefabOverrideScanResult Failure(
            BuildGuardPrefabOverrideScanError error,
            string errorMessage,
            int visitedGameObjectCount,
            int scannedPrefabInstanceCount)
        {
            if (error == BuildGuardPrefabOverrideScanError.None)
            {
                throw new ArgumentException("A failed scan requires a non-success error.", nameof(error));
            }

            return new BuildGuardPrefabOverrideScanResult(
                false,
                error,
                errorMessage,
                Array.Empty<BuildGuardPrefabOverrideFinding>(),
                visitedGameObjectCount,
                scannedPrefabInstanceCount);
        }
    }
}
