// SPDX-License-Identifier: MIT

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 1つのロケールテーブルの項目識別子と直接のローカライズ値を保持します。
    /// </summary>
    internal sealed class LocalizationKeyAuditLocalizedEntrySnapshot
    {
        /// <summary>ローカライズ済み項目の不変スナップショットを作ります。</summary>
        internal LocalizationKeyAuditLocalizedEntrySnapshot(long id, string value)
        {
            Id = id;
            Value = value;
        }

        /// <summary>共有テーブルデータと対応付ける項目識別子です。</summary>
        internal long Id { get; }

        /// <summary>代替処理や実行時解決を適用していない直接値です。</summary>
        internal string Value { get; }
    }
}
