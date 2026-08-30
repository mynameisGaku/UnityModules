// SPDX-License-Identifier: MIT

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 共有テーブルデータの項目識別子とキーを保持します。
    /// </summary>
    internal sealed class LocalizationKeyAuditSharedEntrySnapshot
    {
        /// <summary>共有項目の不変スナップショットを作ります。</summary>
        internal LocalizationKeyAuditSharedEntrySnapshot(long id, string key)
        {
            Id = id;
            Key = key ?? string.Empty;
        }

        /// <summary>コレクション内で使う項目識別子です。</summary>
        internal long Id { get; }

        /// <summary>表示と名前検索に使う項目キーです。</summary>
        internal string Key { get; }
    }
}
