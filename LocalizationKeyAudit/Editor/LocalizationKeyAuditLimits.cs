// SPDX-License-Identifier: MIT

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 1 回の手動監査で受け付ける入力と生成物の上限です。
    /// </summary>
    internal static class LocalizationKeyAuditLimits
    {
        /// <summary>明示指定できる必須ロケール数です。</summary>
        internal const int MaximumRequiredLocales = 256;

        /// <summary>静的参照の宣言済み走査範囲パス数です。</summary>
        internal const int MaximumDeclaredAssetPaths = 100000;

        /// <summary>認識済み静的参照数です。</summary>
        internal const int MaximumStaticReferences = 250000;

        /// <summary>静的参照走査範囲の物理アセット1件あたりの上限です。</summary>
        internal const int MaximumCoverageAssetBytes = 16 * 1024 * 1024;

        /// <summary>静的参照走査範囲のUnity形式のYAMLファイル1件で解析する行数です。</summary>
        internal const int MaximumCoverageYamlLines = 1000000;

        /// <summary>静的参照走査範囲で保持する総バイト数の上限です。</summary>
        internal const long MaximumCoverageTotalBytes = 512L * 1024L * 1024L;

        /// <summary>未加工事前検査で扱う共有テーブルデータのアセット数です。</summary>
        internal const int MaximumSharedTableDataAssets = 4096;

        /// <summary>共有テーブルデータのアセット1件あたりの未加工バイト数です。</summary>
        internal const int MaximumRawAssetBytes = 16 * 1024 * 1024;

        /// <summary>全共有テーブルデータアセットの未加工バイト総数です。</summary>
        internal const long MaximumTotalRawBytes = 256L * 1024L * 1024L;

        /// <summary>完全性を証明する物理探索で列挙するファイル数です。</summary>
        internal const int MaximumPhysicalAssetFiles = 1000000;

        /// <summary>完全性を証明する物理探索で列挙するディレクトリ数です。</summary>
        internal const int MaximumPhysicalDirectories = 100000;

        /// <summary>完全性を証明する物理探索で走査する総バイト数です。</summary>
        internal const long MaximumPhysicalDiscoveryBytes = 8L * 1024L * 1024L * 1024L;

        /// <summary>型として読み取ったスナップショットに含められるロケール数です。</summary>
        internal const int MaximumLocales = 4096;

        /// <summary>型として読み取ったスナップショットに含められるコレクション数です。</summary>
        internal const int MaximumCollections = 4096;

        /// <summary>型として読み取ったスナップショットに含められるロケールテーブル総数です。</summary>
        internal const int MaximumLocaleTables = 65536;

        /// <summary>型として読み取ったスナップショットに含められる共有項目総数です。</summary>
        internal const int MaximumSharedEntries = 1000000;

        /// <summary>型として読み取ったスナップショットに含められるローカライズ済み項目総数です。</summary>
        internal const int MaximumLocalizedEntries = 4000000;

        /// <summary>直接網羅と静的参照から作る参照関係総数です。</summary>
        internal const long MaximumGraphEdges = 5000000;

        /// <summary>完全な結果として返せる問題数です。</summary>
        internal const int MaximumIssues = 100000;

        /// <summary>識別子、パス、説明に許可する文字数です。</summary>
        internal const int MaximumTextCharacters = 32768;

        /// <summary>ローカライズ済み値1件に許可する文字数です。</summary>
        internal const int MaximumLocalizedValueCharacters = 1048576;
    }
}
