// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 1 Locale の StringTable asset と direct entry 一覧を保持します。
    /// </summary>
    internal sealed class LocalizationKeyAuditLocaleTableSnapshot
    {
        /// <summary>table と entry 一覧を防御的に copy します。</summary>
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

        /// <summary>StringTable の Locale identifier です。</summary>
        internal string LocaleIdentifier { get; }

        /// <summary>StringTable asset path です。</summary>
        internal string AssetPath { get; }

        /// <summary>fallback を適用していない direct entry 一覧です。</summary>
        internal IReadOnlyList<LocalizationKeyAuditLocalizedEntrySnapshot> Entries { get; }
    }
}

