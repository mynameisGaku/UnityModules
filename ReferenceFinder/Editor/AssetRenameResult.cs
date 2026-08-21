using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ReferenceFinder
{
    /// <summary>
    /// Reports paths changed by one completed batch rename.
    /// </summary>
    public sealed class AssetRenameResult
    {
        private readonly ReadOnlyCollection<string> _renamedAssetPaths;

        internal AssetRenameResult(string[] renamedAssetPaths)
        {
            _renamedAssetPaths = Array.AsReadOnly(renamedAssetPaths);
        }

        /// <summary>Gets the number of assets renamed successfully.</summary>
        public int RenamedAssetCount => _renamedAssetPaths.Count;

        /// <summary>Gets final AssetDatabase paths in ordinal order.</summary>
        public IReadOnlyList<string> RenamedAssetPaths => _renamedAssetPaths;
    }
}
