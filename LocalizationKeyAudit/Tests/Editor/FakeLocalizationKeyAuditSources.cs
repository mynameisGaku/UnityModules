using System;
using System.Collections.Generic;
using AuditEditor = LocalizationKeyAudit.Editor;

namespace LocalizationKeyAudit.Tests
{
    /// <summary>
    /// プロジェクトファイルへ触れず、網羅対象アセットの成功、参照なし、例外を再現します。
    /// </summary>
    internal sealed class FakeLocalizationKeyAuditCoverageSource : AuditEditor.ILocalizationKeyAuditCoverageSource
    {
        /// <summary>成功時に返す網羅対象アセットです。</summary>
        internal IReadOnlyList<AuditEditor.LocalizationKeyAuditCoverageAsset> Assets { get; set; } =
            Array.Empty<AuditEditor.LocalizationKeyAuditCoverageAsset>();

        /// <summary>指定時に網羅情報の読み取り境界から送出する例外です。</summary>
        internal Exception Exception { get; set; }

        /// <summary>全件収集の呼び出し回数です。</summary>
        internal int ReadCallCount { get; private set; }

        /// <summary>最後に取得元へ渡された宣言範囲のパスです。</summary>
        internal IReadOnlyList<string> LastDeclaredAssetPaths { get; private set; }

        /// <summary>設定済みアセットを返すか、指定例外を送出します。</summary>
        public IReadOnlyList<AuditEditor.LocalizationKeyAuditCoverageAsset> ReadAssets(
            IReadOnlyList<string> declaredAssetPaths)
        {
            ReadCallCount++;
            LastDeclaredAssetPaths = declaredAssetPaths;
            if (Exception != null)
            {
                throw Exception;
            }

            return Assets;
        }
    }

    /// <summary>
    /// 物理ファイルへ触れず、未加工データ取得元の成功、参照なし、例外を再現します。
    /// </summary>
    internal sealed class FakeLocalizationKeyAuditRawSource : AuditEditor.ILocalizationKeyAuditRawSource
    {
        /// <summary>成功時に返す全未加工アセットです。</summary>
        internal IReadOnlyList<AuditEditor.LocalizationKeyAuditRawAsset> Assets { get; set; } =
            Array.Empty<AuditEditor.LocalizationKeyAuditRawAsset>();

        /// <summary>指定時に読み取り境界から送出する例外です。</summary>
        internal Exception Exception { get; set; }

        /// <summary>全件収集の呼び出し回数です。</summary>
        internal int ReadCallCount { get; private set; }

        /// <summary>設定済みスナップショットを返すか、指定例外を送出します。</summary>
        public IReadOnlyList<AuditEditor.LocalizationKeyAuditRawAsset> ReadSharedTableDataAssets()
        {
            ReadCallCount++;
            if (Exception != null)
            {
                throw Exception;
            }

            return Assets;
        }
    }

    /// <summary>
    /// ローカライズAPIへ触れず、型として読み取ったスナップショットの成功、参照なし、例外を再現します。
    /// </summary>
    internal sealed class FakeLocalizationKeyAuditTypedSource : AuditEditor.ILocalizationKeyAuditTypedSource
    {
        /// <summary>成功時に返す、型として読み取ったスナップショットです。</summary>
        internal AuditEditor.LocalizationKeyAuditTypedSnapshot Snapshot { get; set; }

        /// <summary>指定時に型としての読み取り境界から送出する例外です。</summary>
        internal Exception Exception { get; set; }

        /// <summary>型として読み取ったスナップショットの取得回数です。</summary>
        internal int ReadCallCount { get; private set; }

        /// <summary>設定済みスナップショットを返すか、指定例外を送出します。</summary>
        public AuditEditor.LocalizationKeyAuditTypedSnapshot ReadSnapshot()
        {
            ReadCallCount++;
            if (Exception != null)
            {
                throw Exception;
            }

            return Snapshot;
        }
    }
}
