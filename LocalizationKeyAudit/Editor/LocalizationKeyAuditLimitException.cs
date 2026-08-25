// SPDX-License-Identifier: MIT

using System;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// typed adapter が snapshot 構築前に検出した入力上限超過です。
    /// </summary>
    internal sealed class LocalizationKeyAuditLimitException : Exception
    {
        /// <summary>上限超過の説明を保持します。</summary>
        internal LocalizationKeyAuditLimitException(string message)
            : base(message)
        {
        }

        /// <summary>数値演算例外を上限超過として保持します。</summary>
        internal LocalizationKeyAuditLimitException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
