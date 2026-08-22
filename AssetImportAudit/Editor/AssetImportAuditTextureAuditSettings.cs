using System;

namespace AssetImportAudit.Editor
{
    /// <summary>Defines whether an audit covers shared importer values, one platform, or both.</summary>
    public readonly struct AssetImportAuditTextureAuditSettings : IEquatable<AssetImportAuditTextureAuditSettings>
    {
        private AssetImportAuditTextureAuditSettings(bool includesShared, AssetImportAuditTextureSettings sharedSettings, bool includesPlatform, AssetImportAuditTexturePlatform platform, AssetImportAuditTexturePlatformSettings platformSettings)
        {
            if (includesPlatform && (!Enum.IsDefined(typeof(AssetImportAuditTexturePlatform), platform) || platform == AssetImportAuditTexturePlatform.None))
                throw new ArgumentOutOfRangeException(nameof(platform));

            IncludesShared = includesShared;
            SharedSettings = sharedSettings;
            IncludesPlatform = includesPlatform;
            Platform = platform;
            PlatformSettings = platformSettings;
            Validate();
        }

        /// <summary>Whether shared importer fields are included.</summary>
        public bool IncludesShared { get; }

        /// <summary>Shared importer values used when IncludesShared is true.</summary>
        public AssetImportAuditTextureSettings SharedSettings { get; }

        /// <summary>Whether one platform override is included.</summary>
        public bool IncludesPlatform { get; }

        /// <summary>Platform used when IncludesPlatform is true.</summary>
        public AssetImportAuditTexturePlatform Platform { get; }

        /// <summary>Platform values used when IncludesPlatform is true.</summary>
        public AssetImportAuditTexturePlatformSettings PlatformSettings { get; }

        /// <summary>Creates an audit covering shared importer fields only.</summary>
        public static AssetImportAuditTextureAuditSettings ForShared(AssetImportAuditTextureSettings sharedSettings)
        {
            return new AssetImportAuditTextureAuditSettings(true, sharedSettings, false, AssetImportAuditTexturePlatform.None, default);
        }

        /// <summary>Creates an audit covering one platform override only.</summary>
        public static AssetImportAuditTextureAuditSettings ForPlatform(AssetImportAuditTexturePlatform platform, AssetImportAuditTexturePlatformSettings platformSettings)
        {
            return new AssetImportAuditTextureAuditSettings(false, default, true, platform, platformSettings);
        }

        /// <summary>Creates an audit covering shared fields and one platform override.</summary>
        public static AssetImportAuditTextureAuditSettings ForSharedAndPlatform(AssetImportAuditTextureSettings sharedSettings, AssetImportAuditTexturePlatform platform, AssetImportAuditTexturePlatformSettings platformSettings)
        {
            return new AssetImportAuditTextureAuditSettings(true, sharedSettings, true, platform, platformSettings);
        }

        /// <summary>Creates a shared-settings audit using the practical default.</summary>
        public static AssetImportAuditTextureAuditSettings Default => ForShared(AssetImportAuditTextureSettings.Default);

        internal void Validate()
        {
            if (!IncludesShared && !IncludesPlatform)
                throw new ArgumentException("At least one audit scope is required.");
            if (IncludesShared)
                SharedSettings.Validate();
            if (IncludesPlatform)
            {
                if (!Enum.IsDefined(typeof(AssetImportAuditTexturePlatform), Platform) || Platform == AssetImportAuditTexturePlatform.None)
                    throw new ArgumentOutOfRangeException(nameof(Platform));
                PlatformSettings.Validate();
            }
        }

        public bool Equals(AssetImportAuditTextureAuditSettings other)
        {
            return IncludesShared == other.IncludesShared && SharedSettings.Equals(other.SharedSettings) && IncludesPlatform == other.IncludesPlatform && Platform == other.Platform && PlatformSettings.Equals(other.PlatformSettings);
        }

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is AssetImportAuditTextureAuditSettings other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(IncludesShared, SharedSettings, IncludesPlatform, Platform, PlatformSettings);
    }
}
