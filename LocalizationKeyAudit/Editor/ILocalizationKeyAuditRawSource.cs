// SPDX-License-Identifier: MIT

using System.Collections.Generic;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// AssetDatabase path と physical bytes だけで SharedTableData を収集する境界です。
    /// </summary>
    internal interface ILocalizationKeyAuditRawSource
    {
        /// <summary>
        /// 全候補の収集を完了してから一覧を返し、途中結果を公開しません。
        /// </summary>
        IReadOnlyList<LocalizationKeyAuditRawAsset> ReadSharedTableDataAssets();
    }
}

