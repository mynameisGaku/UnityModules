// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 公式のローカライズ機能から読み取った文字列監査データと、アセットテーブルの所有境界を保持するスナップショットです。
    /// </summary>
    internal sealed class LocalizationKeyAuditTypedSnapshot
    {
        /// <summary>型として読み取る取得元の全出力を防御的に複製します。</summary>
        internal LocalizationKeyAuditTypedSnapshot(
            IReadOnlyList<string> localeIdentifiers,
            IReadOnlyList<LocalizationKeyAuditCollectionSnapshot> collections,
            IReadOnlyList<LocalizationKeyAuditOrphanLocaleTableSnapshot> orphanLocaleTables = null,
            IReadOnlyList<LocalizationKeyAuditNonStringSharedDataIdentity> nonStringSharedDataIdentities = null)
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

            var nonStringCopy = new LocalizationKeyAuditNonStringSharedDataIdentity[
                nonStringSharedDataIdentities?.Count ?? 0];
            for (var index = 0; index < nonStringCopy.Length; index++)
            {
                var identity = nonStringSharedDataIdentities[index];
                nonStringCopy[index] = identity == null
                    ? null
                    : new LocalizationKeyAuditNonStringSharedDataIdentity(
                        identity.AssetPath,
                        identity.CollectionGuid);
            }

            NonStringSharedDataIdentities =
                new ReadOnlyCollection<LocalizationKeyAuditNonStringSharedDataIdentity>(nonStringCopy);
        }

        /// <summary>ローカライズ設定に登録済みのロケール識別子です。</summary>
        internal IReadOnlyList<string> LocaleIdentifiers { get; }

        /// <summary>公式機能が返した全文字列テーブルコレクションです。</summary>
        internal IReadOnlyList<LocalizationKeyAuditCollectionSnapshot> Collections { get; }

        /// <summary>どの文字列テーブルコレクションにも対応しなかった直接の文字列テーブルです。</summary>
        internal IReadOnlyList<LocalizationKeyAuditOrphanLocaleTableSnapshot> OrphanLocaleTables { get; }

        /// <summary>アセットテーブルの所有元が参照し、文字列キーの直接網羅から除外する共有テーブルデータです。</summary>
        internal IReadOnlyList<LocalizationKeyAuditNonStringSharedDataIdentity> NonStringSharedDataIdentities { get; }
    }
}
