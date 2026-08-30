// SPDX-License-Identifier: MIT

using System;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// アセットテーブル側で所有され、文字列キーの直接網羅対象外となる共有テーブルデータの識別情報です。
    /// </summary>
    internal sealed class LocalizationKeyAuditNonStringSharedDataIdentity
    {
        /// <summary>アセットパスとコレクション識別子（GUID）を保持します。</summary>
        internal LocalizationKeyAuditNonStringSharedDataIdentity(string assetPath, Guid collectionGuid)
        {
            AssetPath = assetPath ?? string.Empty;
            CollectionGuid = collectionGuid;
        }

        /// <summary>共有テーブルデータのアセットパスです。</summary>
        internal string AssetPath { get; }

        /// <summary>型として読み取ったアセットテーブルの所有元が参照したコレクション識別子（GUID）です。</summary>
        internal Guid CollectionGuid { get; }
    }
}
