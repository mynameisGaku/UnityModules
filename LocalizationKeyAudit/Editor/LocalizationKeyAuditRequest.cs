// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 手動監査で必須とする Locale と static reference coverage を明示します。
    /// </summary>
    internal sealed class LocalizationKeyAuditRequest
    {
        /// <summary>
        /// 必須 Locale と coverage を防御的に copy します。
        /// </summary>
        internal LocalizationKeyAuditRequest(
            IReadOnlyList<string> requiredLocaleIdentifiers,
            LocalizationKeyAuditCoverage coverage)
        {
            var locales = new string[requiredLocaleIdentifiers?.Count ?? 0];
            for (var index = 0; index < locales.Length; index++)
            {
                locales[index] = requiredLocaleIdentifiers[index] ?? string.Empty;
            }

            RequiredLocaleIdentifiers = new ReadOnlyCollection<string>(locales);
            Coverage = coverage?.Copy();
        }

        /// <summary>暗黙補完せず利用者が明示した必須 Locale identifiers です。</summary>
        internal IReadOnlyList<string> RequiredLocaleIdentifiers { get; }

        /// <summary>static reference の宣言済み scope と認識結果です。</summary>
        internal LocalizationKeyAuditCoverage Coverage { get; }
    }
}

