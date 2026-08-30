// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 監査条件と型として読み取ったスナップショットを検証し、直接網羅グラフと参考問題を構築します。
    /// </summary>
    internal static class LocalizationKeyAuditAnalyzer
    {
        /// <summary>型としての読み取り処理を呼ぶ前に監査条件全体を検証します。</summary>
        internal static bool TryValidateRequest(
            LocalizationKeyAuditRequest request,
            out LocalizationKeyAuditIssue failure)
        {
            failure = null;
            if (request == null || request.Coverage == null)
            {
                failure = CreateConfigurationFailure("必須ロケールと静的参照網羅情報を指定してください。");
                return false;
            }

            if (request.RequiredLocaleIdentifiers.Count == 0)
            {
                failure = CreateConfigurationFailure("必須ロケールを1件以上指定してください。");
                return false;
            }

            if (request.RequiredLocaleIdentifiers.Count > LocalizationKeyAuditLimits.MaximumRequiredLocales)
            {
                failure = CreateLimitFailure($"必須ロケール数が上限 {LocalizationKeyAuditLimits.MaximumRequiredLocales} 件を超えています。");
                return false;
            }

            var locales = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < request.RequiredLocaleIdentifiers.Count; index++)
            {
                var locale = request.RequiredLocaleIdentifiers[index];
                if (!IsExactNonEmptyText(locale) || !locales.Add(locale))
                {
                    failure = CreateConfigurationFailure("必須ロケール識別子が空、長すぎる、前後に空白を含む、または重複しています。");
                    return false;
                }
            }

            var coverage = request.Coverage;
            if (!IsExactNonEmptyText(coverage.ScopeDescription))
            {
                failure = CreateConfigurationFailure("静的参照の走査範囲を説明してください。");
                return false;
            }

            if (coverage.IsComplete == !string.IsNullOrEmpty(coverage.IncompleteReason))
            {
                failure = CreateConfigurationFailure("静的参照網羅の完了状態と未完了理由が矛盾しています。");
                return false;
            }

            if (!coverage.IsComplete && !IsExactNonEmptyText(coverage.IncompleteReason))
            {
                failure = CreateConfigurationFailure("未完了の静的参照網羅には理由を指定してください。");
                return false;
            }

            if (coverage.DeclaredAssetPaths.Count == 0)
            {
                failure = CreateConfigurationFailure("静的参照の走査対象パスを1件以上宣言してください。");
                return false;
            }

            if (coverage.DeclaredAssetPaths.Count > LocalizationKeyAuditLimits.MaximumDeclaredAssetPaths)
            {
                failure = CreateLimitFailure($"宣言済みアセットパス数が上限 {LocalizationKeyAuditLimits.MaximumDeclaredAssetPaths} 件を超えています。");
                return false;
            }

            var declaredPaths = new HashSet<string>(StringComparer.Ordinal);
            var declaredRoot = string.Empty;
            for (var index = 0; index < coverage.DeclaredAssetPaths.Count; index++)
            {
                var path = coverage.DeclaredAssetPaths[index];
                if (!IsProjectAssetPath(path, true) || !declaredPaths.Add(path))
                {
                    failure = CreateConfigurationFailure("宣言済みアセットパスが不正または重複しています。");
                    return false;
                }

                var candidateRoot = GetCoverageRoot(path);
                if (declaredRoot.Length == 0)
                {
                    declaredRoot = candidateRoot;
                }
                else if (!string.Equals(declaredRoot, candidateRoot, StringComparison.Ordinal))
                {
                    failure = CreateConfigurationFailure(
                        "1回の監査で宣言できる論理ルートはAssetsまたは登録済みパッケージのいずれか1つだけです。");
                    return false;
                }
            }

            if (coverage.RecognizedReferences.Count > LocalizationKeyAuditLimits.MaximumStaticReferences)
            {
                failure = CreateLimitFailure($"静的参照数が上限 {LocalizationKeyAuditLimits.MaximumStaticReferences} 件を超えています。");
                return false;
            }

            var referenceIdentities = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < coverage.RecognizedReferences.Count; index++)
            {
                var reference = coverage.RecognizedReferences[index];
                if (reference == null ||
                    !IsProjectAssetPath(reference.SourceAssetPath, false) ||
                    !IsInsideDeclaredScope(reference.SourceAssetPath, coverage.DeclaredAssetPaths) ||
                    reference.CollectionGuid == Guid.Empty ||
                    reference.EntryId == 0 ||
                    !IsOptionalText(reference.CollectionName) ||
                    !IsOptionalText(reference.EntryKey))
                {
                    failure = CreateConfigurationFailure("静的参照に不正なパス、GUID、項目識別子、または表示文字列があります。");
                    return false;
                }

                var identity = reference.SourceAssetPath + "\0" +
                    reference.CollectionGuid.ToString("N") + "\0" +
                    reference.EntryId;
                if (!referenceIdentities.Add(identity))
                {
                    failure = CreateConfigurationFailure("同じ静的参照が複数回含まれています。");
                    return false;
                }
            }

            return true;
        }

        /// <summary>型として読み取ったスナップショット全体を検証してから完全な参考結果を構築します。</summary>
        internal static LocalizationKeyAuditResult Analyze(
            LocalizationKeyAuditRequest request,
            LocalizationKeyAuditTypedSnapshot snapshot,
            IReadOnlyList<LocalizationKeyAuditRawIdentity> rawIdentities)
        {
            if (request == null || snapshot == null || rawIdentities == null)
            {
                throw new InvalidDataException("監査条件、型として読み取ったスナップショット、または未加工の識別情報がありません。");
            }

            var locales = NormalizeLocales(snapshot.LocaleIdentifiers);
            var collections = NormalizeCollections(snapshot.Collections);
            var orphanLocaleTables = NormalizeOrphanLocaleTables(snapshot.OrphanLocaleTables);
            var nonStringSharedDataIdentities = NormalizeNonStringSharedDataIdentities(
                snapshot.NonStringSharedDataIdentities);
            var normalizedRawIdentities = NormalizeRawIdentities(rawIdentities);
            ValidateAggregateTableLimits(collections, orphanLocaleTables);
            ValidateRawIdentities(
                collections,
                orphanLocaleTables,
                nonStringSharedDataIdentities,
                normalizedRawIdentities);
            var stringRelevantRawIdentities = FilterStringRelevantRawIdentities(
                collections,
                orphanLocaleTables,
                nonStringSharedDataIdentities,
                normalizedRawIdentities);
            var graphEdgeCount = CountGraphEdges(request, collections, orphanLocaleTables);
            var issues = new List<LocalizationKeyAuditIssue>();

            AddLocaleIssues(request, locales, issues);
            AddCollectionIntegrityIssues(collections, stringRelevantRawIdentities, issues);
            AddRawGuidIntegrityIssues(stringRelevantRawIdentities, issues);
            AddOrphanIssues(collections, orphanLocaleTables, stringRelevantRawIdentities, issues);
            AddDirectCoverageIssues(request, collections, issues);
            AddStaticReferenceIssues(
                request.Coverage,
                collections,
                nonStringSharedDataIdentities,
                stringRelevantRawIdentities,
                issues);
            issues.Sort(CompareIssues);

            return new LocalizationKeyAuditResult(
                true,
                request.Coverage,
                locales,
                collections,
                issues,
                graphEdgeCount,
                orphanLocaleTables);
        }

        /// <summary>未加工識別情報を独立した複製にしてアセットパス、GUIDの順に並べます。</summary>
        private static List<LocalizationKeyAuditRawIdentity> NormalizeRawIdentities(
            IReadOnlyList<LocalizationKeyAuditRawIdentity> source)
        {
            var normalized = new List<LocalizationKeyAuditRawIdentity>(source.Count);
            for (var index = 0; index < source.Count; index++)
            {
                var identity = source[index];
                normalized.Add(identity == null
                    ? null
                    : new LocalizationKeyAuditRawIdentity(identity.AssetPath, identity.CollectionGuid));
            }

            normalized.Sort(CompareRawIdentities);
            return normalized;
        }

        /// <summary>アセットテーブルが所有する共有テーブルデータの識別情報を独立した複製にして決定論的に並べます。</summary>
        private static List<LocalizationKeyAuditNonStringSharedDataIdentity>
            NormalizeNonStringSharedDataIdentities(
                IReadOnlyList<LocalizationKeyAuditNonStringSharedDataIdentity> source)
        {
            if (source == null)
            {
                throw new InvalidDataException("型として読み取ったアセットテーブルの共有テーブルデータ識別情報一覧が未設定です。");
            }

            if (source.Count > LocalizationKeyAuditLimits.MaximumSharedTableDataAssets)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"アセットテーブルの共有テーブルデータ識別情報数が上限 {LocalizationKeyAuditLimits.MaximumSharedTableDataAssets} 件を超えています。");
            }

            var normalized = new List<LocalizationKeyAuditNonStringSharedDataIdentity>(source.Count);
            for (var index = 0; index < source.Count; index++)
            {
                var identity = source[index];
                if (identity == null ||
                    !IsUnityAssetPath(identity.AssetPath, false) ||
                    identity.CollectionGuid == Guid.Empty)
                {
                    throw new InvalidDataException("型として読み取ったアセットテーブルの共有テーブルデータ識別情報が未設定または不正です。");
                }

                normalized.Add(new LocalizationKeyAuditNonStringSharedDataIdentity(
                    identity.AssetPath,
                    identity.CollectionGuid));
            }

            normalized.Sort(CompareNonStringSharedDataIdentities);
            return normalized;
        }

        /// <summary>コレクションに属さない、型として読み取ったテーブルを検証して決定論的に並べます。</summary>
        private static List<LocalizationKeyAuditOrphanLocaleTableSnapshot> NormalizeOrphanLocaleTables(
            IReadOnlyList<LocalizationKeyAuditOrphanLocaleTableSnapshot> source)
        {
            if (source == null)
            {
                throw new InvalidDataException("型として読み取った所属先なしロケールテーブル一覧が未設定です。");
            }

            var normalized = new List<LocalizationKeyAuditOrphanLocaleTableSnapshot>(source.Count);
            for (var index = 0; index < source.Count; index++)
            {
                var orphan = source[index];
                var table = orphan?.LocaleTable;
                if (orphan == null ||
                    orphan.CollectionGuid == Guid.Empty ||
                    !IsUnityAssetPath(orphan.SharedDataAssetPath, false) ||
                    table == null ||
                    !IsExactNonEmptyText(table.LocaleIdentifier) ||
                    !IsUnityAssetPath(table.AssetPath, false) ||
                    table.Entries == null)
                {
                    throw new InvalidDataException("型として読み取った所属先なしロケールテーブルの識別情報が未設定または不正です。");
                }

                var entries = new List<LocalizationKeyAuditLocalizedEntrySnapshot>(table.Entries.Count);
                for (var entryIndex = 0; entryIndex < table.Entries.Count; entryIndex++)
                {
                    var entry = table.Entries[entryIndex];
                    if (entry == null || entry.Id == 0 ||
                        (entry.Value != null && entry.Value.Length > LocalizationKeyAuditLimits.MaximumLocalizedValueCharacters))
                    {
                        throw new InvalidDataException($"所属先なしのローカライズ済み項目が未設定、不正、または長すぎます: {table.AssetPath}");
                    }

                    entries.Add(new LocalizationKeyAuditLocalizedEntrySnapshot(entry.Id, entry.Value));
                }

                entries.Sort(CompareLocalizedEntries);
                normalized.Add(new LocalizationKeyAuditOrphanLocaleTableSnapshot(
                    orphan.SharedDataAssetPath,
                    orphan.CollectionGuid,
                    new LocalizationKeyAuditLocaleTableSnapshot(
                        table.LocaleIdentifier,
                        table.AssetPath,
                        entries)));
            }

            normalized.Sort(CompareOrphanLocaleTables);
            return normalized;
        }

        /// <summary>コレクションと所属先なし項目を合わせ、型として読み取ったテーブルおよび項目の上限を検証します。</summary>
        private static void ValidateAggregateTableLimits(
            IReadOnlyList<LocalizationKeyAuditCollectionSnapshot> collections,
            IReadOnlyList<LocalizationKeyAuditOrphanLocaleTableSnapshot> orphanLocaleTables)
        {
            long tableCount = orphanLocaleTables.Count;
            long entryCount = 0;
            for (var index = 0; index < orphanLocaleTables.Count; index++)
            {
                entryCount += orphanLocaleTables[index].LocaleTable.Entries.Count;
            }

            for (var collectionIndex = 0; collectionIndex < collections.Count; collectionIndex++)
            {
                var collection = collections[collectionIndex];
                tableCount += collection.LocaleTables.Count;
                for (var tableIndex = 0; tableIndex < collection.LocaleTables.Count; tableIndex++)
                {
                    entryCount += collection.LocaleTables[tableIndex].Entries.Count;
                }
            }

            if (tableCount > LocalizationKeyAuditLimits.MaximumLocaleTables ||
                entryCount > LocalizationKeyAuditLimits.MaximumLocalizedEntries)
            {
                throw new LocalizationKeyAuditLimitException("コレクションと所属先なし項目を合わせた、型として読み取ったテーブル数または項目数が上限を超えています。");
            }
        }

        /// <summary>ロケール識別子を検証して決定論的に並べます。</summary>
        private static List<string> NormalizeLocales(IReadOnlyList<string> source)
        {
            if (source == null)
            {
                throw new InvalidDataException("型として読み取ったロケール一覧が未設定です。");
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
                if (!IsExactNonEmptyText(locale))
                {
                    throw new InvalidDataException("型として読み取ったロケール識別子が空、不正、または長すぎます。");
                }

                locales.Add(locale);
            }

            locales.Sort(CompareLocaleIdentifiers);
            return locales;
        }

        /// <summary>コレクション階層を検証し、全階層を決定論的な複製にします。</summary>
        private static List<LocalizationKeyAuditCollectionSnapshot> NormalizeCollections(
            IReadOnlyList<LocalizationKeyAuditCollectionSnapshot> source)
        {
            if (source == null)
            {
                throw new InvalidDataException("型として読み取ったコレクション一覧が未設定です。");
            }

            if (source.Count > LocalizationKeyAuditLimits.MaximumCollections)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"コレクション数が上限 {LocalizationKeyAuditLimits.MaximumCollections} 件を超えています。");
            }

            var collections = new List<LocalizationKeyAuditCollectionSnapshot>(source.Count);
            long tableCount = 0;
            long sharedEntryCount = 0;
            long localizedEntryCount = 0;
            for (var index = 0; index < source.Count; index++)
            {
                var collection = source[index];
                if (collection == null ||
                    !IsExactNonEmptyText(collection.CollectionName) ||
                    collection.CollectionGuid == Guid.Empty ||
                    !IsUnityAssetPath(collection.SharedDataAssetPath, false))
                {
                    throw new InvalidDataException("型として読み取ったコレクション識別情報が空、不正、または長すぎます。");
                }

                if (collection.SharedEntries == null || collection.LocaleTables == null)
                {
                    throw new InvalidDataException($"型として読み取ったコレクションの子要素一覧が未設定です: {collection.CollectionName}");
                }

                sharedEntryCount += collection.SharedEntries.Count;
                if (sharedEntryCount > LocalizationKeyAuditLimits.MaximumSharedEntries)
                {
                    throw new LocalizationKeyAuditLimitException(
                        $"共有項目総数が上限 {LocalizationKeyAuditLimits.MaximumSharedEntries} 件を超えています。");
                }

                var sharedEntries = new List<LocalizationKeyAuditSharedEntrySnapshot>(collection.SharedEntries.Count);
                for (var entryIndex = 0; entryIndex < collection.SharedEntries.Count; entryIndex++)
                {
                    var entry = collection.SharedEntries[entryIndex];
                    if (entry == null || entry.Id == 0 || string.IsNullOrEmpty(entry.Key) ||
                        entry.Key.Length > LocalizationKeyAuditLimits.MaximumTextCharacters)
                    {
                        throw new InvalidDataException($"共有項目が未設定または不正です: {collection.CollectionName}");
                    }

                    sharedEntries.Add(new LocalizationKeyAuditSharedEntrySnapshot(entry.Id, entry.Key));
                }

                sharedEntries.Sort(CompareSharedEntries);
                tableCount += collection.LocaleTables.Count;
                if (tableCount > LocalizationKeyAuditLimits.MaximumLocaleTables)
                {
                    throw new LocalizationKeyAuditLimitException(
                        $"ロケールテーブル総数が上限 {LocalizationKeyAuditLimits.MaximumLocaleTables} 件を超えています。");
                }

                var tables = new List<LocalizationKeyAuditLocaleTableSnapshot>(collection.LocaleTables.Count);
                for (var tableIndex = 0; tableIndex < collection.LocaleTables.Count; tableIndex++)
                {
                    var table = collection.LocaleTables[tableIndex];
                    if (table == null ||
                        !IsExactNonEmptyText(table.LocaleIdentifier) ||
                        !IsUnityAssetPath(table.AssetPath, false) ||
                        table.Entries == null)
                    {
                        throw new InvalidDataException($"型として読み取ったロケールテーブルが未設定または不正です: {collection.CollectionName}");
                    }

                    localizedEntryCount += table.Entries.Count;
                    if (localizedEntryCount > LocalizationKeyAuditLimits.MaximumLocalizedEntries)
                    {
                        throw new LocalizationKeyAuditLimitException(
                            $"ローカライズ済み項目総数が上限 {LocalizationKeyAuditLimits.MaximumLocalizedEntries} 件を超えています。");
                    }

                    var entries = new List<LocalizationKeyAuditLocalizedEntrySnapshot>(table.Entries.Count);
                    for (var localizedIndex = 0; localizedIndex < table.Entries.Count; localizedIndex++)
                    {
                        var entry = table.Entries[localizedIndex];
                        if (entry == null || entry.Id == 0 ||
                            (entry.Value != null && entry.Value.Length > LocalizationKeyAuditLimits.MaximumLocalizedValueCharacters))
                        {
                            throw new InvalidDataException($"ローカライズ済み項目が未設定、不正、または長すぎます: {table.AssetPath}");
                        }

                        entries.Add(new LocalizationKeyAuditLocalizedEntrySnapshot(entry.Id, entry.Value));
                    }

                    entries.Sort(CompareLocalizedEntries);
                    tables.Add(new LocalizationKeyAuditLocaleTableSnapshot(
                        table.LocaleIdentifier,
                        table.AssetPath,
                        entries));
                }

                tables.Sort(CompareLocaleTables);
                collections.Add(new LocalizationKeyAuditCollectionSnapshot(
                    collection.CollectionName,
                    collection.CollectionGuid,
                    collection.SharedDataAssetPath,
                    sharedEntries,
                    tables));
            }

            collections.Sort(CompareCollections);
            return collections;
        }

        /// <summary>型として読み取ったコレクション識別情報が未加工の事前検査済みアセットと完全一致するかを調べます。</summary>
        private static void ValidateRawIdentities(
            IReadOnlyList<LocalizationKeyAuditCollectionSnapshot> collections,
            IReadOnlyList<LocalizationKeyAuditOrphanLocaleTableSnapshot> orphanLocaleTables,
            IReadOnlyList<LocalizationKeyAuditNonStringSharedDataIdentity> nonStringSharedDataIdentities,
            IReadOnlyList<LocalizationKeyAuditRawIdentity> rawIdentities)
        {
            if (rawIdentities.Count > LocalizationKeyAuditLimits.MaximumSharedTableDataAssets)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"未加工識別情報数が上限 {LocalizationKeyAuditLimits.MaximumSharedTableDataAssets} 件を超えています。");
            }

            var byPath = new Dictionary<string, LocalizationKeyAuditRawIdentity>(StringComparer.Ordinal);
            for (var index = 0; index < rawIdentities.Count; index++)
            {
                var identity = rawIdentities[index];
                if (identity == null ||
                    !IsUnityAssetPath(identity.AssetPath, false) ||
                    identity.CollectionGuid == Guid.Empty ||
                    byPath.ContainsKey(identity.AssetPath))
                {
                    throw new InvalidDataException("未加工事前検査の識別情報が未設定、不正、またはアセットパス重複です。");
                }

                byPath.Add(identity.AssetPath, identity);
            }

            for (var index = 0; index < collections.Count; index++)
            {
                var collection = collections[index];
                if (!byPath.TryGetValue(collection.SharedDataAssetPath, out var identity) ||
                    identity.CollectionGuid != collection.CollectionGuid)
                {
                    throw new InvalidDataException(
                        $"型として読み取ったコレクション識別情報が未加工の事前検査結果と一致しません: {collection.SharedDataAssetPath}");
                }
            }

            var collectionPaths = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < collections.Count; index++)
            {
                collectionPaths.Add(collections[index].SharedDataAssetPath);
            }

            for (var index = 0; index < orphanLocaleTables.Count; index++)
            {
                var orphan = orphanLocaleTables[index];
                if (collectionPaths.Contains(orphan.SharedDataAssetPath))
                {
                    throw new InvalidDataException($"コレクション所属テーブルが所属先なしテーブルとして重複しています: {orphan.LocaleTable.AssetPath}");
                }

                if (!byPath.TryGetValue(orphan.SharedDataAssetPath, out var identity) ||
                    identity.CollectionGuid != orphan.CollectionGuid)
                {
                    throw new InvalidDataException(
                        $"型として読み取った所属先なしテーブル識別情報が未加工の事前検査結果と一致しません: {orphan.SharedDataAssetPath}");
                }
            }

            var stringOwnedGuids = new HashSet<Guid>();
            for (var index = 0; index < collections.Count; index++)
            {
                stringOwnedGuids.Add(collections[index].CollectionGuid);
            }

            for (var index = 0; index < orphanLocaleTables.Count; index++)
            {
                stringOwnedGuids.Add(orphanLocaleTables[index].CollectionGuid);
            }

            var nonStringPaths = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < nonStringSharedDataIdentities.Count; index++)
            {
                var nonString = nonStringSharedDataIdentities[index];
                if (!nonStringPaths.Add(nonString.AssetPath))
                {
                    throw new InvalidDataException(
                        $"型として読み取ったアセットテーブルの共有テーブルデータ識別情報でアセットパスが重複しています: {nonString.AssetPath}");
                }

                if (!byPath.TryGetValue(nonString.AssetPath, out var identity) ||
                    identity.CollectionGuid != nonString.CollectionGuid)
                {
                    throw new InvalidDataException(
                        $"型として読み取ったアセットテーブルの共有テーブルデータ識別情報が未加工の事前検査結果と一致しません: {nonString.AssetPath}");
                }

                if (stringOwnedGuids.Contains(nonString.CollectionGuid))
                {
                    throw new InvalidDataException(
                        $"文字列テーブルとアセットテーブルが同じコレクション識別子（GUID）を使用しているため静的参照の種類を一意に判定できません: {nonString.CollectionGuid:N}");
                }
            }

        }

        /// <summary>
        /// アセットテーブルだけが所有すると確認された未加工の識別情報を、文字列キーの重複・所属先なし・静的参照解決から除外します。
        /// 所有元不明の未加工識別情報は保守的に残し、文字列／アセット共用GUIDはこの処理より前に安全側で停止します。
        /// </summary>
        private static List<LocalizationKeyAuditRawIdentity> FilterStringRelevantRawIdentities(
            IReadOnlyList<LocalizationKeyAuditCollectionSnapshot> collections,
            IReadOnlyList<LocalizationKeyAuditOrphanLocaleTableSnapshot> orphanLocaleTables,
            IReadOnlyList<LocalizationKeyAuditNonStringSharedDataIdentity> nonStringSharedDataIdentities,
            IReadOnlyList<LocalizationKeyAuditRawIdentity> rawIdentities)
        {
            var stringOwnedPaths = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < collections.Count; index++)
            {
                stringOwnedPaths.Add(collections[index].SharedDataAssetPath);
            }

            for (var index = 0; index < orphanLocaleTables.Count; index++)
            {
                stringOwnedPaths.Add(orphanLocaleTables[index].SharedDataAssetPath);
            }

            var nonStringOwnedPaths = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < nonStringSharedDataIdentities.Count; index++)
            {
                nonStringOwnedPaths.Add(nonStringSharedDataIdentities[index].AssetPath);
            }

            var relevant = new List<LocalizationKeyAuditRawIdentity>(rawIdentities.Count);
            for (var index = 0; index < rawIdentities.Count; index++)
            {
                var identity = rawIdentities[index];
                if (nonStringOwnedPaths.Contains(identity.AssetPath) &&
                    !stringOwnedPaths.Contains(identity.AssetPath))
                {
                    continue;
                }

                relevant.Add(new LocalizationKeyAuditRawIdentity(
                    identity.AssetPath,
                    identity.CollectionGuid));
            }

            return relevant;
        }

        /// <summary>直接網羅、テーブル所属、静的参照の辺数を上限内で数えます。</summary>
        private static long CountGraphEdges(
            LocalizationKeyAuditRequest request,
            IReadOnlyList<LocalizationKeyAuditCollectionSnapshot> collections,
            IReadOnlyList<LocalizationKeyAuditOrphanLocaleTableSnapshot> orphanLocaleTables)
        {
            long edgeCount = request.Coverage.RecognizedReferences.Count;
            for (var index = 0; index < collections.Count; index++)
            {
                var collection = collections[index];
                AddGraphEdges(ref edgeCount, collection.LocaleTables.Count);
                AddGraphEdges(
                    ref edgeCount,
                    MultiplyGraphEdges(collection.SharedEntries.Count, request.RequiredLocaleIdentifiers.Count));
                for (var tableIndex = 0; tableIndex < collection.LocaleTables.Count; tableIndex++)
                {
                    AddGraphEdges(ref edgeCount, collection.LocaleTables[tableIndex].Entries.Count);
                }
            }

            for (var index = 0; index < orphanLocaleTables.Count; index++)
            {
                AddGraphEdges(ref edgeCount, 1);
                AddGraphEdges(ref edgeCount, orphanLocaleTables[index].LocaleTable.Entries.Count);
            }

            return edgeCount;
        }

        /// <summary>乗算のオーバーフローとグラフ上限を検証します。</summary>
        private static long MultiplyGraphEdges(int left, int right)
        {
            try
            {
                return checked((long)left * right);
            }
            catch (OverflowException exception)
            {
                throw new LocalizationKeyAuditLimitException("直接網羅の参照関係数が数値上限を超えています。", exception);
            }
        }

        /// <summary>加算のオーバーフローとグラフ上限を検証します。</summary>
        private static void AddGraphEdges(ref long edgeCount, long addition)
        {
            try
            {
                edgeCount = checked(edgeCount + addition);
            }
            catch (OverflowException exception)
            {
                throw new LocalizationKeyAuditLimitException("参照関係数が数値上限を超えています。", exception);
            }

            if (edgeCount > LocalizationKeyAuditLimits.MaximumGraphEdges)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"参照関係数が上限 {LocalizationKeyAuditLimits.MaximumGraphEdges} 件を超えています。");
            }
        }

        /// <summary>設定済みロケールの重複と必須ロケール不足を追加します。</summary>
        private static void AddLocaleIssues(
            LocalizationKeyAuditRequest request,
            IReadOnlyList<string> locales,
            List<LocalizationKeyAuditIssue> issues)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < locales.Count; index++)
            {
                counts.TryGetValue(locales[index], out var count);
                counts[locales[index]] = count + 1;
            }

            foreach (var pair in counts)
            {
                if (pair.Value > 1)
                {
                    AddIssue(issues, new LocalizationKeyAuditIssue(
                        LocalizationKeyAuditIssueKind.DuplicateLocaleIdentifier,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        Guid.Empty,
                        pair.Key,
                        string.Empty,
                        0,
                        $"ローカライズ設定にロケール「{pair.Key}」が {pair.Value} 件登録されています。"));
                }
            }

            for (var index = 0; index < request.RequiredLocaleIdentifiers.Count; index++)
            {
                var required = request.RequiredLocaleIdentifiers[index];
                if (!counts.ContainsKey(required))
                {
                    AddIssue(issues, new LocalizationKeyAuditIssue(
                        LocalizationKeyAuditIssueKind.RequiredLocaleNotConfigured,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        Guid.Empty,
                        required,
                        string.Empty,
                        0,
                        $"必須ロケール「{required}」がローカライズ設定に登録されていません。"));
                }
            }
        }

        /// <summary>コレクション、テーブル、項目の重複と所属先なしのローカライズ済み項目を追加します。</summary>
        private static void AddCollectionIntegrityIssues(
            IReadOnlyList<LocalizationKeyAuditCollectionSnapshot> collections,
            IReadOnlyList<LocalizationKeyAuditRawIdentity> rawIdentities,
            List<LocalizationKeyAuditIssue> issues)
        {
            var names = BuildCollectionNameIndex(collections);
            var guids = BuildCollectionGuidIndex(collections);
            var rawGuids = BuildRawGuidIndex(rawIdentities);
            AddDuplicateCollectionIssues(
                collections,
                names,
                LocalizationKeyAuditIssueKind.DuplicateCollectionName,
                issues);
            foreach (var pair in guids)
            {
                if (pair.Value.Count < 2 ||
                    (rawGuids.TryGetValue(pair.Key, out var rawIndices) && rawIndices.Count > 1))
                {
                    continue;
                }

                for (var duplicateIndex = 0; duplicateIndex < pair.Value.Count; duplicateIndex++)
                {
                    var collection = collections[pair.Value[duplicateIndex]];
                    var related = collections[pair.Value[duplicateIndex == 0 ? 1 : 0]];
                    AddIssue(issues, CreateCollectionIssue(
                        LocalizationKeyAuditIssueKind.DuplicateCollectionGuid,
                        collection,
                        collection.SharedDataAssetPath,
                        related.SharedDataAssetPath,
                        string.Empty,
                        string.Empty,
                        0,
                        $"型として読み取ったコレクションの識別子（GUID）が {pair.Value.Count} 件のコレクションで重複しています。"));
                }
            }

            for (var index = 0; index < collections.Count; index++)
            {
                var collection = collections[index];
                var sharedIds = BuildSharedIdIndex(collection.SharedEntries);
                var sharedKeys = BuildSharedKeyIndex(collection.SharedEntries);
                foreach (var pair in sharedIds)
                {
                    if (pair.Value.Count > 1)
                    {
                        var entry = collection.SharedEntries[pair.Value[0]];
                        AddIssue(issues, CreateCollectionIssue(
                            LocalizationKeyAuditIssueKind.DuplicateSharedEntryId,
                            collection,
                            collection.SharedDataAssetPath,
                            string.Empty,
                            string.Empty,
                            entry.Key,
                            entry.Id,
                            $"共有項目識別子「{entry.Id}」が {pair.Value.Count} 件あります。"));
                    }
                }

                foreach (var pair in sharedKeys)
                {
                    if (pair.Value.Count > 1)
                    {
                        var entry = collection.SharedEntries[pair.Value[0]];
                        AddIssue(issues, CreateCollectionIssue(
                            LocalizationKeyAuditIssueKind.DuplicateSharedEntryKey,
                            collection,
                            collection.SharedDataAssetPath,
                            string.Empty,
                            string.Empty,
                            entry.Key,
                            entry.Id,
                            $"共有項目キー「{entry.Key}」が {pair.Value.Count} 件あります。"));
                    }
                }

                var localeTables = BuildLocaleTableIndex(collection.LocaleTables);
                foreach (var pair in localeTables)
                {
                    if (pair.Value.Count > 1)
                    {
                        var first = collection.LocaleTables[pair.Value[0]];
                        var second = collection.LocaleTables[pair.Value[1]];
                        AddIssue(issues, CreateCollectionIssue(
                            LocalizationKeyAuditIssueKind.DuplicateLocaleTable,
                            collection,
                            first.AssetPath,
                            second.AssetPath,
                            first.LocaleIdentifier,
                            string.Empty,
                            0,
                            $"ロケール「{first.LocaleIdentifier}」のテーブルが {pair.Value.Count} 件あります。直接網羅は一意に判定しません。"));
                    }
                }

                var sharedIdSet = new HashSet<long>(sharedIds.Keys);
                for (var tableIndex = 0; tableIndex < collection.LocaleTables.Count; tableIndex++)
                {
                    var table = collection.LocaleTables[tableIndex];
                    var localizedIds = BuildLocalizedIdIndex(table.Entries);
                    foreach (var pair in localizedIds)
                    {
                        if (pair.Value.Count > 1)
                        {
                            AddIssue(issues, CreateCollectionIssue(
                                LocalizationKeyAuditIssueKind.DuplicateLocalizedEntryId,
                                collection,
                                table.AssetPath,
                                collection.SharedDataAssetPath,
                                table.LocaleIdentifier,
                                string.Empty,
                                pair.Key,
                                $"ローカライズ済み項目識別子「{pair.Key}」が {pair.Value.Count} 件あります。直接網羅は一意に判定しません。"));
                        }

                        if (!sharedIdSet.Contains(pair.Key))
                        {
                            AddIssue(issues, CreateCollectionIssue(
                                LocalizationKeyAuditIssueKind.OrphanedLocalizedEntry,
                                collection,
                                table.AssetPath,
                                collection.SharedDataAssetPath,
                                table.LocaleIdentifier,
                                string.Empty,
                                pair.Key,
                                "ローカライズ済み項目識別子が共有テーブルデータに存在しません。"));
                        }
                    }
                }
            }
        }

        /// <summary>コレクションに属さないテーブルと、型として読み取ったオブジェクトに対応しない未加工の共有テーブルデータを追加します。</summary>
        private static void AddOrphanIssues(
            IReadOnlyList<LocalizationKeyAuditCollectionSnapshot> collections,
            IReadOnlyList<LocalizationKeyAuditOrphanLocaleTableSnapshot> orphanLocaleTables,
            IReadOnlyList<LocalizationKeyAuditRawIdentity> rawIdentities,
            List<LocalizationKeyAuditIssue> issues)
        {
            var observedSharedPaths = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < collections.Count; index++)
            {
                observedSharedPaths.Add(collections[index].SharedDataAssetPath);
            }

            for (var index = 0; index < orphanLocaleTables.Count; index++)
            {
                var orphan = orphanLocaleTables[index];
                observedSharedPaths.Add(orphan.SharedDataAssetPath);
                AddIssue(issues, new LocalizationKeyAuditIssue(
                    LocalizationKeyAuditIssueKind.OrphanedLocaleTable,
                    orphan.LocaleTable.AssetPath,
                    orphan.SharedDataAssetPath,
                    string.Empty,
                    orphan.CollectionGuid,
                    orphan.LocaleTable.LocaleIdentifier,
                    string.Empty,
                    0,
                    "型として読み取った文字列テーブルに対応する文字列テーブルコレクションが見つかりません。"));
            }

            for (var index = 0; index < rawIdentities.Count; index++)
            {
                var identity = rawIdentities[index];
                if (observedSharedPaths.Contains(identity.AssetPath))
                {
                    continue;
                }

                AddIssue(issues, new LocalizationKeyAuditIssue(
                    LocalizationKeyAuditIssueKind.OrphanedSharedTableData,
                    identity.AssetPath,
                    string.Empty,
                    string.Empty,
                    identity.CollectionGuid,
                    string.Empty,
                    string.Empty,
                    0,
                    "有効な未加工の共有テーブルデータに対応する、型として読み取ったコレクションまたはテーブルが見つかりません。"));
            }

        }

        /// <summary>異なる未加工パスが同じコレクション識別子（GUID）を持つ曖昧性をパスごとに追加します。</summary>
        private static void AddRawGuidIntegrityIssues(
            IReadOnlyList<LocalizationKeyAuditRawIdentity> rawIdentities,
            List<LocalizationKeyAuditIssue> issues)
        {
            var rawByGuid = BuildRawGuidIndex(rawIdentities);
            foreach (var pair in rawByGuid)
            {
                if (pair.Value.Count < 2)
                {
                    continue;
                }

                for (var duplicateIndex = 0; duplicateIndex < pair.Value.Count; duplicateIndex++)
                {
                    var current = rawIdentities[pair.Value[duplicateIndex]];
                    var related = rawIdentities[pair.Value[duplicateIndex == 0 ? 1 : 0]];
                    AddIssue(issues, new LocalizationKeyAuditIssue(
                        LocalizationKeyAuditIssueKind.DuplicateCollectionGuid,
                        current.AssetPath,
                        related.AssetPath,
                        string.Empty,
                        current.CollectionGuid,
                        string.Empty,
                        string.Empty,
                        0,
                        $"異なる共有テーブルデータのパス {pair.Value.Count} 件に同じコレクション識別子（GUID）が記録されています。"));
                }
            }
        }

        /// <summary>必須ロケールごとの不足テーブル、不足項目、未設定または空の直接値を追加します。</summary>
        private static void AddDirectCoverageIssues(
            LocalizationKeyAuditRequest request,
            IReadOnlyList<LocalizationKeyAuditCollectionSnapshot> collections,
            List<LocalizationKeyAuditIssue> issues)
        {
            var requiredLocales = new List<string>(request.RequiredLocaleIdentifiers);
            requiredLocales.Sort(CompareLocaleIdentifiers);
            for (var collectionIndex = 0; collectionIndex < collections.Count; collectionIndex++)
            {
                var collection = collections[collectionIndex];
                var tablesByLocale = BuildLocaleTableIndex(collection.LocaleTables);
                var sharedIds = BuildSharedIdIndex(collection.SharedEntries);
                for (var localeIndex = 0; localeIndex < requiredLocales.Count; localeIndex++)
                {
                    var locale = requiredLocales[localeIndex];
                    if (!tablesByLocale.TryGetValue(locale, out var tableIndices) || tableIndices.Count == 0)
                    {
                        AddIssue(issues, CreateCollectionIssue(
                            LocalizationKeyAuditIssueKind.MissingLocaleTable,
                            collection,
                            collection.SharedDataAssetPath,
                            string.Empty,
                            locale,
                            string.Empty,
                            0,
                            $"必須ロケール「{locale}」の直接の文字列テーブルがありません。実行時の代替処理結果は判定していません。"));
                        continue;
                    }

                    if (tableIndices.Count != 1)
                    {
                        continue;
                    }

                    var table = collection.LocaleTables[tableIndices[0]];
                    var localizedIds = BuildLocalizedIdIndex(table.Entries);
                    for (var entryIndex = 0; entryIndex < collection.SharedEntries.Count; entryIndex++)
                    {
                        var sharedEntry = collection.SharedEntries[entryIndex];
                        if (sharedIds[sharedEntry.Id].Count != 1)
                        {
                            continue;
                        }

                        if (!localizedIds.TryGetValue(sharedEntry.Id, out var localizedIndices) || localizedIndices.Count == 0)
                        {
                            AddIssue(issues, CreateCollectionIssue(
                                LocalizationKeyAuditIssueKind.MissingDirectEntry,
                                collection,
                                table.AssetPath,
                                collection.SharedDataAssetPath,
                                locale,
                                sharedEntry.Key,
                                sharedEntry.Id,
                                "必須ロケールテーブルに共有項目識別子と対応する直接項目がありません。実行時の代替処理結果は判定していません。"));
                            continue;
                        }

                        if (localizedIndices.Count != 1)
                        {
                            continue;
                        }

                        var localizedEntry = table.Entries[localizedIndices[0]];
                        if (string.IsNullOrEmpty(localizedEntry.Value))
                        {
                            AddIssue(issues, CreateCollectionIssue(
                                LocalizationKeyAuditIssueKind.EmptyDirectValue,
                                collection,
                                table.AssetPath,
                                collection.SharedDataAssetPath,
                                locale,
                                sharedEntry.Key,
                                sharedEntry.Id,
                                "直接のローカライズ値が未設定または空です。空白文字だけの値と実行時の代替処理結果は別扱いです。"));
                        }
                    }
                }
            }
        }

        /// <summary>静的参照網羅の完了状態と認識済みGUIDおよび項目識別子参照を検証します。</summary>
        private static void AddStaticReferenceIssues(
            LocalizationKeyAuditCoverage coverage,
            IReadOnlyList<LocalizationKeyAuditCollectionSnapshot> collections,
            IReadOnlyList<LocalizationKeyAuditNonStringSharedDataIdentity> nonStringSharedDataIdentities,
            IReadOnlyList<LocalizationKeyAuditRawIdentity> rawIdentities,
            List<LocalizationKeyAuditIssue> issues)
        {
            if (!coverage.IsComplete)
            {
                AddIssue(issues, new LocalizationKeyAuditIssue(
                    LocalizationKeyAuditIssueKind.StaticReferenceCoverageIncomplete,
                    coverage.DeclaredAssetPaths[0],
                    string.Empty,
                    string.Empty,
                    Guid.Empty,
                    string.Empty,
                    string.Empty,
                    0,
                    $"宣言済み走査範囲の静的参照網羅は未完了です: {coverage.IncompleteReason}"));

                return;
            }
            var collectionsByGuid = BuildCollectionGuidIndex(collections);
            var rawByGuid = BuildRawGuidIndex(rawIdentities);
            var nonStringGuids = new HashSet<Guid>();
            for (var index = 0; index < nonStringSharedDataIdentities.Count; index++)
            {
                nonStringGuids.Add(nonStringSharedDataIdentities[index].CollectionGuid);
            }

            var sharedIdsByCollection = new Dictionary<long, List<int>>[collections.Count];
            for (var collectionIndex = 0; collectionIndex < collections.Count; collectionIndex++)
            {
                sharedIdsByCollection[collectionIndex] = BuildSharedIdIndex(collections[collectionIndex].SharedEntries);
            }

            var referencedIdsByGuid = new Dictionary<Guid, HashSet<long>>();
            for (var index = 0; index < coverage.RecognizedReferences.Count; index++)
            {
                var reference = coverage.RecognizedReferences[index];
                if (!referencedIdsByGuid.TryGetValue(reference.CollectionGuid, out var referencedIds))
                {
                    referencedIds = new HashSet<long>();
                    referencedIdsByGuid.Add(reference.CollectionGuid, referencedIds);
                }

                referencedIds.Add(reference.EntryId);
                if (nonStringGuids.Contains(reference.CollectionGuid))
                {
                    continue;
                }

                if (!collectionsByGuid.TryGetValue(reference.CollectionGuid, out var collectionIndices) ||
                    collectionIndices.Count != 1 ||
                    !rawByGuid.TryGetValue(reference.CollectionGuid, out var rawIndices) ||
                    rawIndices.Count != 1)
                {
                    AddIssue(issues, new LocalizationKeyAuditIssue(
                        LocalizationKeyAuditIssueKind.DanglingStaticReference,
                        reference.SourceAssetPath,
                        string.Empty,
                        reference.CollectionName,
                        reference.CollectionGuid,
                        string.Empty,
                        reference.EntryKey,
                        reference.EntryId,
                        "静的参照のコレクション識別子（GUID）を一意に解決できません。"));
                    continue;
                }

                var collection = collections[collectionIndices[0]];
                var sharedIds = sharedIdsByCollection[collectionIndices[0]];
                if (!sharedIds.TryGetValue(reference.EntryId, out var entryIndices) || entryIndices.Count != 1)
                {
                    AddIssue(issues, new LocalizationKeyAuditIssue(
                        LocalizationKeyAuditIssueKind.DanglingStaticReference,
                        reference.SourceAssetPath,
                        collection.SharedDataAssetPath,
                        collection.CollectionName,
                        collection.CollectionGuid,
                        string.Empty,
                        reference.EntryKey,
                        reference.EntryId,
                    "静的参照の項目識別子を共有テーブルデータで一意に解決できません。"));
                }
            }

            for (var collectionIndex = 0; collectionIndex < collections.Count; collectionIndex++)
            {
                var collection = collections[collectionIndex];
                if (!collectionsByGuid.TryGetValue(collection.CollectionGuid, out var collectionIndices) ||
                    collectionIndices.Count != 1 ||
                    !rawByGuid.TryGetValue(collection.CollectionGuid, out var rawIndices) ||
                    rawIndices.Count != 1 ||
                    !string.Equals(
                        rawIdentities[rawIndices[0]].AssetPath,
                        collection.SharedDataAssetPath,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                referencedIdsByGuid.TryGetValue(collection.CollectionGuid, out var referencedIds);
                var sharedIds = sharedIdsByCollection[collectionIndex];
                foreach (var pair in sharedIds)
                {
                    if (pair.Value.Count != 1 || (referencedIds != null && referencedIds.Contains(pair.Key)))
                    {
                        continue;
                    }

                    var entry = collection.SharedEntries[pair.Value[0]];
                    AddIssue(issues, CreateCollectionIssue(
                        LocalizationKeyAuditIssueKind.NoStaticReferenceFoundWithinDeclaredScope,
                        collection,
                        collection.SharedDataAssetPath,
                        coverage.DeclaredAssetPaths[0],
                        string.Empty,
                        entry.Key,
                        entry.Id,
                        $"宣言済み走査範囲「{coverage.ScopeDescription}」内で、この一意なGUIDと項目識別子への認識済み静的参照が見つかりません。項目が未使用とは断定していません。"));
                }
            }
        }

        /// <summary>コレクション名から全添字への重複保持対応表を作ります。</summary>
        private static Dictionary<string, List<int>> BuildCollectionNameIndex(
            IReadOnlyList<LocalizationKeyAuditCollectionSnapshot> collections)
        {
            var index = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (var collectionIndex = 0; collectionIndex < collections.Count; collectionIndex++)
            {
                AddIndex(index, collections[collectionIndex].CollectionName, collectionIndex);
            }

            return index;
        }

        /// <summary>コレクション識別子（GUID）から全添字への重複保持対応表を作ります。</summary>
        private static Dictionary<Guid, List<int>> BuildCollectionGuidIndex(
            IReadOnlyList<LocalizationKeyAuditCollectionSnapshot> collections)
        {
            var index = new Dictionary<Guid, List<int>>();
            for (var collectionIndex = 0; collectionIndex < collections.Count; collectionIndex++)
            {
                AddIndex(index, collections[collectionIndex].CollectionGuid, collectionIndex);
            }

            return index;
        }

        /// <summary>未加工コレクション識別子（GUID）から全識別情報添字への重複保持対応表を作ります。</summary>
        private static Dictionary<Guid, List<int>> BuildRawGuidIndex(
            IReadOnlyList<LocalizationKeyAuditRawIdentity> rawIdentities)
        {
            var index = new Dictionary<Guid, List<int>>();
            for (var identityIndex = 0; identityIndex < rawIdentities.Count; identityIndex++)
            {
                AddIndex(index, rawIdentities[identityIndex].CollectionGuid, identityIndex);
            }

            return index;
        }

        /// <summary>共有項目識別子から全添字への重複保持対応表を作ります。</summary>
        private static Dictionary<long, List<int>> BuildSharedIdIndex(
            IReadOnlyList<LocalizationKeyAuditSharedEntrySnapshot> entries)
        {
            var index = new Dictionary<long, List<int>>();
            for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                AddIndex(index, entries[entryIndex].Id, entryIndex);
            }

            return index;
        }

        /// <summary>共有項目キーから全添字への重複保持対応表を作ります。</summary>
        private static Dictionary<string, List<int>> BuildSharedKeyIndex(
            IReadOnlyList<LocalizationKeyAuditSharedEntrySnapshot> entries)
        {
            var index = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                AddIndex(index, entries[entryIndex].Key, entryIndex);
            }

            return index;
        }

        /// <summary>ロケール識別子から全テーブル添字への重複保持対応表を作ります。</summary>
        private static Dictionary<string, List<int>> BuildLocaleTableIndex(
            IReadOnlyList<LocalizationKeyAuditLocaleTableSnapshot> tables)
        {
            var index = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            for (var tableIndex = 0; tableIndex < tables.Count; tableIndex++)
            {
                AddIndex(index, tables[tableIndex].LocaleIdentifier, tableIndex);
            }

            return index;
        }

        /// <summary>ローカライズ済み項目識別子から全添字への重複保持対応表を作ります。</summary>
        private static Dictionary<long, List<int>> BuildLocalizedIdIndex(
            IReadOnlyList<LocalizationKeyAuditLocalizedEntrySnapshot> entries)
        {
            var index = new Dictionary<long, List<int>>();
            for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                AddIndex(index, entries[entryIndex].Id, entryIndex);
            }

            return index;
        }

        /// <summary>重複保持添字へ項目添字を1件追加します。</summary>
        private static void AddIndex<TKey>(Dictionary<TKey, List<int>> index, TKey key, int itemIndex)
        {
            if (!index.TryGetValue(key, out var values))
            {
                values = new List<int>();
                index.Add(key, values);
            }

            values.Add(itemIndex);
        }

        /// <summary>名前またはGUIDの重複に属する各コレクションへ問題を追加します。</summary>
        private static void AddDuplicateCollectionIssues<TKey>(
            IReadOnlyList<LocalizationKeyAuditCollectionSnapshot> collections,
            IReadOnlyDictionary<TKey, List<int>> index,
            LocalizationKeyAuditIssueKind kind,
            List<LocalizationKeyAuditIssue> issues)
        {
            foreach (var pair in index)
            {
                if (pair.Value.Count < 2)
                {
                    continue;
                }

                for (var duplicateIndex = 0; duplicateIndex < pair.Value.Count; duplicateIndex++)
                {
                    var collection = collections[pair.Value[duplicateIndex]];
                    var related = collections[pair.Value[duplicateIndex == 0 ? 1 : 0]];
                    AddIssue(issues, CreateCollectionIssue(
                        kind,
                        collection,
                        collection.SharedDataAssetPath,
                        related.SharedDataAssetPath,
                        string.Empty,
                        string.Empty,
                        0,
                        kind == LocalizationKeyAuditIssueKind.DuplicateCollectionName
                            ? $"コレクション名「{collection.CollectionName}」が一意ではありません。"
                            : $"コレクション識別子（GUID）「{collection.CollectionGuid:N}」が一意ではありません。"));
                }
            }
        }

        /// <summary>コレクション識別情報を補った問題を作ります。</summary>
        private static LocalizationKeyAuditIssue CreateCollectionIssue(
            LocalizationKeyAuditIssueKind kind,
            LocalizationKeyAuditCollectionSnapshot collection,
            string assetPath,
            string relatedAssetPath,
            string localeIdentifier,
            string entryKey,
            long entryId,
            string message)
        {
            return new LocalizationKeyAuditIssue(
                kind,
                assetPath,
                relatedAssetPath,
                collection.CollectionName,
                collection.CollectionGuid,
                localeIdentifier,
                entryKey,
                entryId,
                message);
        }

        /// <summary>問題数の上限を超えない場合だけ追加します。</summary>
        private static void AddIssue(List<LocalizationKeyAuditIssue> issues, LocalizationKeyAuditIssue issue)
        {
            if (issues.Count >= LocalizationKeyAuditLimits.MaximumIssues)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"問題数が上限 {LocalizationKeyAuditLimits.MaximumIssues} 件を超えています。");
            }

            issues.Add(issue);
        }

        /// <summary>ロケール識別子を大文字小文字を区別しない順、区別する順で並べます。</summary>
        private static int CompareLocaleIdentifiers(string left, string right)
        {
            var comparison = string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
            return comparison != 0
                ? comparison
                : string.Compare(left, right, StringComparison.Ordinal);
        }

        /// <summary>共有項目をID、キーの順に並べます。</summary>
        private static int CompareSharedEntries(
            LocalizationKeyAuditSharedEntrySnapshot left,
            LocalizationKeyAuditSharedEntrySnapshot right)
        {
            var comparison = left.Id.CompareTo(right.Id);
            return comparison != 0
                ? comparison
                : string.Compare(left.Key, right.Key, StringComparison.Ordinal);
        }

        /// <summary>ローカライズ済み項目をID、値の順に並べます。</summary>
        private static int CompareLocalizedEntries(
            LocalizationKeyAuditLocalizedEntrySnapshot left,
            LocalizationKeyAuditLocalizedEntrySnapshot right)
        {
            var comparison = left.Id.CompareTo(right.Id);
            return comparison != 0
                ? comparison
                : string.Compare(left.Value, right.Value, StringComparison.Ordinal);
        }

        /// <summary>所属先なしテーブルを共有データのパス、ロケール、テーブルパス、GUIDの順に並べます。</summary>
        private static int CompareOrphanLocaleTables(
            LocalizationKeyAuditOrphanLocaleTableSnapshot left,
            LocalizationKeyAuditOrphanLocaleTableSnapshot right)
        {
            var comparison = string.Compare(left.SharedDataAssetPath, right.SharedDataAssetPath, StringComparison.Ordinal);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = CompareLocaleIdentifiers(left.LocaleTable.LocaleIdentifier, right.LocaleTable.LocaleIdentifier);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = string.Compare(left.LocaleTable.AssetPath, right.LocaleTable.AssetPath, StringComparison.Ordinal);
            return comparison != 0
                ? comparison
                : left.CollectionGuid.CompareTo(right.CollectionGuid);
        }

        /// <summary>未加工識別情報を未設定、アセットパス、GUIDの順に並べます。</summary>
        private static int CompareRawIdentities(
            LocalizationKeyAuditRawIdentity left,
            LocalizationKeyAuditRawIdentity right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            var comparison = string.Compare(left.AssetPath, right.AssetPath, StringComparison.Ordinal);
            return comparison != 0 ? comparison : left.CollectionGuid.CompareTo(right.CollectionGuid);
        }

        /// <summary>アセットテーブルの共有データ識別情報をアセットパス、GUIDの順に並べます。</summary>
        private static int CompareNonStringSharedDataIdentities(
            LocalizationKeyAuditNonStringSharedDataIdentity left,
            LocalizationKeyAuditNonStringSharedDataIdentity right)
        {
            var comparison = string.Compare(left.AssetPath, right.AssetPath, StringComparison.Ordinal);
            return comparison != 0 ? comparison : left.CollectionGuid.CompareTo(right.CollectionGuid);
        }

        /// <summary>ロケールテーブルをロケール、アセットパスの順に並べます。</summary>
        private static int CompareLocaleTables(
            LocalizationKeyAuditLocaleTableSnapshot left,
            LocalizationKeyAuditLocaleTableSnapshot right)
        {
            var comparison = CompareLocaleIdentifiers(left.LocaleIdentifier, right.LocaleIdentifier);
            return comparison != 0
                ? comparison
                : string.Compare(left.AssetPath, right.AssetPath, StringComparison.Ordinal);
        }

        /// <summary>コレクションを名前、GUID、共有データのパスの順に並べます。</summary>
        private static int CompareCollections(
            LocalizationKeyAuditCollectionSnapshot left,
            LocalizationKeyAuditCollectionSnapshot right)
        {
            var comparison = string.Compare(left.CollectionName, right.CollectionName, StringComparison.Ordinal);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.CollectionGuid.CompareTo(right.CollectionGuid);
            return comparison != 0
                ? comparison
                : string.Compare(left.SharedDataAssetPath, right.SharedDataAssetPath, StringComparison.Ordinal);
        }

        /// <summary>問題を全表示項目の固定順に並べます。</summary>
        private static int CompareIssues(LocalizationKeyAuditIssue left, LocalizationKeyAuditIssue right)
        {
            var comparison = left.Kind.CompareTo(right.Kind);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = string.Compare(left.CollectionName, right.CollectionName, StringComparison.Ordinal);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.CollectionGuid.CompareTo(right.CollectionGuid);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = CompareLocaleIdentifiers(left.LocaleIdentifier, right.LocaleIdentifier);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = string.Compare(left.EntryKey, right.EntryKey, StringComparison.Ordinal);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.EntryId.CompareTo(right.EntryId);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = string.Compare(left.AssetPath, right.AssetPath, StringComparison.Ordinal);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = string.Compare(left.RelatedAssetPath, right.RelatedAssetPath, StringComparison.Ordinal);
            return comparison != 0
                ? comparison
                : string.Compare(left.Message, right.Message, StringComparison.Ordinal);
        }

        /// <summary>空でなく前後空白を含まない制限内文字列かを調べます。</summary>
        private static bool IsExactNonEmptyText(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                value.Length <= LocalizationKeyAuditLimits.MaximumTextCharacters &&
                string.Equals(value, value.Trim(), StringComparison.Ordinal);
        }

        /// <summary>空を許す制限内文字列かを調べます。</summary>
        private static bool IsOptionalText(string value)
        {
            return value != null && value.Length <= LocalizationKeyAuditLimits.MaximumTextCharacters;
        }

        /// <summary>Unity相対パスとして安全な区間だけを持つかを調べます。</summary>
        private static bool IsUnityAssetPath(string path, bool allowRoot)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                path.Length > LocalizationKeyAuditLimits.MaximumTextCharacters ||
                path.IndexOf('\\') >= 0 ||
                path.IndexOf('\0') >= 0)
            {
                return false;
            }

            if (allowRoot && (path == "Assets" || path == "Packages"))
            {
                return true;
            }

            if (!path.StartsWith("Assets/", StringComparison.Ordinal) &&
                !path.StartsWith("Packages/", StringComparison.Ordinal))
            {
                return false;
            }

            var segments = path.Split('/');
            for (var index = 0; index < segments.Length; index++)
            {
                if (segments[index].Length == 0 || segments[index] == "." || segments[index] == "..")
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>静的参照網羅が対応するAssetsまたはPackagesパスかを調べます。</summary>
        private static bool IsProjectAssetPath(string path, bool allowRoot)
        {
            if (!IsUnityAssetPath(path, allowRoot))
            {
                return false;
            }

            var segments = path.Split('/');
            for (var index = 0; index < segments.Length; index++)
            {
                if (segments[index].IndexOf('~') >= 0 || segments[index].IndexOf(':') >= 0 ||
                    segments[index].EndsWith(".", StringComparison.Ordinal) ||
                    segments[index].EndsWith(" ", StringComparison.Ordinal))
                {
                    return false;
                }
            }

            if (path == "Assets" || path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return true;
            }

            if (!path.StartsWith("Packages/", StringComparison.Ordinal))
            {
                return false;
            }

            var minimumSegments = allowRoot ? 2 : 3;
            if (segments.Length < minimumSegments)
            {
                return false;
            }

            return true;
        }

        /// <summary>網羅対象パスをAssetsまたはPackages内のパッケージ名ルートへ正規化します。</summary>
        private static string GetCoverageRoot(string path)
        {
            if (path == "Assets" || path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return "Assets";
            }

            var separator = path.IndexOf('/', "Packages/".Length);
            return separator < 0 ? path : path.Substring(0, separator);
        }

        /// <summary>参照元パスが宣言済みアセットまたはフォルダーの内側かを調べます。</summary>
        private static bool IsInsideDeclaredScope(string sourcePath, IReadOnlyList<string> declaredPaths)
        {
            for (var index = 0; index < declaredPaths.Count; index++)
            {
                var declaredPath = declaredPaths[index];
                if (string.Equals(sourcePath, declaredPath, StringComparison.Ordinal) ||
                    sourcePath.StartsWith(declaredPath.TrimEnd('/') + "/", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>設定不正を表す監査停止問題を作ります。</summary>
        private static LocalizationKeyAuditIssue CreateConfigurationFailure(string message)
        {
            return CreateTerminalIssue(LocalizationKeyAuditIssueKind.InvalidConfiguration, message);
        }

        /// <summary>上限超過を表す監査停止問題を作ります。</summary>
        private static LocalizationKeyAuditIssue CreateLimitFailure(string message)
        {
            return CreateTerminalIssue(LocalizationKeyAuditIssueKind.LimitExceeded, message);
        }

        /// <summary>共通項目が空の監査停止問題を作ります。</summary>
        private static LocalizationKeyAuditIssue CreateTerminalIssue(LocalizationKeyAuditIssueKind kind, string message)
        {
            return new LocalizationKeyAuditIssue(
                kind,
                string.Empty,
                string.Empty,
                string.Empty,
                Guid.Empty,
                string.Empty,
                string.Empty,
                0,
                message);
        }
    }
}
