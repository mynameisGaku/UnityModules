// SPDX-License-Identifier: MIT

using System;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// Asset Table 側で所有され、String key の direct coverage 対象外となる SharedTableData identity です。
    /// </summary>
    internal sealed class LocalizationKeyAuditNonStringSharedDataIdentity
    {
        /// <summary>asset path と collection GUID を保持します。</summary>
        internal LocalizationKeyAuditNonStringSharedDataIdentity(string assetPath, Guid collectionGuid)
        {
            AssetPath = assetPath ?? string.Empty;
            CollectionGuid = collectionGuid;
        }

        /// <summary>SharedTableData asset path です。</summary>
        internal string AssetPath { get; }

        /// <summary>typed Asset Table owner が参照した collection GUID です。</summary>
        internal Guid CollectionGuid { get; }
    }
}
