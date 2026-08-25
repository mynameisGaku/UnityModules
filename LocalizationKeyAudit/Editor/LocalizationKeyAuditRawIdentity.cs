// SPDX-License-Identifier: MIT

using System;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// raw YAML preflight で安全に確認した SharedTableData identity です。
    /// </summary>
    internal sealed class LocalizationKeyAuditRawIdentity
    {
        /// <summary>asset path と serialized collection GUID を保持します。</summary>
        internal LocalizationKeyAuditRawIdentity(string assetPath, Guid collectionGuid)
        {
            AssetPath = assetPath ?? string.Empty;
            CollectionGuid = collectionGuid;
        }

        /// <summary>SharedTableData asset path です。</summary>
        internal string AssetPath { get; }

        /// <summary>raw YAML に 1 件だけ存在した non-empty GUID です。</summary>
        internal Guid CollectionGuid { get; }
    }
}

