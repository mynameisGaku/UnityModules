using System;
using System.Collections.Generic;

namespace AssetImportAudit.Editor
{
    /// <summary>反映前に再確認できる、変更不可の差分確認結果を表します。</summary>
    public sealed class AssetImportAuditPlan
    {
        internal AssetImportAuditPlan(string rootFolder, AssetImportAuditTextureSettings expectedSettings, IReadOnlyList<AssetImportAuditIssue> issues, IReadOnlyList<AssetImportAuditPlanEntry> entries)
            : this(rootFolder, AssetImportAuditTextureAuditSettings.ForShared(expectedSettings), issues, entries)
        {
        }

        internal AssetImportAuditPlan(string rootFolder, AssetImportAuditTextureAuditSettings expectedSettings, IReadOnlyList<AssetImportAuditIssue> issues, IReadOnlyList<AssetImportAuditPlanEntry> entries)
        {
            RootFolder = rootFolder;
            ExpectedAuditSettings = expectedSettings;
            Issues = new List<AssetImportAuditIssue>(issues).AsReadOnly();
            Entries = new List<AssetImportAuditPlanEntry>(entries).AsReadOnly();
        }

        /// <summary>差分確認対象のフォルダー。</summary>
        public string RootFolder { get; }

        /// <summary>差分確認で要求された共通設定。共通設定を含まない場合は実用上の既定値。</summary>
        public AssetImportAuditTextureSettings ExpectedSettings => IncludesShared ? ExpectedAuditSettings.SharedSettings : AssetImportAuditTextureSettings.Default;

        /// <summary>対象機種別設定を含む、差分確認で要求された設定。</summary>
        public AssetImportAuditTextureAuditSettings ExpectedAuditSettings { get; }

        /// <summary>共通の取込設定項目を差分確認対象に含むかどうか。</summary>
        public bool IncludesShared => ExpectedAuditSettings.IncludesShared;

        /// <summary>1つの対象機種別設定を差分確認対象に含むかどうか。</summary>
        public bool IncludesPlatform => ExpectedAuditSettings.IncludesPlatform;

        /// <summary>差分確認対象の機種。共通設定だけの場合は、対象機種なしを示す列挙値。</summary>
        public AssetImportAuditTexturePlatform Platform => ExpectedAuditSettings.Platform;

        /// <summary>差分確認で要求された対象機種別設定。対象機種別設定を含む場合だけ使用します。</summary>
        public AssetImportAuditTexturePlatformSettings ExpectedPlatformSettings => ExpectedAuditSettings.PlatformSettings;

        /// <summary>並び替え済みの不一致一覧。</summary>
        public IReadOnlyList<AssetImportAuditIssue> Issues { get; }

        /// <summary>差分確認で不一致が見つからなかったかどうか。</summary>
        public bool IsEmpty => Entries.Count == 0;

        /// <summary>反映前の再確認に使う対象別記録です。</summary>
        internal IReadOnlyList<AssetImportAuditPlanEntry> Entries { get; }
    }

    /// <summary>1件のアセットについて差分確認時点の状態を保持します。</summary>
    internal readonly struct AssetImportAuditPlanEntry
    {
        public AssetImportAuditPlanEntry(string assetPath, string snapshot)
            : this(assetPath, snapshot, null)
        {
        }

        internal AssetImportAuditPlanEntry(string assetPath, string snapshot, IReadOnlyList<AssetImportAuditTexturePlatform> platforms)
        {
            AssetPath = assetPath;
            Snapshot = snapshot;
            Platforms = platforms == null ? Array.AsReadOnly(Array.Empty<AssetImportAuditTexturePlatform>()) : new List<AssetImportAuditTexturePlatform>(platforms).AsReadOnly();
        }

        /// <summary>対象アセットのプロジェクト相対パスです。</summary>
        public string AssetPath { get; }

        /// <summary>管理対象値を一定形式で並べた差分確認時点の記録です。</summary>
        public string Snapshot { get; }

        /// <summary>記録へ含めた対象機種です。</summary>
        public IReadOnlyList<AssetImportAuditTexturePlatform> Platforms { get; }

        /// <summary>最初の対象機種です。対象機種を含まない場合は未指定値です。</summary>
        public AssetImportAuditTexturePlatform Platform => Platforms.Count == 0 ? AssetImportAuditTexturePlatform.None : Platforms[0];
    }
}
