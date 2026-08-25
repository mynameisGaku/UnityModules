// SPDX-License-Identifier: MIT

using System;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// typed StringTable は存在する一方、対応する StringTableCollection が見つからない table snapshot です。
    /// </summary>
    internal sealed class LocalizationKeyAuditOrphanLocaleTableSnapshot
    {
        /// <summary>SharedTableData identity と table を防御的に copy します。</summary>
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

        /// <summary>table が参照する SharedTableData asset path です。</summary>
        internal string SharedDataAssetPath { get; }

        /// <summary>table が参照する collection GUID です。</summary>
        internal Guid CollectionGuid { get; }

        /// <summary>collection に所属しない direct table です。</summary>
        internal LocalizationKeyAuditLocaleTableSnapshot LocaleTable { get; }
    }
}
