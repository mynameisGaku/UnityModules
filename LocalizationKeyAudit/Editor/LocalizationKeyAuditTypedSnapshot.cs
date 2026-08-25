// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 公式 Localization API から読み取った Locale と StringTableCollection の snapshot です。
    /// </summary>
    internal sealed class LocalizationKeyAuditTypedSnapshot
    {
        /// <summary>typed source の全出力を防御的に copy します。</summary>
        internal LocalizationKeyAuditTypedSnapshot(
            IReadOnlyList<string> localeIdentifiers,
            IReadOnlyList<LocalizationKeyAuditCollectionSnapshot> collections,
            IReadOnlyList<LocalizationKeyAuditOrphanLocaleTableSnapshot> orphanLocaleTables = null)
        {
            var locales = new string[localeIdentifiers?.Count ?? 0];
            for (var index = 0; index < locales.Length; index++)
            {
                locales[index] = localeIdentifiers[index] ?? string.Empty;
            }

            LocaleIdentifiers = new ReadOnlyCollection<string>(locales);
            var collectionCopy = new LocalizationKeyAuditCollectionSnapshot[collections?.Count ?? 0];
            for (var index = 0; index < collectionCopy.Length; index++)
            {
                var collection = collections[index];
                collectionCopy[index] = collection == null
                    ? null
                    : new LocalizationKeyAuditCollectionSnapshot(
                        collection.CollectionName,
                        collection.CollectionGuid,
                        collection.SharedDataAssetPath,
                        collection.SharedEntries,
                        collection.LocaleTables);
            }

            Collections = new ReadOnlyCollection<LocalizationKeyAuditCollectionSnapshot>(collectionCopy);

            var orphanCopy = new LocalizationKeyAuditOrphanLocaleTableSnapshot[orphanLocaleTables?.Count ?? 0];
            for (var index = 0; index < orphanCopy.Length; index++)
            {
                var orphan = orphanLocaleTables[index];
                orphanCopy[index] = orphan == null
                    ? null
                    : new LocalizationKeyAuditOrphanLocaleTableSnapshot(
                        orphan.SharedDataAssetPath,
                        orphan.CollectionGuid,
                        orphan.LocaleTable);
            }

            OrphanLocaleTables = new ReadOnlyCollection<LocalizationKeyAuditOrphanLocaleTableSnapshot>(orphanCopy);
        }

        /// <summary>Localization Settings に登録済みの Locale identifiers です。</summary>
        internal IReadOnlyList<string> LocaleIdentifiers { get; }

        /// <summary>公式 API が返した全 StringTableCollection です。</summary>
        internal IReadOnlyList<LocalizationKeyAuditCollectionSnapshot> Collections { get; }

        /// <summary>どの StringTableCollection にも対応しなかった direct StringTable です。</summary>
        internal IReadOnlyList<LocalizationKeyAuditOrphanLocaleTableSnapshot> OrphanLocaleTables { get; }
    }
}
