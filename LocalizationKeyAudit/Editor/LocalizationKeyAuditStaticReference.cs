// SPDX-License-Identifier: MIT

using System;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 宣言済み scope で認識した table GUID と entry ID の直接参照です。
    /// </summary>
    internal sealed class LocalizationKeyAuditStaticReference
    {
        /// <summary>
        /// 一意な参照 identity と任意の表示用 name/key を保持します。
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

        /// <summary>参照を認識した source asset path です。</summary>
        internal string SourceAssetPath { get; }

        /// <summary>参照先 table collection の GUID です。</summary>
        internal Guid CollectionGuid { get; }

        /// <summary>参照先 shared entry の ID です。</summary>
        internal long EntryId { get; }

        /// <summary>診断表示にだけ使う collection 名です。</summary>
        internal string CollectionName { get; }

        /// <summary>診断表示にだけ使う entry key です。</summary>
        internal string EntryKey { get; }
    }
}

