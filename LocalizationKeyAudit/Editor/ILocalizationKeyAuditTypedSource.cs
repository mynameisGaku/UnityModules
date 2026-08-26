// SPDX-License-Identifier: MIT

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// raw preflight 通過後に限り String audit snapshot と Asset Table 所有境界を読み取る境界です。
    /// </summary>
    internal interface ILocalizationKeyAuditTypedSource
    {
        /// <summary>
        /// 公式 Localization API から完全な typed snapshot を 1 回だけ取得します。
        /// </summary>
        LocalizationKeyAuditTypedSnapshot ReadSnapshot();
    }
}
