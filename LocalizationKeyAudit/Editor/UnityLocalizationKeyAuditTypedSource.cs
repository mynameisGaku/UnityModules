// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 未加工データの事前検査後に、公式のローカライズ機能を読み取り専用の不変な転送値へ変換します。
    /// </summary>
    internal sealed class UnityLocalizationKeyAuditTypedSource : ILocalizationKeyAuditTypedSource
    {
        /// <summary>
        /// ロケールと文字列テーブルコレクションはLocalizationEditorSettings、テーブルとアセットテーブルの所有元は直接アセット列挙で読みます。
        /// </summary>
        public LocalizationKeyAuditTypedSnapshot ReadSnapshot()
        {
            var locales = ReadLocales();
            var tablesBySharedDataPath = ReadLocaleTables();
            var collections = ReadCollections(tablesBySharedDataPath, out var orphanLocaleTables);
            var nonStringSharedDataIdentities = ReadNonStringSharedDataIdentities();
            return new LocalizationKeyAuditTypedSnapshot(
                locales,
                collections,
                orphanLocaleTables,
                nonStringSharedDataIdentities);
        }

        /// <summary>ローカライズ設定のロケール識別子を読みます。</summary>
        private static IReadOnlyList<string> ReadLocales()
        {
            var source = LocalizationEditorSettings.GetLocales();
            if (source == null)
            {
                throw new InvalidDataException("LocalizationEditorSettings.GetLocalesの戻り値がありません。");
            }

            if (source.Count > LocalizationKeyAuditLimits.MaximumLocales)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"ロケール数が上限 {LocalizationKeyAuditLimits.MaximumLocales} 件を超えています。");
            }

            var locales = new List<string>(source.Count);
            for (var index = 0; index < source.Count; index++)
            {
                var locale = source[index];
                if (locale == null)
                {
                    throw new InvalidDataException("ローカライズ設定のロケール一覧に未設定の要素が含まれています。");
                }

                locales.Add(locale.Identifier.Code);
            }

            return locales;
        }

        /// <summary>
        /// 変更経路を持つStringTableCollection.StringTables／Tables／GetTableを避け、すべての文字列テーブルアセットを直接読みます。
        /// </summary>
        private static Dictionary<string, LocaleTableGroup> ReadLocaleTables()
        {
            var guids = AssetDatabase.FindAssets("t:StringTable") ?? Array.Empty<string>();
            if (guids.Length > LocalizationKeyAuditLimits.MaximumLocaleTables)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"文字列テーブル数が上限 {LocalizationKeyAuditLimits.MaximumLocaleTables} 件を超えています。");
            }

            Array.Sort(guids, StringComparer.Ordinal);
            var tables = new Dictionary<string, LocaleTableGroup>(StringComparer.Ordinal);
            long localizedEntryCount = 0;
            for (var index = 0; index < guids.Length; index++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[index]);
                if (string.IsNullOrEmpty(assetPath))
                {
                    throw new InvalidDataException($"文字列テーブルのGUID {guids[index]} に対応するアセットパスを取得できません。");
                }

                var table = AssetDatabase.LoadAssetAtPath<StringTable>(assetPath);
                if (table == null || table.SharedData == null)
                {
                    throw new InvalidDataException($"文字列テーブルまたは共有テーブルデータを型として読み取れません: {assetPath}");
                }

                var entries = ReadSerializedEntries(table, assetPath, localizedEntryCount);
                localizedEntryCount += entries.Count;

                var collectionGuid = table.SharedData.TableCollectionNameGuid;
                var sharedDataAssetPath = AssetDatabase.GetAssetPath(table.SharedData);
                if (string.IsNullOrEmpty(sharedDataAssetPath))
                {
                    throw new InvalidDataException($"文字列テーブルが参照する共有テーブルデータのアセットパスを取得できません: {assetPath}");
                }

                if (!tables.TryGetValue(sharedDataAssetPath, out var group))
                {
                    group = new LocaleTableGroup(sharedDataAssetPath, collectionGuid);
                    tables.Add(sharedDataAssetPath, group);
                }
                else if (group.CollectionGuid != collectionGuid)
                {
                    throw new InvalidDataException($"同じ共有テーブルデータのアセットパスに異なるコレクション識別子（GUID）が見つかりました: {sharedDataAssetPath}");
                }

                group.LocalizedEntryCount += entries.Count;
                group.Tables.Add(new LocalizationKeyAuditLocaleTableSnapshot(
                    table.LocaleIdentifier.Code,
                    assetPath,
                    entries));
            }

            return tables;
        }

        /// <summary>文字列テーブルコレクションと共有テーブルデータの項目一覧を公式のエディター機能から読みます。</summary>
        private static IReadOnlyList<LocalizationKeyAuditCollectionSnapshot> ReadCollections(
            IReadOnlyDictionary<string, LocaleTableGroup> tablesBySharedDataPath,
            out IReadOnlyList<LocalizationKeyAuditOrphanLocaleTableSnapshot> orphanLocaleTables)
        {
            var guids = AssetDatabase.FindAssets("t:StringTableCollection") ?? Array.Empty<string>();
            if (guids.Length > LocalizationKeyAuditLimits.MaximumCollections)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"文字列テーブルコレクション数が上限 {LocalizationKeyAuditLimits.MaximumCollections} 件を超えています。");
            }

            Array.Sort(guids, StringComparer.Ordinal);
            var collections = new List<LocalizationKeyAuditCollectionSnapshot>(guids.Length);
            var matchedSharedDataPaths = new HashSet<string>(StringComparer.Ordinal);
            long sharedEntryCount = 0;
            long assignedLocaleTableCount = 0;
            long assignedLocalizedEntryCount = 0;
            for (var index = 0; index < guids.Length; index++)
            {
                var collectionAssetPath = AssetDatabase.GUIDToAssetPath(guids[index]);
                if (string.IsNullOrEmpty(collectionAssetPath))
                {
                    throw new InvalidDataException($"文字列テーブルコレクションのGUID {guids[index]} に対応するアセットパスを取得できません。");
                }

                var collection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(collectionAssetPath);
                if (collection == null || collection.SharedData == null)
                {
                    throw new InvalidDataException($"文字列テーブルコレクションまたは共有テーブルデータを型として読み取れません: {collectionAssetPath}");
                }

                var sharedData = collection.SharedData;
                var sharedAssetPath = AssetDatabase.GetAssetPath(sharedData);
                if (string.IsNullOrEmpty(sharedAssetPath))
                {
                    throw new InvalidDataException($"共有テーブルデータのアセットパスを取得できません: {collection.name}");
                }

                var sourceEntries = sharedData.Entries;
                if (sourceEntries == null)
                {
                    throw new InvalidDataException($"共有テーブルデータの項目一覧が未設定です: {sharedAssetPath}");
                }

                sharedEntryCount += sourceEntries.Count;
                if (sharedEntryCount > LocalizationKeyAuditLimits.MaximumSharedEntries)
                {
                    throw new LocalizationKeyAuditLimitException(
                        $"共有項目総数が上限 {LocalizationKeyAuditLimits.MaximumSharedEntries} 件を超えています。");
                }

                var sharedEntries = new List<LocalizationKeyAuditSharedEntrySnapshot>(sourceEntries.Count);
                for (var entryIndex = 0; entryIndex < sourceEntries.Count; entryIndex++)
                {
                    var entry = sourceEntries[entryIndex];
                    if (entry == null)
                    {
                        throw new InvalidDataException($"共有テーブルデータに未設定の項目が含まれています: {sharedAssetPath}");
                    }

                    sharedEntries.Add(new LocalizationKeyAuditSharedEntrySnapshot(entry.Id, entry.Key));
                }

                var collectionGuid = sharedData.TableCollectionNameGuid;
                var localeTables = tablesBySharedDataPath.TryGetValue(sharedAssetPath, out var foundGroup)
                    ? foundGroup.Tables
                    : new List<LocalizationKeyAuditLocaleTableSnapshot>();
                if (foundGroup != null && foundGroup.CollectionGuid != collectionGuid)
                {
                    throw new InvalidDataException($"コレクションと直接読み取った文字列テーブルのGUIDが一致しません: {sharedAssetPath}");
                }

                var localeTableCount = foundGroup?.Tables.Count ?? 0;
                var localizedEntryViewCount = foundGroup?.LocalizedEntryCount ?? 0;
                EnsureCollectionViewBudget(
                    localeTableCount,
                    localizedEntryViewCount,
                    assignedLocaleTableCount,
                    assignedLocalizedEntryCount,
                    sharedAssetPath);
                assignedLocaleTableCount += localeTableCount;
                assignedLocalizedEntryCount += localizedEntryViewCount;

                matchedSharedDataPaths.Add(sharedAssetPath);
                collections.Add(new LocalizationKeyAuditCollectionSnapshot(
                    collection.TableCollectionName,
                    collectionGuid,
                    sharedAssetPath,
                    sharedEntries,
                    localeTables));
            }

            var orphanTables = new List<LocalizationKeyAuditOrphanLocaleTableSnapshot>();
            foreach (var pair in tablesBySharedDataPath)
            {
                if (matchedSharedDataPaths.Contains(pair.Key))
                {
                    continue;
                }

                for (var tableIndex = 0; tableIndex < pair.Value.Tables.Count; tableIndex++)
                {
                    var table = pair.Value.Tables[tableIndex];
                    var entryCount = table?.Entries.Count ?? 0;
                    EnsureCollectionViewBudget(
                        1,
                        entryCount,
                        assignedLocaleTableCount,
                        assignedLocalizedEntryCount,
                        pair.Key);
                    assignedLocaleTableCount++;
                    assignedLocalizedEntryCount += entryCount;
                    orphanTables.Add(new LocalizationKeyAuditOrphanLocaleTableSnapshot(
                        pair.Value.SharedDataAssetPath,
                        pair.Value.CollectionGuid,
                        table));
                }
            }

            orphanLocaleTables = orphanTables;
            return collections;
        }

        /// <summary>
        /// 直接網羅対象外のアセットテーブル／アセットテーブルコレクションの所有元を読み、共有テーブルデータの識別情報だけを保持します。
        /// コレクションのTables／AssetTablesプロパティは変更経路を持つため呼びません。
        /// </summary>
        private static IReadOnlyList<LocalizationKeyAuditNonStringSharedDataIdentity>
            ReadNonStringSharedDataIdentities()
        {
            var identities = new Dictionary<string, Guid>(StringComparer.Ordinal);
            var tableGuids = AssetDatabase.FindAssets("t:AssetTable") ?? Array.Empty<string>();
            if (tableGuids.Length > LocalizationKeyAuditLimits.MaximumLocaleTables)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"アセットテーブル数が上限 {LocalizationKeyAuditLimits.MaximumLocaleTables} 件を超えています。");
            }

            Array.Sort(tableGuids, StringComparer.Ordinal);
            for (var index = 0; index < tableGuids.Length; index++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(tableGuids[index]);
                if (string.IsNullOrEmpty(assetPath))
                {
                    throw new InvalidDataException($"アセットテーブルのGUID {tableGuids[index]} に対応するアセットパスを取得できません。");
                }

                var table = AssetDatabase.LoadAssetAtPath<AssetTable>(assetPath);
                if (table == null || table.SharedData == null)
                {
                    throw new InvalidDataException($"アセットテーブルまたは共有テーブルデータを型として読み取れません: {assetPath}");
                }

                AddNonStringSharedDataIdentity(identities, table.SharedData, assetPath);
            }

            var collectionGuids = AssetDatabase.FindAssets("t:AssetTableCollection") ?? Array.Empty<string>();
            if (collectionGuids.Length > LocalizationKeyAuditLimits.MaximumCollections)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"アセットテーブルコレクション数が上限 {LocalizationKeyAuditLimits.MaximumCollections} 件を超えています。");
            }

            Array.Sort(collectionGuids, StringComparer.Ordinal);
            for (var index = 0; index < collectionGuids.Length; index++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(collectionGuids[index]);
                if (string.IsNullOrEmpty(assetPath))
                {
                    throw new InvalidDataException(
                        $"アセットテーブルコレクションのGUID {collectionGuids[index]} に対応するアセットパスを取得できません。");
                }

                var collection = AssetDatabase.LoadAssetAtPath<AssetTableCollection>(assetPath);
                if (collection == null || collection.SharedData == null)
                {
                    throw new InvalidDataException(
                        $"アセットテーブルコレクションまたは共有テーブルデータを型として読み取れません: {assetPath}");
                }

                AddNonStringSharedDataIdentity(identities, collection.SharedData, assetPath);
            }

            var paths = new List<string>(identities.Keys);
            paths.Sort(StringComparer.Ordinal);
            var result = new List<LocalizationKeyAuditNonStringSharedDataIdentity>(paths.Count);
            for (var index = 0; index < paths.Count; index++)
            {
                result.Add(new LocalizationKeyAuditNonStringSharedDataIdentity(
                    paths[index],
                    identities[paths[index]]));
            }

            return result;
        }

        /// <summary>アセットテーブルの所有元が参照する共有テーブルデータのアセットパスとGUIDを一意に保持します。</summary>
        private static void AddNonStringSharedDataIdentity(
            IDictionary<string, Guid> identities,
            SharedTableData sharedData,
            string ownerAssetPath)
        {
            var sharedDataAssetPath = AssetDatabase.GetAssetPath(sharedData);
            if (string.IsNullOrEmpty(sharedDataAssetPath))
            {
                throw new InvalidDataException(
                    $"アセットテーブルの所有元が参照する共有テーブルデータのアセットパスを取得できません: {ownerAssetPath}");
            }

            var collectionGuid = sharedData.TableCollectionNameGuid;
            if (collectionGuid == Guid.Empty)
            {
                throw new InvalidDataException(
                    $"アセットテーブルの所有元が参照する共有テーブルデータのコレクション識別子（GUID）が空です: {sharedDataAssetPath}");
            }

            if (identities.TryGetValue(sharedDataAssetPath, out var existing))
            {
                if (existing != collectionGuid)
                {
                    throw new InvalidDataException(
                        $"同じアセットテーブル用共有テーブルデータのアセットパスに異なるコレクション識別子（GUID）が見つかりました: {sharedDataAssetPath}");
                }

                return;
            }

            if (identities.Count >= LocalizationKeyAuditLimits.MaximumSharedTableDataAssets)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"アセットテーブルが参照する共有テーブルデータ数が上限 {LocalizationKeyAuditLimits.MaximumSharedTableDataAssets} 件を超えています。");
            }

            identities.Add(sharedDataAssetPath, collectionGuid);
        }

        /// <summary>シリアル化されたm_TableDataを直接読み、連想配列化で失われる重複IDも保持します。</summary>
        private static List<LocalizationKeyAuditLocalizedEntrySnapshot> ReadSerializedEntries(
            StringTable table,
            string assetPath,
            long localizedEntriesAlreadyRead)
        {
            using (var serializedObject = new SerializedObject(table))
            {
                var tableData = serializedObject.FindProperty("m_TableData");
                if (tableData == null || !tableData.isArray)
                {
                    throw new InvalidDataException($"文字列テーブルのシリアル化されたm_TableDataを取得できません: {assetPath}");
                }

                EnsureLocalizedEntryBudget(
                    tableData.arraySize,
                    localizedEntriesAlreadyRead,
                    assetPath);

                var entries = new List<LocalizationKeyAuditLocalizedEntrySnapshot>(tableData.arraySize);
                for (var index = 0; index < tableData.arraySize; index++)
                {
                    var entry = tableData.GetArrayElementAtIndex(index);
                    var id = entry?.FindPropertyRelative("m_Id");
                    var localized = entry?.FindPropertyRelative("m_Localized");
                    if (id == null || localized == null)
                    {
                        throw new InvalidDataException($"文字列テーブル項目のm_Id／m_Localizedを取得できません: {assetPath}");
                    }

                    var value = localized.stringValue;
                    if (value != null && value.Length > LocalizationKeyAuditLimits.MaximumLocalizedValueCharacters)
                    {
                        throw new LocalizationKeyAuditLimitException(
                            $"ローカライズ済みの値が文字数上限 {LocalizationKeyAuditLimits.MaximumLocalizedValueCharacters} を超えています: {assetPath}");
                    }

                    entries.Add(new LocalizationKeyAuditLocalizedEntrySnapshot(id.longValue, value));
                }

                return entries;
            }
        }

        /// <summary>次のテーブルを複製する前に、ローカライズ済み項目総数の残り上限を検証します。</summary>
        internal static void EnsureLocalizedEntryBudget(
            int tableEntryCount,
            long localizedEntriesAlreadyRead,
            string assetPath)
        {
            if (tableEntryCount < 0 ||
                localizedEntriesAlreadyRead < 0 ||
                localizedEntriesAlreadyRead > LocalizationKeyAuditLimits.MaximumLocalizedEntries ||
                tableEntryCount > LocalizationKeyAuditLimits.MaximumLocalizedEntries - localizedEntriesAlreadyRead)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"ローカライズ済み項目総数が上限 {LocalizationKeyAuditLimits.MaximumLocalizedEntries} 件を超えています: {assetPath}");
            }
        }

        /// <summary>コレクション／所属先なし表示の防御的複製前に、テーブルとローカライズ済み項目の残り上限を検証します。</summary>
        internal static void EnsureCollectionViewBudget(
            long tableCount,
            long localizedEntryCount,
            long tablesAlreadyAssigned,
            long localizedEntriesAlreadyAssigned,
            string assetPath)
        {
            if (tableCount < 0 ||
                tablesAlreadyAssigned < 0 ||
                tablesAlreadyAssigned > LocalizationKeyAuditLimits.MaximumLocaleTables ||
                tableCount > LocalizationKeyAuditLimits.MaximumLocaleTables - tablesAlreadyAssigned)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"コレクション表示のロケールテーブル総数が上限 {LocalizationKeyAuditLimits.MaximumLocaleTables} 件を超えています: {assetPath}");
            }

            if (localizedEntryCount < 0 ||
                localizedEntriesAlreadyAssigned < 0 ||
                localizedEntriesAlreadyAssigned > LocalizationKeyAuditLimits.MaximumLocalizedEntries ||
                localizedEntryCount > LocalizationKeyAuditLimits.MaximumLocalizedEntries - localizedEntriesAlreadyAssigned)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"コレクション表示のローカライズ済み項目総数が上限 {LocalizationKeyAuditLimits.MaximumLocalizedEntries} 件を超えています: {assetPath}");
            }
        }

        /// <summary>同じ共有テーブルデータのアセットパスを参照する直接テーブルを保持します。</summary>
        private sealed class LocaleTableGroup
        {
            /// <summary>識別情報と空のテーブル一覧を初期化します。</summary>
            internal LocaleTableGroup(string sharedDataAssetPath, Guid collectionGuid)
            {
                SharedDataAssetPath = sharedDataAssetPath;
                CollectionGuid = collectionGuid;
                Tables = new List<LocalizationKeyAuditLocaleTableSnapshot>();
            }

            /// <summary>共有テーブルデータのアセットパスです。</summary>
            internal string SharedDataAssetPath { get; }

            /// <summary>共有テーブルデータのコレクション識別子（GUID）です。</summary>
            internal Guid CollectionGuid { get; }

            /// <summary>この共有テーブルデータを参照する直接テーブルです。</summary>
            internal List<LocalizationKeyAuditLocaleTableSnapshot> Tables { get; }

            /// <summary>このまとまりへ一度だけ読み込まれたローカライズ済み項目総数です。</summary>
            internal long LocalizedEntryCount { get; set; }
        }
    }
}
