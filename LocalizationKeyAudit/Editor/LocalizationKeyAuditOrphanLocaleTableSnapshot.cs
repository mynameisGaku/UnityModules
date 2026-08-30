// SPDX-License-Identifier: MIT

using System;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 型として読み取った文字列テーブルは存在する一方、対応する文字列テーブルコレクションが見つからない所属先なしテーブルのスナップショットです。
    /// </summary>
    internal sealed class LocalizationKeyAuditOrphanLocaleTableSnapshot
    {
        /// <summary>共有テーブルデータの識別情報とテーブルを防御的に複製します。</summary>
        internal LocalizationKeyAuditOrphanLocaleTableSnapshot(
            string sharedDataAssetPath,
            Guid collectionGuid,
            LocalizationKeyAuditLocaleTableSnapshot localeTable)
        {
            SharedDataAssetPath = sharedDataAssetPath ?? string.Empty;
            CollectionGuid = collectionGuid;
            LocaleTable = localeTable == null
                ? null
                : new LocalizationKeyAuditLocaleTableSnapshot(
                    localeTable.LocaleIdentifier,
                    localeTable.AssetPath,
                    localeTable.Entries);
        }

        /// <summary>テーブルが参照する共有テーブルデータのアセットパスです。</summary>
        internal string SharedDataAssetPath { get; }

        /// <summary>テーブルが参照するコレクション識別子（GUID）です。</summary>
        internal Guid CollectionGuid { get; }

        /// <summary>コレクションに所属しない直接テーブルです。</summary>
        internal LocalizationKeyAuditLocaleTableSnapshot LocaleTable { get; }
    }
}
