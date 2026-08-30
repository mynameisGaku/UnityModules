// SPDX-License-Identifier: MIT

using System;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 宣言済み走査範囲で認識したテーブルGUIDと項目識別子の直接参照です。
    /// </summary>
    internal sealed class LocalizationKeyAuditStaticReference
    {
        /// <summary>
        /// 一意な参照識別情報と任意の表示用名前およびキーを保持します。
        /// </summary>
        internal LocalizationKeyAuditStaticReference(
            string sourceAssetPath,
            Guid collectionGuid,
            long entryId,
            string collectionName,
            string entryKey)
        {
            SourceAssetPath = sourceAssetPath ?? string.Empty;
            CollectionGuid = collectionGuid;
            EntryId = entryId;
            CollectionName = collectionName ?? string.Empty;
            EntryKey = entryKey ?? string.Empty;
        }

        /// <summary>参照を認識した参照元アセットパスです。</summary>
        internal string SourceAssetPath { get; }

        /// <summary>参照先テーブルコレクションのGUIDです。</summary>
        internal Guid CollectionGuid { get; }

        /// <summary>参照先共有項目のIDです。</summary>
        internal long EntryId { get; }

        /// <summary>診断表示にだけ使うコレクション名です。</summary>
        internal string CollectionName { get; }

        /// <summary>診断表示にだけ使う項目キーです。</summary>
        internal string EntryKey { get; }
    }
}
