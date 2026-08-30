// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 手動監査で必須とするロケールと静的参照網羅を明示します。
    /// </summary>
    internal sealed class LocalizationKeyAuditRequest
    {
        /// <summary>
        /// 必須ロケールと静的参照網羅を防御的に複製します。
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

        /// <summary>暗黙補完せず利用者が明示した必須ロケール識別子です。</summary>
        internal IReadOnlyList<string> RequiredLocaleIdentifiers { get; }

        /// <summary>静的参照の宣言済み走査範囲と認識結果です。</summary>
        internal LocalizationKeyAuditCoverage Coverage { get; }
    }
}
