using System;
using System.Collections.Generic;

namespace AssetImportAudit.Editor
{
    /// <summary>Immutable preview result that can be rechecked before applying changes.</summary>
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

        /// <summary>Folder used for the preview.</summary>
        public string RootFolder { get; }

        /// <summary>Shared settings requested by the preview, or the practical default when IncludesShared is false.</summary>
        public AssetImportAuditTextureSettings ExpectedSettings => IncludesShared ? ExpectedAuditSettings.SharedSettings : AssetImportAuditTextureSettings.Default;

        /// <summary>Settings requested by the preview, including platform overrides.</summary>
        public AssetImportAuditTextureAuditSettings ExpectedAuditSettings { get; }

        /// <summary>Whether the preview includes shared importer fields.</summary>
        public bool IncludesShared => ExpectedAuditSettings.IncludesShared;

        /// <summary>Whether the preview includes one platform override.</summary>
        public bool IncludesPlatform => ExpectedAuditSettings.IncludesPlatform;

        /// <summary>Platform included by the preview, or None for shared-only previews.</summary>
        public AssetImportAuditTexturePlatform Platform => ExpectedAuditSettings.Platform;

        /// <summary>Platform settings requested by the preview. Consume this value only when IncludesPlatform is true.</summary>
        public AssetImportAuditTexturePlatformSettings ExpectedPlatformSettings => ExpectedAuditSettings.PlatformSettings;

        /// <summary>Sorted mismatch list.</summary>
        public IReadOnlyList<AssetImportAuditIssue> Issues { get; }

        /// <summary>Whether the preview found no differences.</summary>
        public bool IsEmpty => Entries.Count == 0;

        internal IReadOnlyList<AssetImportAuditPlanEntry> Entries { get; }
    }

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

        public string AssetPath { get; }
        public string Snapshot { get; }

        public IReadOnlyList<AssetImportAuditTexturePlatform> Platforms { get; }

        public AssetImportAuditTexturePlatform Platform => Platforms.Count == 0 ? AssetImportAuditTexturePlatform.None : Platforms[0];
    }
}
