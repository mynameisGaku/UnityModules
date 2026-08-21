using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ReferenceFinder
{
    /// <summary>
    /// Reports the completed changes from one replacement operation.
    /// </summary>
    public sealed class AssetReferenceReplacementResult
    {
        private readonly ReadOnlyCollection<string> _changedAssetPaths;

        internal AssetReferenceReplacementResult(int replacedReferenceCount, string[] changedAssetPaths)
        {
            ReplacedReferenceCount = replacedReferenceCount;
            _changedAssetPaths = Array.AsReadOnly(changedAssetPaths);
        }

        /// <summary>Gets the number of serialized properties that were changed.</summary>
        public int ReplacedReferenceCount { get; }

        /// <summary>Gets changed AssetDatabase paths in ordinal order.</summary>
        public IReadOnlyList<string> ChangedAssetPaths => _changedAssetPaths;
    }
}
