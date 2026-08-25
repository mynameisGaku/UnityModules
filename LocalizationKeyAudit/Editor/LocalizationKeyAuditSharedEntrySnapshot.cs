// SPDX-License-Identifier: MIT

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// SharedTableData の entry ID と key を保持します。
    /// </summary>
    internal sealed class LocalizationKeyAuditSharedEntrySnapshot
    {
        /// <summary>shared entry の不変 snapshot を作ります。</summary>
        internal LocalizationKeyAuditSharedEntrySnapshot(long id, string key)
        {
            Id = id;
            Key = key ?? string.Empty;
        }

        /// <summary>collection 内で使う entry ID です。</summary>
        internal long Id { get; }

        /// <summary>表示と name lookup に使う entry key です。</summary>
        internal string Key { get; }
    }
}

