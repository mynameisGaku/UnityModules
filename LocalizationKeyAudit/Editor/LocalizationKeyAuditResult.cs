// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 手動監査の完了状態、coverage、typed snapshot、issue を保持します。
    /// </summary>
    internal sealed class LocalizationKeyAuditResult
    {
        /// <summary>結果全体を独立した読み取り専用 snapshot にします。</summary>
        internal LocalizationKeyAuditResult(
            bool isComplete,
            LocalizationKeyAuditCoverage coverage,
            IReadOnlyList<string> localeIdentifiers,
            IReadOnlyList<LocalizationKeyAuditCollectionSnapshot> collections,
            IReadOnlyList<LocalizationKeyAuditIssue> issues,
            long graphEdgeCount,
            IReadOnlyList<LocalizationKeyAuditOrphanLocaleTableSnapshot> orphanLocaleTables = null)
        {
            IsComplete = isComplete;
            Coverage = coverage?.Copy() ?? CreateUnavailableCoverage();
            LocaleIdentifiers = CopyStrings(localeIdentifiers);
            Collections = CopyCollections(collections);
            OrphanLocaleTables = CopyOrphanLocaleTables(orphanLocaleTables);
            Issues = CopyIssues(issues);
            GraphEdgeCount = graphEdgeCount;
        }

        /// <summary>raw、typed、limit を含む監査処理が完了したかを示します。</summary>
        internal bool IsComplete { get; }

        /// <summary>static reference の宣言済み scope と完了状態です。</summary>
        internal LocalizationKeyAuditCoverage Coverage { get; }

        /// <summary>Localization Settings から取得した Locale identifiers です。</summary>
        internal IReadOnlyList<string> LocaleIdentifiers { get; }

        /// <summary>決定論的な順序に並ぶ StringTableCollection snapshots です。</summary>
        internal IReadOnlyList<LocalizationKeyAuditCollectionSnapshot> Collections { get; }

        /// <summary>collection に所属しない typed StringTable snapshots です。</summary>
        internal IReadOnlyList<LocalizationKeyAuditOrphanLocaleTableSnapshot> OrphanLocaleTables { get; }

        /// <summary>決定論的な順序に並ぶ advisory issues です。</summary>
        internal IReadOnlyList<LocalizationKeyAuditIssue> Issues { get; }

        /// <summary>上限検証済みの direct coverage と static reference edge 数です。</summary>
        internal long GraphEdgeCount { get; }

        /// <summary>coverage を取得できない failure 用 snapshot を作ります。</summary>
        internal static LocalizationKeyAuditCoverage CreateUnavailableCoverage()
        {
            return new LocalizationKeyAuditCoverage(
                "未指定",
                Array.Empty<string>(),
                Array.Empty<LocalizationKeyAuditStaticReference>(),
                false,
                "監査 request を取得できませんでした。");
        }

        /// <summary>文字列一覧を読み取り専用 copy にします。</summary>
        private static IReadOnlyList<string> CopyStrings(IReadOnlyList<string> values)
        {
            var copy = new string[values?.Count ?? 0];
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = values[index] ?? string.Empty;
            }

            return new ReadOnlyCollection<string>(copy);
        }

        /// <summary>collection 一覧を読み取り専用 copy にします。</summary>
        private static IReadOnlyList<LocalizationKeyAuditCollectionSnapshot> CopyCollections(
            IReadOnlyList<LocalizationKeyAuditCollectionSnapshot> values)
        {
            var copy = new LocalizationKeyAuditCollectionSnapshot[values?.Count ?? 0];
            for (var index = 0; index < copy.Length; index++)
            {
                var value = values[index];
                copy[index] = value == null
                    ? null
                    : new LocalizationKeyAuditCollectionSnapshot(
                        value.CollectionName,
                        value.CollectionGuid,
                        value.SharedDataAssetPath,
                        value.SharedEntries,
                        value.LocaleTables);
            }

            return new ReadOnlyCollection<LocalizationKeyAuditCollectionSnapshot>(copy);
        }

        /// <summary>orphan table 一覧を読み取り専用 copy にします。</summary>
        private static IReadOnlyList<LocalizationKeyAuditOrphanLocaleTableSnapshot> CopyOrphanLocaleTables(
            IReadOnlyList<LocalizationKeyAuditOrphanLocaleTableSnapshot> values)
        {
            var copy = new LocalizationKeyAuditOrphanLocaleTableSnapshot[values?.Count ?? 0];
            for (var index = 0; index < copy.Length; index++)
            {
                var value = values[index];
                copy[index] = value == null
                    ? null
                    : new LocalizationKeyAuditOrphanLocaleTableSnapshot(
                        value.SharedDataAssetPath,
                        value.CollectionGuid,
                        value.LocaleTable);
            }

            return new ReadOnlyCollection<LocalizationKeyAuditOrphanLocaleTableSnapshot>(copy);
        }

        /// <summary>issue 一覧を読み取り専用 copy にします。</summary>
        private static IReadOnlyList<LocalizationKeyAuditIssue> CopyIssues(IReadOnlyList<LocalizationKeyAuditIssue> values)
        {
            var copy = new LocalizationKeyAuditIssue[values?.Count ?? 0];
            for (var index = 0; index < copy.Length; index++)
            {
                var value = values[index];
                copy[index] = value == null
                    ? null
                    : new LocalizationKeyAuditIssue(
                        value.Kind,
                        value.AssetPath,
                        value.RelatedAssetPath,
                        value.CollectionName,
                        value.CollectionGuid,
                        value.LocaleIdentifier,
                        value.EntryKey,
                        value.EntryId,
                        value.Message);
            }

            return new ReadOnlyCollection<LocalizationKeyAuditIssue>(copy);
        }
    }
}
