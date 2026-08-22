using System;
using UnityEditor;

namespace AssetImportAudit.Editor
{
    /// <summary>Identifies a supported Unity texture platform preset.</summary>
    public enum AssetImportAuditTexturePlatform
    {
        None = 0,
        Standalone = 1,
        Android = 2,
        iOS = 3
    }

    internal static class AssetImportAuditTexturePlatformUtility
    {
        internal static string ToUnityName(AssetImportAuditTexturePlatform platform)
        {
            switch (platform)
            {
                case AssetImportAuditTexturePlatform.Standalone:
                    return "Standalone";
                case AssetImportAuditTexturePlatform.Android:
                    return "Android";
                case AssetImportAuditTexturePlatform.iOS:
                    return "iPhone";
                default:
                    throw new ArgumentOutOfRangeException(nameof(platform), platform, "A concrete texture platform is required.");
            }
        }

        internal static TextureImporterPlatformSettings Read(TextureImporter importer, AssetImportAuditTexturePlatform platform)
        {
            return importer.GetPlatformTextureSettings(ToUnityName(platform));
        }
    }
}
