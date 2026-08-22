using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace AssetImportAudit.Editor
{
    /// <summary>Previews and safely applies bounded Texture Import Settings changes under Assets.</summary>
    public static class AssetImportAuditService
    {
        /// <summary>Creates a deterministic preview for shared texture importer fields.</summary>
        public static AssetImportAuditPlan Preview(string rootFolder, AssetImportAuditTextureSettings expectedSettings)
        {
            return Preview(rootFolder, AssetImportAuditTextureAuditSettings.ForShared(expectedSettings));
        }

        /// <summary>Creates a deterministic preview for shared or selected platform fields.</summary>
        public static AssetImportAuditPlan Preview(string rootFolder, AssetImportAuditTextureAuditSettings expectedSettings)
        {
            expectedSettings.Validate();

            rootFolder = NormalizeRootFolder(rootFolder);
            var issues = new List<AssetImportAuditIssue>();
            var entries = new List<AssetImportAuditPlanEntry>();
            foreach (var assetPath in AssetDatabase.FindAssets("t:Texture2D", new[] { rootFolder }).Select(AssetDatabase.GUIDToAssetPath).Where(IsTextureAssetPath).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal))
            {
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                    continue;

                var assetIssues = new List<AssetImportAuditIssue>();
                if (expectedSettings.IncludesShared)
                    assetIssues.AddRange(BuildSharedIssues(assetPath, Read(importer), expectedSettings.SharedSettings));
                if (expectedSettings.IncludesPlatform)
                    assetIssues.AddRange(BuildPlatformIssues(assetPath, expectedSettings.Platform, expectedSettings.PlatformSettings, AssetImportAuditTexturePlatformUtility.Read(importer, expectedSettings.Platform)));
                if (assetIssues.Count == 0)
                    continue;

                issues.AddRange(assetIssues);
                entries.Add(new AssetImportAuditPlanEntry(assetPath, Serialize(importer, expectedSettings), expectedSettings.IncludesPlatform ? new[] { expectedSettings.Platform } : new AssetImportAuditTexturePlatform[0]));
            }

            return new AssetImportAuditPlan(rootFolder, expectedSettings, issues, entries);
        }

        /// <summary>Applies every asset in a preview after checking for stale importer values.</summary>
        public static AssetImportAuditApplyResult Apply(AssetImportAuditPlan plan)
        {
            return Apply(plan, null);
        }

        /// <summary>Applies selected assets in a preview after checking all selected assets for stale values.</summary>
        public static AssetImportAuditApplyResult Apply(AssetImportAuditPlan plan, IEnumerable<string> selectedAssetPaths)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            var selected = selectedAssetPaths == null
                ? null
                : new HashSet<string>(selectedAssetPaths.Where(path => !string.IsNullOrWhiteSpace(path)).Select(NormalizeAssetPath), StringComparer.Ordinal);
            var entries = plan.Entries.Where(entry => selected == null || selected.Contains(entry.AssetPath)).ToArray();
            if (entries.Length == 0)
                return new AssetImportAuditApplyResult(false, AssetImportAuditError.NoChanges, 0, Array.Empty<string>());

            var stale = new List<string>();
            var importers = new List<TextureImporter>(entries.Length);
            foreach (var entry in entries)
            {
                var importer = AssetImporter.GetAtPath(entry.AssetPath) as TextureImporter;
                if (importer == null || !StringComparer.Ordinal.Equals(Serialize(importer, plan.ExpectedAuditSettings), entry.Snapshot))
                {
                    stale.Add(entry.AssetPath);
                    continue;
                }

                importers.Add(importer);
            }

            if (stale.Count > 0)
                return new AssetImportAuditApplyResult(false, AssetImportAuditError.StalePlan, 0, stale.OrderBy(path => path, StringComparer.Ordinal).ToArray());

            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply Asset Import Audit");
            var appliedAssetCount = 0;
            try
            {
                for (var index = 0; index < entries.Length; index++)
                {
                    var importer = importers[index];
                    Undo.RecordObject(importer, "Apply Asset Import Audit");
                    Write(importer, plan.ExpectedAuditSettings);
                    importer.SaveAndReimport();
                    appliedAssetCount++;
                }

                Undo.CollapseUndoOperations(group);
                return new AssetImportAuditApplyResult(true, AssetImportAuditError.None, appliedAssetCount, Array.Empty<string>());
            }
            catch (Exception)
            {
                return new AssetImportAuditApplyResult(false, AssetImportAuditError.ApplyFailed, appliedAssetCount, Array.Empty<string>());
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

        private static List<AssetImportAuditIssue> BuildSharedIssues(string assetPath, AssetImportAuditTextureSettings current, AssetImportAuditTextureSettings expected)
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

        private static List<AssetImportAuditIssue> BuildPlatformIssues(string assetPath, AssetImportAuditTexturePlatform platform, AssetImportAuditTexturePlatformSettings expected, TextureImporterPlatformSettings current)
        {
            var issues = new List<AssetImportAuditIssue>();
            AddIssue(issues, platform, assetPath, "overridden", current.overridden, expected.OverrideEnabled);
            if (expected.OverrideEnabled)
            {
                AddIssue(issues, platform, assetPath, "maxTextureSize", current.maxTextureSize, expected.MaxTextureSize);
                AddIssue(issues, platform, assetPath, "textureCompression", current.textureCompression, expected.Compression);
            }
            return issues;
        }

        private static void AddIssue<T>(ICollection<AssetImportAuditIssue> issues, string assetPath, string settingName, T current, T expected)
        {
            if (!EqualityComparer<T>.Default.Equals(current, expected))
                issues.Add(new AssetImportAuditIssue(assetPath, settingName, current.ToString(), expected.ToString()));
        }

        private static void AddIssue<T>(ICollection<AssetImportAuditIssue> issues, AssetImportAuditTexturePlatform platform, string assetPath, string settingName, T current, T expected)
        {
            if (!EqualityComparer<T>.Default.Equals(current, expected))
                issues.Add(new AssetImportAuditIssue(platform, assetPath, settingName, current.ToString(), expected.ToString()));
        }

        private static string Serialize(TextureImporter importer, AssetImportAuditTextureAuditSettings settings)
        {
            var values = new List<string>();
            if (settings.IncludesShared)
                values.Add("shared:" + Serialize(Read(importer)));
            if (settings.IncludesPlatform)
            {
                var current = AssetImportAuditTexturePlatformUtility.Read(importer, settings.Platform);
                values.Add(settings.PlatformSettings.OverrideEnabled && current.overridden
                    ? $"platform:{(int)settings.Platform}:{current.overridden}:{current.maxTextureSize}:{(int)current.textureCompression}"
                    : $"platform:{(int)settings.Platform}:{current.overridden}");
            }

            return string.Join("||", values);
        }

        private static string Serialize(AssetImportAuditTextureSettings settings)
        {
            return string.Join("|", settings.MaxTextureSize, (int)settings.Compression, settings.MipmapEnabled ? 1 : 0, settings.SRgbTexture ? 1 : 0, settings.Readable ? 1 : 0, (int)settings.FilterMode, settings.AnisoLevel);
        }

        private static void Write(TextureImporter importer, AssetImportAuditTextureAuditSettings settings)
        {
            if (settings.IncludesShared)
            {
                var shared = settings.SharedSettings;
                importer.maxTextureSize = shared.MaxTextureSize;
                importer.textureCompression = shared.Compression;
                importer.mipmapEnabled = shared.MipmapEnabled;
                importer.sRGBTexture = shared.SRgbTexture;
                importer.isReadable = shared.Readable;
                importer.filterMode = shared.FilterMode;
                importer.anisoLevel = shared.AnisoLevel;
            }

            if (settings.IncludesPlatform)
            {
                var platformSettings = AssetImportAuditTexturePlatformUtility.Read(importer, settings.Platform);
                platformSettings.name = AssetImportAuditTexturePlatformUtility.ToUnityName(settings.Platform);
                platformSettings = settings.PlatformSettings.ApplyTo(platformSettings);
                importer.SetPlatformTextureSettings(platformSettings);
            }
        }

        private static string NormalizeAssetPath(string assetPath)
        {
            return (assetPath ?? string.Empty).Trim().Replace('\\', '/').TrimEnd('/');
        }
    }
}
