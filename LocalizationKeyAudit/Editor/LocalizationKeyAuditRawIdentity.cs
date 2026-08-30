// SPDX-License-Identifier: MIT

using System;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 未加工のYAML事前検査で安全に確認した共有テーブルデータの識別情報です。
    /// </summary>
    internal sealed class LocalizationKeyAuditRawIdentity
    {
        /// <summary>アセットパスと直列化済みコレクション識別子（GUID）を保持します。</summary>
        internal LocalizationKeyAuditRawIdentity(string assetPath, Guid collectionGuid)
        {
            AssetPath = assetPath ?? string.Empty;
            CollectionGuid = collectionGuid;
        }

        /// <summary>共有テーブルデータのアセットパスです。</summary>
        internal string AssetPath { get; }

        /// <summary>未加工のYAMLに1件だけ存在した空でないGUIDです。</summary>
        internal Guid CollectionGuid { get; }
    }
}
