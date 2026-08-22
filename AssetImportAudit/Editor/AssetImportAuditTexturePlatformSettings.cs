using System;
using UnityEditor;

namespace AssetImportAudit.Editor
{
    /// <summary>Stores the platform importer fields owned by the bounded audit rule.</summary>
    public readonly struct AssetImportAuditTexturePlatformSettings : IEquatable<AssetImportAuditTexturePlatformSettings>
    {
        /// <summary>Creates validated platform override values.</summary>
        public AssetImportAuditTexturePlatformSettings(bool overrideEnabled, int maxTextureSize, TextureImporterCompression compression)
        {
            OverrideEnabled = overrideEnabled;
            MaxTextureSize = maxTextureSize;
            Compression = compression;
            Validate();
        }

        /// <summary>Whether the platform importer override is enabled.</summary>
        public bool OverrideEnabled { get; }

        /// <summary>Maximum imported texture dimension for the platform.</summary>
        public int MaxTextureSize { get; }

        /// <summary>Texture compression mode for the platform.</summary>
        public TextureImporterCompression Compression { get; }

        internal void Validate()
        {
            AssetImportAuditTextureSize.Validate(MaxTextureSize, nameof(MaxTextureSize));
            if (!Enum.IsDefined(typeof(TextureImporterCompression), Compression))
                throw new ArgumentOutOfRangeException(nameof(Compression));
        }

        internal bool HasDifference(TextureImporterPlatformSettings current)
        {
            if (current.overridden != OverrideEnabled)
                return true;

            return OverrideEnabled && (current.maxTextureSize != MaxTextureSize || current.textureCompression != Compression);
        }

        internal TextureImporterPlatformSettings ApplyTo(TextureImporterPlatformSettings current)
        {
            current.overridden = OverrideEnabled;
            if (OverrideEnabled)
            {
                current.maxTextureSize = MaxTextureSize;
                current.textureCompression = Compression;
            }
            return current;
        }

        public bool Equals(AssetImportAuditTexturePlatformSettings other)
        {
            return OverrideEnabled == other.OverrideEnabled && MaxTextureSize == other.MaxTextureSize && Compression == other.Compression;
        }

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is AssetImportAuditTexturePlatformSettings other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(OverrideEnabled, MaxTextureSize, Compression);
    }
}
