// SPDX-License-Identifier: MIT

using System.Collections.Generic;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 宣言済み Assets scope の physical files を static-reference parser へ渡します。
    /// </summary>
    internal interface ILocalizationKeyAuditCoverageSource
    {
        /// <summary>全対象を収集し、途中結果を返さず immutable asset 一覧を返します。</summary>
        IReadOnlyList<LocalizationKeyAuditCoverageAsset> ReadAssets(IReadOnlyList<string> declaredAssetPaths);
    }
}
