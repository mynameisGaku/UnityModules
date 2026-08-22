using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AssetImportAudit.Editor
{
    /// <summary>Previews and safely applies bounded Texture Import Settings changes under Assets.</summary>
    public static class AssetImportAuditService
    {
        /// <summary>Creates a deterministic preview for textures in an Assets folder.</summary>
        public static AssetImportAuditPlan Preview(string rootFolder, AssetImportAuditTextureSettings expectedSettings)
        {
            rootFolder = NormalizeRootFolder(rootFolder);
            var issues = new List<AssetImportAuditIssue>();
            var entries = new List<AssetImportAuditPlanEntry>();

            foreach (var assetPath in AssetDatabase.FindAssets("t:Texture2D", new[] { rootFolder }).Select(AssetDatabase.GUIDToAssetPath).Where(IsTextureAssetPath).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal))
            {
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                    continue;

                var currentSettings = Read(importer);
                var assetIssues = BuildIssues(assetPath, currentSettings, expectedSettings);
                if (assetIssues.Count == 0)
                    continue;

                issues.AddRange(assetIssues);
                entries.Add(new AssetImportAuditPlanEntry(assetPath, Serialize(currentSettings)));
            }

            return new AssetImportAuditPlan(rootFolder, expectedSettings, issues, entries);
        }

        /// <summary>Applies every asset in a preview after checking for stale importer values.</summary>
        public static AssetImportAuditApplyResult Apply(AssetImportAuditPlan plan)
        {
            return Apply(plan, null);
        }

        /// <summary>Applies selected assets in a preview after checking for stale importer values.</summary>
        public static AssetImportAuditApplyResult Apply(AssetImportAuditPlan plan, IEnumerable<string> selectedAssetPaths)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            var selected = selectedAssetPaths == null ? null : new HashSet<string>(selectedAssetPaths, StringComparer.Ordinal);
            var entries = plan.Entries.Where(entry => selected == null || selected.Contains(entry.AssetPath)).ToArray();
            if (entries.Length == 0)
                return new AssetImportAuditApplyResult(false, AssetImportAuditError.NoChanges, 0, Array.Empty<string>());

            var stale = new List<string>();
            var importers = new List<TextureImporter>(entries.Length);
            foreach (var entry in entries)
            {
                var importer = AssetImporter.GetAtPath(entry.AssetPath) as TextureImporter;
                if (importer == null)
                {
                    stale.Add(entry.AssetPath);
                    continue;
                }

                if (!StringComparer.Ordinal.Equals(Serialize(Read(importer)), entry.Snapshot))
                    stale.Add(entry.AssetPath);
                importers.Add(importer);
            }

            if (stale.Count > 0)
                return new AssetImportAuditApplyResult(false, AssetImportAuditError.StalePlan, 0, stale.OrderBy(path => path, StringComparer.Ordinal).ToArray());

            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply Asset Import Audit");
            try
            {
                for (var index = 0; index < entries.Length; index++)
                {
                    var importer = importers[index];
                    Undo.RecordObject(importer, "Apply Asset Import Audit");
                    Write(importer, plan.ExpectedSettings);
                    importer.SaveAndReimport();
                }

                Undo.CollapseUndoOperations(group);
                return new AssetImportAuditApplyResult(true, AssetImportAuditError.None, entries.Length, Array.Empty<string>());
            }
            catch (Exception)
            {
                return new AssetImportAuditApplyResult(false, AssetImportAuditError.ApplyFailed, 0, Array.Empty<string>());
            }
        }

        internal static string NormalizeRootFolder(string rootFolder)
        {
            rootFolder = (rootFolder ?? string.Empty).Trim().Replace('\\', '/').TrimEnd('/');
            if (rootFolder.Length == 0)
                rootFolder = "Assets";
            if (!rootFolder.Equals("Assets", StringComparison.Ordinal) && !rootFolder.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("Root folder must be under Assets.", nameof(rootFolder));
            if (!AssetDatabase.IsValidFolder(rootFolder))
                throw new ArgumentException("Root folder does not exist.", nameof(rootFolder));
            return rootFolder;
        }

        internal static AssetImportAuditTextureSettings Read(TextureImporter importer)
        {
            return new AssetImportAuditTextureSettings(importer.maxTextureSize, importer.textureCompression, importer.mipmapEnabled, importer.sRGBTexture, importer.isReadable, importer.filterMode, importer.anisoLevel);
        }

        private static bool IsTextureAssetPath(string path) => path.StartsWith("Assets/", StringComparison.Ordinal) && !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase);

        private static List<AssetImportAuditIssue> BuildIssues(string assetPath, AssetImportAuditTextureSettings current, AssetImportAuditTextureSettings expected)
        {
            var issues = new List<AssetImportAuditIssue>();
            AddIssue(issues, assetPath, "maxTextureSize", current.MaxTextureSize, expected.MaxTextureSize);
            AddIssue(issues, assetPath, "textureCompression", current.Compression, expected.Compression);
            AddIssue(issues, assetPath, "mipmapEnabled", current.MipmapEnabled, expected.MipmapEnabled);
            AddIssue(issues, assetPath, "sRGBTexture", current.SRgbTexture, expected.SRgbTexture);
            AddIssue(issues, assetPath, "isReadable", current.Readable, expected.Readable);
            AddIssue(issues, assetPath, "filterMode", current.FilterMode, expected.FilterMode);
            AddIssue(issues, assetPath, "anisoLevel", current.AnisoLevel, expected.AnisoLevel);
            return issues;
        }

        private static void AddIssue<T>(ICollection<AssetImportAuditIssue> issues, string assetPath, string settingName, T current, T expected)
        {
            if (!EqualityComparer<T>.Default.Equals(current, expected))
                issues.Add(new AssetImportAuditIssue(assetPath, settingName, current.ToString(), expected.ToString()));
        }

        private static string Serialize(AssetImportAuditTextureSettings settings)
        {
            return string.Join("|", settings.MaxTextureSize, (int)settings.Compression, settings.MipmapEnabled ? 1 : 0, settings.SRgbTexture ? 1 : 0, settings.Readable ? 1 : 0, (int)settings.FilterMode, settings.AnisoLevel);
        }

        private static void Write(TextureImporter importer, AssetImportAuditTextureSettings settings)
        {
            importer.maxTextureSize = settings.MaxTextureSize;
            importer.textureCompression = settings.Compression;
            importer.mipmapEnabled = settings.MipmapEnabled;
            importer.sRGBTexture = settings.SRgbTexture;
            importer.isReadable = settings.Readable;
            importer.filterMode = settings.FilterMode;
            importer.anisoLevel = settings.AnisoLevel;
        }
    }
}
