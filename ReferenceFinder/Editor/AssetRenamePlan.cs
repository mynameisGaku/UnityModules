using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ReferenceFinder
{
    /// <summary>
    /// Contains an immutable batch-rename preview in ordinal source-path order.
    /// </summary>
    public sealed class AssetRenamePlan
    {
        private readonly ReadOnlyCollection<AssetRenameEntry> _entries;

        internal AssetRenamePlan(AssetRenameEntry[] entries)
        {
            _entries = Array.AsReadOnly(entries);
        }

        /// <summary>Gets every path change that will be attempted.</summary>
        public IReadOnlyList<AssetRenameEntry> Entries => _entries;
    }
}
