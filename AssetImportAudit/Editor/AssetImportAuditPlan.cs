using System.Collections.Generic;

namespace AssetImportAudit.Editor
{
    /// <summary>Immutable preview result that can be rechecked before applying changes.</summary>
    public sealed class AssetImportAuditPlan
    {
        internal AssetImportAuditPlan(string rootFolder, AssetImportAuditTextureSettings expectedSettings, IReadOnlyList<AssetImportAuditIssue> issues, IReadOnlyList<AssetImportAuditPlanEntry> entries)
        {
            RootFolder = rootFolder;
            ExpectedSettings = expectedSettings;
            Issues = issues;
            Entries = entries;
        }

        /// <summary>Folder used for the preview.</summary>
        public string RootFolder { get; }

        /// <summary>Settings requested by the preview.</summary>
        public AssetImportAuditTextureSettings ExpectedSettings { get; }

        /// <summary>Sorted mismatch list.</summary>
        public IReadOnlyList<AssetImportAuditIssue> Issues { get; }

        /// <summary>Whether the preview found no differences.</summary>
        public bool IsEmpty => Entries.Count == 0;

        internal IReadOnlyList<AssetImportAuditPlanEntry> Entries { get; }
    }

    internal readonly struct AssetImportAuditPlanEntry
    {
        public AssetImportAuditPlanEntry(string assetPath, string snapshot)
        {
            AssetPath = assetPath;
            Snapshot = snapshot;
        }

        public string AssetPath { get; }
        public string Snapshot { get; }
    }
}
