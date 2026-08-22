using System;
using UnityEditor;
using UnityEngine;

namespace AssetImportAudit.Editor
{
    /// <summary>Holds the Texture Import Settings values that an audit can compare and apply.</summary>
    public readonly struct AssetImportAuditTextureSettings : IEquatable<AssetImportAuditTextureSettings>
    {
        /// <summary>Creates a validated texture import settings value.</summary>
        public AssetImportAuditTextureSettings(int maxTextureSize, TextureImporterCompression compression, bool mipmapEnabled, bool sRgbTexture, bool readable, FilterMode filterMode, int anisoLevel)
        {
            if (maxTextureSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxTextureSize));
            if (anisoLevel < 0 || anisoLevel > 16)
                throw new ArgumentOutOfRangeException(nameof(anisoLevel));

            MaxTextureSize = maxTextureSize;
            Compression = compression;
            MipmapEnabled = mipmapEnabled;
            SRgbTexture = sRgbTexture;
            Readable = readable;
            FilterMode = filterMode;
            AnisoLevel = anisoLevel;
        }

        /// <summary>Maximum imported texture dimension.</summary>
        public int MaxTextureSize { get; }

        /// <summary>Texture compression mode.</summary>
        public TextureImporterCompression Compression { get; }

        /// <summary>Whether mipmaps are generated.</summary>
        public bool MipmapEnabled { get; }

        /// <summary>Whether the texture is imported as sRGB.</summary>
        public bool SRgbTexture { get; }

        /// <summary>Whether the imported texture remains CPU-readable.</summary>
        public bool Readable { get; }

        /// <summary>Imported texture filter mode.</summary>
        public FilterMode FilterMode { get; }

        /// <summary>Anisotropic filtering level.</summary>
        public int AnisoLevel { get; }

        /// <summary>Returns a practical default for general color textures.</summary>
        public static AssetImportAuditTextureSettings Default => new AssetImportAuditTextureSettings(2048, TextureImporterCompression.Compressed, false, true, false, FilterMode.Bilinear, 1);

        /// <summary>Compares all import settings.</summary>
        public bool Equals(AssetImportAuditTextureSettings other) => MaxTextureSize == other.MaxTextureSize && Compression == other.Compression && MipmapEnabled == other.MipmapEnabled && SRgbTexture == other.SRgbTexture && Readable == other.Readable && FilterMode == other.FilterMode && AnisoLevel == other.AnisoLevel;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is AssetImportAuditTextureSettings other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(MaxTextureSize, Compression, MipmapEnabled, SRgbTexture, Readable, FilterMode, AnisoLevel);

        /// <inheritdoc />
        public static bool operator ==(AssetImportAuditTextureSettings left, AssetImportAuditTextureSettings right) => left.Equals(right);

        /// <inheritdoc />
        public static bool operator !=(AssetImportAuditTextureSettings left, AssetImportAuditTextureSettings right) => !left.Equals(right);
    }
}
