// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// StringTableCollection の identity、shared entries、Locale tables を保持します。
    /// </summary>
    internal sealed class LocalizationKeyAuditCollectionSnapshot
    {
        /// <summary>collection 全体を防御的に copy します。</summary>
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

        /// <summary>StringTableCollection 名です。</summary>
        internal string CollectionName { get; }

        /// <summary>SharedTableData が保持する collection GUID です。</summary>
        internal Guid CollectionGuid { get; }

        /// <summary>対応する SharedTableData asset path です。</summary>
        internal string SharedDataAssetPath { get; }

        /// <summary>SharedTableData の全 entry です。</summary>
        internal IReadOnlyList<LocalizationKeyAuditSharedEntrySnapshot> SharedEntries { get; }

        /// <summary>collection に直接属する StringTable 一覧です。</summary>
        internal IReadOnlyList<LocalizationKeyAuditLocaleTableSnapshot> LocaleTables { get; }

        /// <summary>shared entry 一覧を読み取り専用 copy にします。</summary>
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

        /// <summary>Locale table 一覧を読み取り専用 copy にします。</summary>
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

