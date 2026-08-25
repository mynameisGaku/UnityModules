using System;
using System.Collections.Generic;
using AuditEditor = LocalizationKeyAudit.Editor;

namespace LocalizationKeyAudit.Tests
{
    /// <summary>
    /// project file へ触れず coverage asset の成功、null、例外を再現します。
    /// </summary>
    internal sealed class FakeLocalizationKeyAuditCoverageSource : AuditEditor.ILocalizationKeyAuditCoverageSource
    {
        /// <summary>成功時に返す coverage asset です。</summary>
        internal IReadOnlyList<AuditEditor.LocalizationKeyAuditCoverageAsset> Assets { get; set; } =
            Array.Empty<AuditEditor.LocalizationKeyAuditCoverageAsset>();

        /// <summary>指定時に coverage 読み取り境界から送出する例外です。</summary>
        internal Exception Exception { get; set; }

        /// <summary>全件収集の呼び出し回数です。</summary>
        internal int ReadCallCount { get; private set; }

        /// <summary>最後に source へ渡された宣言 scope path です。</summary>
        internal IReadOnlyList<string> LastDeclaredAssetPaths { get; private set; }

        /// <summary>設定済み asset を返すか、指定例外を送出します。</summary>
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
    /// physical file へ触れず raw source の成功、null、例外を再現します。
    /// </summary>
    internal sealed class FakeLocalizationKeyAuditRawSource : AuditEditor.ILocalizationKeyAuditRawSource
    {
        /// <summary>成功時に返す全 raw asset です。</summary>
        internal IReadOnlyList<AuditEditor.LocalizationKeyAuditRawAsset> Assets { get; set; } =
            Array.Empty<AuditEditor.LocalizationKeyAuditRawAsset>();

        /// <summary>指定時に読み取り境界から送出する例外です。</summary>
        internal Exception Exception { get; set; }

        /// <summary>全件収集の呼び出し回数です。</summary>
        internal int ReadCallCount { get; private set; }

        /// <summary>設定済み snapshot を返すか、指定例外を送出します。</summary>
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
    /// Localization API へ触れず typed snapshot の成功、null、例外を再現します。
    /// </summary>
    internal sealed class FakeLocalizationKeyAuditTypedSource : AuditEditor.ILocalizationKeyAuditTypedSource
    {
        /// <summary>成功時に返す typed snapshot です。</summary>
        internal AuditEditor.LocalizationKeyAuditTypedSnapshot Snapshot { get; set; }

        /// <summary>指定時に typed 読み取り境界から送出する例外です。</summary>
        internal Exception Exception { get; set; }

        /// <summary>typed snapshot の読み取り呼び出し回数です。</summary>
        internal int ReadCallCount { get; private set; }

        /// <summary>設定済み snapshot を返すか、指定例外を送出します。</summary>
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
