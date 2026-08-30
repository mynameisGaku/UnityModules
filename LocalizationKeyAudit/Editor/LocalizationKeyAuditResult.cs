// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 手動監査の完了状態、静的参照網羅、型として読み取ったスナップショット、問題を保持します。
    /// </summary>
    internal sealed class LocalizationKeyAuditResult
    {
        /// <summary>結果全体を独立した読み取り専用スナップショットにします。</summary>
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

        /// <summary>未加工事前検査、型としての読み取り、上限検査を含む監査処理が完了したかを示します。</summary>
        internal bool IsComplete { get; }

        /// <summary>静的参照の宣言済み範囲と完了状態です。</summary>
        internal LocalizationKeyAuditCoverage Coverage { get; }

        /// <summary>ローカライズ設定から取得したロケール識別子です。</summary>
        internal IReadOnlyList<string> LocaleIdentifiers { get; }

        /// <summary>決定論的な順序に並ぶ文字列テーブルコレクションのスナップショットです。</summary>
        internal IReadOnlyList<LocalizationKeyAuditCollectionSnapshot> Collections { get; }

        /// <summary>コレクションに所属しない、型として読み取った文字列テーブルのスナップショットです。</summary>
        internal IReadOnlyList<LocalizationKeyAuditOrphanLocaleTableSnapshot> OrphanLocaleTables { get; }

        /// <summary>決定論的な順序に並ぶ助言用の問題です。</summary>
        internal IReadOnlyList<LocalizationKeyAuditIssue> Issues { get; }

        /// <summary>上限検証済みの直接網羅と静的参照の参照関係数です。</summary>
        internal long GraphEdgeCount { get; }

        /// <summary>静的参照網羅を取得できない失敗用スナップショットを作ります。</summary>
        internal static LocalizationKeyAuditCoverage CreateUnavailableCoverage()
        {
            return new LocalizationKeyAuditCoverage(
                "未指定",
                Array.Empty<string>(),
                Array.Empty<LocalizationKeyAuditStaticReference>(),
                false,
                "監査条件を取得できませんでした。");
        }

        /// <summary>文字列一覧を読み取り専用の複製にします。</summary>
        private static IReadOnlyList<string> CopyStrings(IReadOnlyList<string> values)
        {
            var copy = new string[values?.Count ?? 0];
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = values[index] ?? string.Empty;
            }

            return new ReadOnlyCollection<string>(copy);
        }

        /// <summary>コレクション一覧を読み取り専用の複製にします。</summary>
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

        /// <summary>所属先なしテーブル一覧を読み取り専用の複製にします。</summary>
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

        /// <summary>問題一覧を読み取り専用の複製にします。</summary>
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
