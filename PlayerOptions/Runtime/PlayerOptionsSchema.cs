// SPDX-License-Identifier: MIT

namespace PlayerOptions
{
    /// <summary>保存文書のversionと入力上限を一箇所で定義する。</summary>
    internal static class PlayerOptionsSchema
    {
        /// <summary>このruntimeが書き出すschema version。</summary>
        internal const int CurrentVersion = 1;

        /// <summary>不正または想定外に大きいPlayerPrefs値をparseしない上限。</summary>
        internal const int MaximumDocumentLength = 16 * 1024;
    }
}
