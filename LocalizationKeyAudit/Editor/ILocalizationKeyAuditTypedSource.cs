// SPDX-License-Identifier: MIT

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 未加工事前検査の通過後に限り、文字列監査スナップショットとアセットテーブルの所有境界を読み取る境界です。
    /// </summary>
    internal interface ILocalizationKeyAuditTypedSource
    {
        /// <summary>
        /// 公式のローカライズ機能から、型として読み取った完全なスナップショットを1回だけ取得します。
        /// </summary>
        LocalizationKeyAuditTypedSnapshot ReadSnapshot();
    }
}
