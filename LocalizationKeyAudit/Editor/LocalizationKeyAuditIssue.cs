// SPDX-License-Identifier: MIT

using System;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 手動監査で検出した 1 件の問題を保持します。
    /// </summary>
    internal sealed class LocalizationKeyAuditIssue
    {
        /// <summary>
        /// 問題の識別情報と表示用説明を不変な値として保持します。
        /// </summary>
        internal LocalizationKeyAuditIssue(
            LocalizationKeyAuditIssueKind kind,
            string assetPath,
            string relatedAssetPath,
            string collectionName,
            Guid collectionGuid,
            string localeIdentifier,
            string entryKey,
            long entryId,
            string message)
        {
            Kind = kind;
            AssetPath = assetPath ?? string.Empty;
            RelatedAssetPath = relatedAssetPath ?? string.Empty;
            CollectionName = collectionName ?? string.Empty;
            CollectionGuid = collectionGuid;
            LocaleIdentifier = localeIdentifier ?? string.Empty;
            EntryKey = entryKey ?? string.Empty;
            EntryId = entryId;
            Message = message ?? string.Empty;
        }

        /// <summary>問題種別です。</summary>
        internal LocalizationKeyAuditIssueKind Kind { get; }

        /// <summary>問題の発生元アセットパスです。</summary>
        internal string AssetPath { get; }

        /// <summary>関連するテーブルまたは共有テーブルデータのアセットパスです。</summary>
        internal string RelatedAssetPath { get; }

        /// <summary>関連するテーブルコレクション名です。</summary>
        internal string CollectionName { get; }

        /// <summary>関連するテーブルのコレクション識別子（GUID）です。</summary>
        internal Guid CollectionGuid { get; }

        /// <summary>関連するロケール識別子です。</summary>
        internal string LocaleIdentifier { get; }

        /// <summary>関連する共有項目キーです。</summary>
        internal string EntryKey { get; }

        /// <summary>関連する共有項目識別子です。</summary>
        internal long EntryId { get; }

        /// <summary>断定範囲を限定した表示用説明です。</summary>
        internal string Message { get; }
    }
}
