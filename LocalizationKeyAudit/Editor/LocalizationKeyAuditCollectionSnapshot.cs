// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 文字列テーブルコレクションの識別情報、共有項目、ロケールテーブルを保持します。
    /// </summary>
    internal sealed class LocalizationKeyAuditCollectionSnapshot
    {
        /// <summary>コレクション全体を防御的に複製します。</summary>
        internal LocalizationKeyAuditCollectionSnapshot(
            string collectionName,
            Guid collectionGuid,
            string sharedDataAssetPath,
            IReadOnlyList<LocalizationKeyAuditSharedEntrySnapshot> sharedEntries,
            IReadOnlyList<LocalizationKeyAuditLocaleTableSnapshot> localeTables)
        {
            CollectionName = collectionName ?? string.Empty;
            CollectionGuid = collectionGuid;
            SharedDataAssetPath = sharedDataAssetPath ?? string.Empty;
            SharedEntries = CopySharedEntries(sharedEntries);
            LocaleTables = CopyLocaleTables(localeTables);
        }

        /// <summary>文字列テーブルコレクション名です。</summary>
        internal string CollectionName { get; }

        /// <summary>共有テーブルデータが保持するコレクション識別子（GUID）です。</summary>
        internal Guid CollectionGuid { get; }

        /// <summary>対応する共有テーブルデータのアセットパスです。</summary>
        internal string SharedDataAssetPath { get; }

        /// <summary>共有テーブルデータの全項目です。</summary>
        internal IReadOnlyList<LocalizationKeyAuditSharedEntrySnapshot> SharedEntries { get; }

        /// <summary>コレクションに直接属する文字列テーブル一覧です。</summary>
        internal IReadOnlyList<LocalizationKeyAuditLocaleTableSnapshot> LocaleTables { get; }

        /// <summary>共有項目一覧を読み取り専用の複製にします。</summary>
        private static IReadOnlyList<LocalizationKeyAuditSharedEntrySnapshot> CopySharedEntries(
            IReadOnlyList<LocalizationKeyAuditSharedEntrySnapshot> entries)
        {
            var copy = new LocalizationKeyAuditSharedEntrySnapshot[entries?.Count ?? 0];
            for (var index = 0; index < copy.Length; index++)
            {
                var entry = entries[index];
                copy[index] = entry == null ? null : new LocalizationKeyAuditSharedEntrySnapshot(entry.Id, entry.Key);
            }

            return new ReadOnlyCollection<LocalizationKeyAuditSharedEntrySnapshot>(copy);
        }

        /// <summary>ロケールテーブル一覧を読み取り専用の複製にします。</summary>
        private static IReadOnlyList<LocalizationKeyAuditLocaleTableSnapshot> CopyLocaleTables(
            IReadOnlyList<LocalizationKeyAuditLocaleTableSnapshot> tables)
        {
            var copy = new LocalizationKeyAuditLocaleTableSnapshot[tables?.Count ?? 0];
            for (var index = 0; index < copy.Length; index++)
            {
                var table = tables[index];
                copy[index] = table == null
                    ? null
                    : new LocalizationKeyAuditLocaleTableSnapshot(
                        table.LocaleIdentifier,
                        table.AssetPath,
                        table.Entries);
            }

            return new ReadOnlyCollection<LocalizationKeyAuditLocaleTableSnapshot>(copy);
        }
    }
}
