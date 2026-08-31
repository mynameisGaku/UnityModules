using System;
using UnityEditor;
using UnityEngine;

namespace AssetImportAudit.Editor
{
    /// <summary>監査で比較・適用できるテクスチャー取り込み設定を保持します。</summary>
    public readonly struct AssetImportAuditTextureSettings : IEquatable<AssetImportAuditTextureSettings>
    {
        /// <summary>指定値からテクスチャー取込設定を作成します。寸法、圧縮方法、補間方法、異方性レベルが対応範囲外の場合は失敗します。</summary>
        /// <param name="maxTextureSize">取込後の最大テクスチャー寸法です。</param>
        /// <param name="compression">テクスチャーの圧縮方法です。</param>
        /// <param name="mipmapEnabled">ミップマップを生成するかどうか。</param>
        /// <param name="sRgbTexture">テクスチャーをsRGBとして取り込むかどうか。</param>
        /// <param name="readable">スクリプトから読み取れる状態にするかどうか。</param>
        /// <param name="filterMode">画素を拡大・縮小するときの補間方法です。</param>
        /// <param name="anisoLevel">異方性フィルタリングの強さです。</param>
        /// <exception cref="ArgumentOutOfRangeException">寸法、圧縮方法、補間方法、または異方性レベルが対応範囲外です。</exception>
        public AssetImportAuditTextureSettings(int maxTextureSize, TextureImporterCompression compression, bool mipmapEnabled, bool sRgbTexture, bool readable, FilterMode filterMode, int anisoLevel)
        {
            MaxTextureSize = maxTextureSize;
            Compression = compression;
            MipmapEnabled = mipmapEnabled;
            SRgbTexture = sRgbTexture;
            Readable = readable;
            FilterMode = filterMode;
            AnisoLevel = anisoLevel;
            Validate();
        }

        /// <summary>取り込み後の最大テクスチャー寸法。</summary>
        public int MaxTextureSize { get; }

        /// <summary>テクスチャー圧縮方式。</summary>
        public TextureImporterCompression Compression { get; }

        /// <summary>ミップマップを生成するかどうか。</summary>
        public bool MipmapEnabled { get; }

        /// <summary>テクスチャーを sRGB として取り込むかどうか。</summary>
        public bool SRgbTexture { get; }

        /// <summary>取り込んだテクスチャーをスクリプトから読み取れる状態にするかどうか。</summary>
        public bool Readable { get; }

        /// <summary>取り込んだテクスチャーのフィルター方式。</summary>
        public FilterMode FilterMode { get; }

        /// <summary>異方性フィルタリングの強さ。</summary>
        public int AnisoLevel { get; }

        /// <summary>一般的なカラーテクスチャー向けの実用的な既定値を返します。</summary>
        public static AssetImportAuditTextureSettings Default => new AssetImportAuditTextureSettings(2048, TextureImporterCompression.Compressed, false, true, false, FilterMode.Bilinear, 1);

        internal void Validate()
        {
            AssetImportAuditTextureSize.Validate(MaxTextureSize, nameof(MaxTextureSize));
            if (!Enum.IsDefined(typeof(TextureImporterCompression), Compression))
                throw new ArgumentOutOfRangeException(nameof(Compression), "圧縮方法に対応していない値が指定されています。");
            if (!Enum.IsDefined(typeof(FilterMode), FilterMode))
                throw new ArgumentOutOfRangeException(nameof(FilterMode), "画素の補間方法に対応していない値が指定されています。");
            if (AnisoLevel < 0 || AnisoLevel > 16)
                throw new ArgumentOutOfRangeException(nameof(AnisoLevel), "異方性レベルには0から16までの値を指定してください。");
        }

        /// <summary>すべての取り込み設定を比較します。</summary>
        /// <param name="other">比較する取込設定です。</param>
        /// <returns>すべての値が同じ場合は真、それ以外は偽です。</returns>
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
