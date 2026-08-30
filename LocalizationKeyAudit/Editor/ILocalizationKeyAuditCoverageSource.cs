// SPDX-License-Identifier: MIT

using System.Collections.Generic;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 宣言済みのAssetsまたは登録済みパッケージ範囲にある物理ファイルを解析器へ渡します。
    /// </summary>
    internal interface ILocalizationKeyAuditCoverageSource
    {
        /// <summary>全対象を収集し、途中結果を返さず変更不能なアセット一覧を返します。</summary>
        IReadOnlyList<LocalizationKeyAuditCoverageAsset> ReadAssets(IReadOnlyList<string> declaredAssetPaths);
    }
}
