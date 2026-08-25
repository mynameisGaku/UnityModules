// SPDX-License-Identifier: MIT

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 1 Locale table の entry ID と direct localized value を保持します。
    /// </summary>
    internal sealed class LocalizationKeyAuditLocalizedEntrySnapshot
    {
        /// <summary>localized entry の不変 snapshot を作ります。</summary>
        internal LocalizationKeyAuditLocalizedEntrySnapshot(long id, string value)
        {
            Id = id;
            Value = value;
        }

        /// <summary>SharedTableData と対応付ける entry ID です。</summary>
        internal long Id { get; }

        /// <summary>fallback や runtime 解決を適用していない direct value です。</summary>
        internal string Value { get; }
    }
}

