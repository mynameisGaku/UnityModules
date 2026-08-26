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
    /// raw preflight 後に公式 Localization API を読み取り専用で immutable DTO へ変換します。
    /// </summary>
    internal sealed class UnityLocalizationKeyAuditTypedSource : ILocalizationKeyAuditTypedSource
    {
        /// <summary>
        /// LocaleとString collectionはLocalizationEditorSettings、tableとAsset ownerはdirect asset enumerationで読みます。
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

        /// <summary>Localization Settings の Locale identifiers を読みます。</summary>
        private static IReadOnlyList<string> ReadLocales()
        {
            var source = LocalizationEditorSettings.GetLocales();
            if (source == null)
            {
                throw new InvalidDataException("LocalizationEditorSettings.GetLocales が null を返しました。");
            }

            if (source.Count > LocalizationKeyAuditLimits.MaximumLocales)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"Locale 数が上限 {LocalizationKeyAuditLimits.MaximumLocales} 件を超えています。");
            }

            var locales = new List<string>(source.Count);
            for (var index = 0; index < source.Count; index++)
            {
                var locale = source[index];
                if (locale == null)
                {
                    throw new InvalidDataException("Localization Settings の Locale 一覧に null が含まれています。");
                }

                locales.Add(locale.Identifier.Code);
            }

            return locales;
        }

        /// <summary>
        /// mutation path を持つ StringTableCollection.StringTables/Tables/GetTable を避け、全 StringTable asset を直接読みます。
        /// </summary>
        private static Dictionary<string, LocaleTableGroup> ReadLocaleTables()
        {
            var guids = AssetDatabase.FindAssets("t:StringTable") ?? Array.Empty<string>();
            if (guids.Length > LocalizationKeyAuditLimits.MaximumLocaleTables)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"StringTable 数が上限 {LocalizationKeyAuditLimits.MaximumLocaleTables} 件を超えています。");
            }

            Array.Sort(guids, StringComparer.Ordinal);
            var tables = new Dictionary<string, LocaleTableGroup>(StringComparer.Ordinal);
            long localizedEntryCount = 0;
            for (var index = 0; index < guids.Length; index++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[index]);
                if (string.IsNullOrEmpty(assetPath))
                {
                    throw new InvalidDataException($"StringTable GUID {guids[index]} の asset path を取得できません。");
                }

                var table = AssetDatabase.LoadAssetAtPath<StringTable>(assetPath);
                if (table == null || table.SharedData == null)
                {
                    throw new InvalidDataException($"StringTable または SharedTableData を typed load できません: {assetPath}");
                }

                var entries = ReadSerializedEntries(table, assetPath, localizedEntryCount);
                localizedEntryCount += entries.Count;

                var collectionGuid = table.SharedData.TableCollectionNameGuid;
                var sharedDataAssetPath = AssetDatabase.GetAssetPath(table.SharedData);
                if (string.IsNullOrEmpty(sharedDataAssetPath))
                {
                    throw new InvalidDataException($"StringTable の SharedTableData asset path を取得できません: {assetPath}");
                }

                if (!tables.TryGetValue(sharedDataAssetPath, out var group))
                {
                    group = new LocaleTableGroup(sharedDataAssetPath, collectionGuid);
                    tables.Add(sharedDataAssetPath, group);
                }
                else if (group.CollectionGuid != collectionGuid)
                {
                    throw new InvalidDataException($"同じ SharedTableData path に異なる collection GUID が見つかりました: {sharedDataAssetPath}");
                }

                group.LocalizedEntryCount += entries.Count;
                group.Tables.Add(new LocalizationKeyAuditLocaleTableSnapshot(
                    table.LocaleIdentifier.Code,
                    assetPath,
                    entries));
            }

            return tables;
        }

        /// <summary>StringTableCollection と SharedTableData entries を公式 Editor API から読みます。</summary>
        private static IReadOnlyList<LocalizationKeyAuditCollectionSnapshot> ReadCollections(
            IReadOnlyDictionary<string, LocaleTableGroup> tablesBySharedDataPath,
            out IReadOnlyList<LocalizationKeyAuditOrphanLocaleTableSnapshot> orphanLocaleTables)
        {
            var guids = AssetDatabase.FindAssets("t:StringTableCollection") ?? Array.Empty<string>();
            if (guids.Length > LocalizationKeyAuditLimits.MaximumCollections)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"StringTableCollection 数が上限 {LocalizationKeyAuditLimits.MaximumCollections} 件を超えています。");
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
                    throw new InvalidDataException($"StringTableCollection GUID {guids[index]} の asset path を取得できません。");
                }

                var collection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(collectionAssetPath);
                if (collection == null || collection.SharedData == null)
                {
                    throw new InvalidDataException($"StringTableCollection または SharedTableData を typed load できません: {collectionAssetPath}");
                }

                var sharedData = collection.SharedData;
                var sharedAssetPath = AssetDatabase.GetAssetPath(sharedData);
                if (string.IsNullOrEmpty(sharedAssetPath))
                {
                    throw new InvalidDataException($"SharedTableData asset path を取得できません: {collection.name}");
                }

                var sourceEntries = sharedData.Entries;
                if (sourceEntries == null)
                {
                    throw new InvalidDataException($"SharedTableData entries が null です: {sharedAssetPath}");
                }

                sharedEntryCount += sourceEntries.Count;
                if (sharedEntryCount > LocalizationKeyAuditLimits.MaximumSharedEntries)
                {
                    throw new LocalizationKeyAuditLimitException(
                        $"shared entry 総数が上限 {LocalizationKeyAuditLimits.MaximumSharedEntries} 件を超えています。");
                }

                var sharedEntries = new List<LocalizationKeyAuditSharedEntrySnapshot>(sourceEntries.Count);
                for (var entryIndex = 0; entryIndex < sourceEntries.Count; entryIndex++)
                {
                    var entry = sourceEntries[entryIndex];
                    if (entry == null)
                    {
                        throw new InvalidDataException($"SharedTableData に null entry が含まれています: {sharedAssetPath}");
                    }

                    sharedEntries.Add(new LocalizationKeyAuditSharedEntrySnapshot(entry.Id, entry.Key));
                }

                var collectionGuid = sharedData.TableCollectionNameGuid;
                var localeTables = tablesBySharedDataPath.TryGetValue(sharedAssetPath, out var foundGroup)
                    ? foundGroup.Tables
                    : new List<LocalizationKeyAuditLocaleTableSnapshot>();
                if (foundGroup != null && foundGroup.CollectionGuid != collectionGuid)
                {
                    throw new InvalidDataException($"collection と direct table の GUID が一致しません: {sharedAssetPath}");
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
        /// direct coverage 対象外の AssetTable/AssetTableCollection owner を読み、SharedTableData identity だけを保持します。
        /// collection の Tables/AssetTables property は mutation path を持つため呼びません。
        /// </summary>
        private static IReadOnlyList<LocalizationKeyAuditNonStringSharedDataIdentity>
            ReadNonStringSharedDataIdentities()
        {
            var identities = new Dictionary<string, Guid>(StringComparer.Ordinal);
            var tableGuids = AssetDatabase.FindAssets("t:AssetTable") ?? Array.Empty<string>();
            if (tableGuids.Length > LocalizationKeyAuditLimits.MaximumLocaleTables)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"AssetTable 数が上限 {LocalizationKeyAuditLimits.MaximumLocaleTables} 件を超えています。");
            }

            Array.Sort(tableGuids, StringComparer.Ordinal);
            for (var index = 0; index < tableGuids.Length; index++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(tableGuids[index]);
                if (string.IsNullOrEmpty(assetPath))
                {
                    throw new InvalidDataException($"AssetTable GUID {tableGuids[index]} の asset path を取得できません。");
                }

                var table = AssetDatabase.LoadAssetAtPath<AssetTable>(assetPath);
                if (table == null || table.SharedData == null)
                {
                    throw new InvalidDataException($"AssetTable または SharedTableData を typed load できません: {assetPath}");
                }

                AddNonStringSharedDataIdentity(identities, table.SharedData, assetPath);
            }

            var collectionGuids = AssetDatabase.FindAssets("t:AssetTableCollection") ?? Array.Empty<string>();
            if (collectionGuids.Length > LocalizationKeyAuditLimits.MaximumCollections)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"AssetTableCollection 数が上限 {LocalizationKeyAuditLimits.MaximumCollections} 件を超えています。");
            }

            Array.Sort(collectionGuids, StringComparer.Ordinal);
            for (var index = 0; index < collectionGuids.Length; index++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(collectionGuids[index]);
                if (string.IsNullOrEmpty(assetPath))
                {
                    throw new InvalidDataException(
                        $"AssetTableCollection GUID {collectionGuids[index]} の asset path を取得できません。");
                }

                var collection = AssetDatabase.LoadAssetAtPath<AssetTableCollection>(assetPath);
                if (collection == null || collection.SharedData == null)
                {
                    throw new InvalidDataException(
                        $"AssetTableCollection または SharedTableData を typed load できません: {assetPath}");
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

        /// <summary>Asset Table owner が参照する SharedTableData path/GUID を一意に保持します。</summary>
        private static void AddNonStringSharedDataIdentity(
            IDictionary<string, Guid> identities,
            SharedTableData sharedData,
            string ownerAssetPath)
        {
            var sharedDataAssetPath = AssetDatabase.GetAssetPath(sharedData);
            if (string.IsNullOrEmpty(sharedDataAssetPath))
            {
                throw new InvalidDataException(
                    $"Asset Table owner の SharedTableData asset path を取得できません: {ownerAssetPath}");
            }

            var collectionGuid = sharedData.TableCollectionNameGuid;
            if (collectionGuid == Guid.Empty)
            {
                throw new InvalidDataException(
                    $"Asset Table owner の SharedTableData collection GUID が空です: {sharedDataAssetPath}");
            }

            if (identities.TryGetValue(sharedDataAssetPath, out var existing))
            {
                if (existing != collectionGuid)
                {
                    throw new InvalidDataException(
                        $"同じ Asset Table SharedTableData path に異なる collection GUID が見つかりました: {sharedDataAssetPath}");
                }

                return;
            }

            if (identities.Count >= LocalizationKeyAuditLimits.MaximumSharedTableDataAssets)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"Asset Table SharedTableData 数が上限 {LocalizationKeyAuditLimits.MaximumSharedTableDataAssets} 件を超えています。");
            }

            identities.Add(sharedDataAssetPath, collectionGuid);
        }

        /// <summary>serialized m_TableData を直接読み、Dictionary 化で失われる重複 ID も保持します。</summary>
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
                    throw new InvalidDataException($"StringTable の serialized m_TableData を取得できません: {assetPath}");
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
                        throw new InvalidDataException($"StringTable entry の m_Id/m_Localized を取得できません: {assetPath}");
                    }

                    var value = localized.stringValue;
                    if (value != null && value.Length > LocalizationKeyAuditLimits.MaximumLocalizedValueCharacters)
                    {
                        throw new LocalizationKeyAuditLimitException(
                            $"localized value が文字数上限 {LocalizationKeyAuditLimits.MaximumLocalizedValueCharacters} を超えています: {assetPath}");
                    }

                    entries.Add(new LocalizationKeyAuditLocalizedEntrySnapshot(id.longValue, value));
                }

                return entries;
            }
        }

        /// <summary>次のtableをcopyする前にaggregate localized entryの残budgetを検証します。</summary>
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
                    $"localized entry 総数が上限 {LocalizationKeyAuditLimits.MaximumLocalizedEntries} 件を超えています: {assetPath}");
            }
        }

        /// <summary>collection/orphan viewの防御copy前にtableとlocalized entryのaggregate残budgetを検証します。</summary>
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
                    $"collection viewのLocale table総数が上限 {LocalizationKeyAuditLimits.MaximumLocaleTables} 件を超えています: {assetPath}");
            }

            if (localizedEntryCount < 0 ||
                localizedEntriesAlreadyAssigned < 0 ||
                localizedEntriesAlreadyAssigned > LocalizationKeyAuditLimits.MaximumLocalizedEntries ||
                localizedEntryCount > LocalizationKeyAuditLimits.MaximumLocalizedEntries - localizedEntriesAlreadyAssigned)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"collection viewのlocalized entry総数が上限 {LocalizationKeyAuditLimits.MaximumLocalizedEntries} 件を超えています: {assetPath}");
            }
        }

        /// <summary>同じ SharedTableData path を参照する direct table を保持します。</summary>
        private sealed class LocaleTableGroup
        {
            /// <summary>identity と空 table 一覧を初期化します。</summary>
            internal LocaleTableGroup(string sharedDataAssetPath, Guid collectionGuid)
            {
                SharedDataAssetPath = sharedDataAssetPath;
                CollectionGuid = collectionGuid;
                Tables = new List<LocalizationKeyAuditLocaleTableSnapshot>();
            }

            /// <summary>SharedTableData asset path です。</summary>
            internal string SharedDataAssetPath { get; }

            /// <summary>SharedTableData の collection GUID です。</summary>
            internal Guid CollectionGuid { get; }

            /// <summary>この SharedTableData を参照する direct tables です。</summary>
            internal List<LocalizationKeyAuditLocaleTableSnapshot> Tables { get; }

            /// <summary>このgroupに一度だけ読み込まれたlocalized entry総数です。</summary>
            internal long LocalizedEntryCount { get; set; }
        }
    }
}
