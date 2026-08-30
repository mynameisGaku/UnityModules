// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 1つのロケールの文字列テーブルアセットと直接項目一覧を保持します。
    /// </summary>
    internal sealed class LocalizationKeyAuditLocaleTableSnapshot
    {
        /// <summary>テーブルと項目一覧を防御的に複製します。</summary>
        internal LocalizationKeyAuditLocaleTableSnapshot(
            string localeIdentifier,
            string assetPath,
            IReadOnlyList<LocalizationKeyAuditLocalizedEntrySnapshot> entries)
        {
            LocaleIdentifier = localeIdentifier ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            var copy = new LocalizationKeyAuditLocalizedEntrySnapshot[entries?.Count ?? 0];
            for (var index = 0; index < copy.Length; index++)
            {
                var entry = entries[index];
                copy[index] = entry == null
                    ? null
                    : new LocalizationKeyAuditLocalizedEntrySnapshot(entry.Id, entry.Value);
            }

            Entries = new ReadOnlyCollection<LocalizationKeyAuditLocalizedEntrySnapshot>(copy);
        }

        /// <summary>文字列テーブルのロケール識別子です。</summary>
        internal string LocaleIdentifier { get; }

        /// <summary>文字列テーブルのアセットパスです。</summary>
        internal string AssetPath { get; }

        /// <summary>代替処理を適用していない直接項目一覧です。</summary>
        internal IReadOnlyList<LocalizationKeyAuditLocalizedEntrySnapshot> Entries { get; }
    }
}
