// SPDX-License-Identifier: MIT

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 1 回の手動監査で受け付ける入力と生成物の上限です。
    /// </summary>
    internal static class LocalizationKeyAuditLimits
    {
        /// <summary>明示指定できる必須 Locale 数です。</summary>
        internal const int MaximumRequiredLocales = 256;

        /// <summary>static reference の宣言済み scope path 数です。</summary>
        internal const int MaximumDeclaredAssetPaths = 100000;

        /// <summary>認識済み static reference 数です。</summary>
        internal const int MaximumStaticReferences = 250000;

        /// <summary>static-reference scope の 1 physical asset 上限です。</summary>
        internal const int MaximumCoverageAssetBytes = 16 * 1024 * 1024;

        /// <summary>static-reference scopeの1 Unity YAML fileで解析するline数です。</summary>
        internal const int MaximumCoverageYamlLines = 1000000;

        /// <summary>static-reference scope で保持する総 bytes 上限です。</summary>
        internal const long MaximumCoverageTotalBytes = 512L * 1024L * 1024L;

        /// <summary>raw preflight で扱う SharedTableData asset 数です。</summary>
        internal const int MaximumSharedTableDataAssets = 4096;

        /// <summary>SharedTableData 1 asset の raw byte 数です。</summary>
        internal const int MaximumRawAssetBytes = 16 * 1024 * 1024;

        /// <summary>SharedTableData 全 asset の raw byte 総数です。</summary>
        internal const long MaximumTotalRawBytes = 256L * 1024L * 1024L;

        /// <summary>proof-complete physical discovery で列挙する file 数です。</summary>
        internal const int MaximumPhysicalAssetFiles = 1000000;

        /// <summary>proof-complete physical discovery で列挙するdirectory数です。</summary>
        internal const int MaximumPhysicalDirectories = 100000;

        /// <summary>proof-complete physical discovery で走査する byte 総数です。</summary>
        internal const long MaximumPhysicalDiscoveryBytes = 8L * 1024L * 1024L * 1024L;

        /// <summary>typed snapshot に含められる Locale 数です。</summary>
        internal const int MaximumLocales = 4096;

        /// <summary>typed snapshot に含められる collection 数です。</summary>
        internal const int MaximumCollections = 4096;

        /// <summary>typed snapshot に含められる locale table 総数です。</summary>
        internal const int MaximumLocaleTables = 65536;

        /// <summary>typed snapshot に含められる shared entry 総数です。</summary>
        internal const int MaximumSharedEntries = 1000000;

        /// <summary>typed snapshot に含められる localized entry 総数です。</summary>
        internal const int MaximumLocalizedEntries = 4000000;

        /// <summary>direct coverage と static reference から作る edge 総数です。</summary>
        internal const long MaximumGraphEdges = 5000000;

        /// <summary>完全な結果として返せる issue 数です。</summary>
        internal const int MaximumIssues = 100000;

        /// <summary>識別子、path、説明に許可する文字数です。</summary>
        internal const int MaximumTextCharacters = 32768;

        /// <summary>localized value 1 件に許可する文字数です。</summary>
        internal const int MaximumLocalizedValueCharacters = 1048576;
    }
}
