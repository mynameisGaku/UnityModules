using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ReferenceFinder
{
    /// <summary>
    /// Contains the deterministic result of one direct-reference search.
    /// </summary>
    public sealed class AssetReferenceSearchResult
    {
        private readonly ReadOnlyCollection<string> _referenceAssetPaths;
        private readonly ReadOnlyCollection<string> _failedAssetPaths;

        internal AssetReferenceSearchResult(
            string targetAssetPath,
            string[] referenceAssetPaths,
            string[] failedAssetPaths,
            int scannedAssetCount,
            int candidateAssetCount,
            bool wasCanceled,
            AssetReferenceSearchMode searchMode)
        {
            TargetAssetPath = targetAssetPath;
            _referenceAssetPaths = Array.AsReadOnly(referenceAssetPaths);
            _failedAssetPaths = Array.AsReadOnly(failedAssetPaths);
            ScannedAssetCount = scannedAssetCount;
            CandidateAssetCount = candidateAssetCount;
            WasCanceled = wasCanceled;
            SearchMode = searchMode;
        }

        /// <summary>Gets the canonical AssetDatabase path that was searched.</summary>
        public string TargetAssetPath { get; }

        /// <summary>Gets asset paths that directly depend on the target, sorted by ordinal path.</summary>
        public IReadOnlyList<string> ReferenceAssetPaths => _referenceAssetPaths;

        /// <summary>Gets candidate paths that could not be inspected, sorted by ordinal path.</summary>
        public IReadOnlyList<string> FailedAssetPaths => _failedAssetPaths;

        /// <summary>Gets the number of candidate assets inspected before completion or cancellation.</summary>
        public int ScannedAssetCount { get; }

        /// <summary>Gets the total number of candidate assets found in the selected folders.</summary>
        public int CandidateAssetCount { get; }

        /// <summary>Gets whether the search stopped before every candidate was inspected.</summary>
        public bool WasCanceled { get; }

        /// <summary>Gets the dependency depth used for this search.</summary>
        public AssetReferenceSearchMode SearchMode { get; }
    }
}
