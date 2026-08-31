using System;
using UnityEditor;

namespace AssetImportAudit.Editor
{
    /// <summary>監査対象を限定する規則が扱う、対象機種別の取込設定項目を保持します。</summary>
    public readonly struct AssetImportAuditTexturePlatformSettings : IEquatable<AssetImportAuditTexturePlatformSettings>
    {
        /// <summary>指定値から対象機種別設定を作成します。寸法または圧縮方法が対応範囲外の場合は失敗します。</summary>
        /// <param name="overrideEnabled">対象機種別の個別設定を有効にするかどうか。</param>
        /// <param name="maxTextureSize">取込後の最大テクスチャー寸法です。</param>
        /// <param name="compression">対象機種別の圧縮方法です。</param>
        /// <exception cref="ArgumentOutOfRangeException">最大テクスチャー寸法または圧縮方法が対応範囲外です。</exception>
        public AssetImportAuditTexturePlatformSettings(bool overrideEnabled, int maxTextureSize, TextureImporterCompression compression)
        {
            OverrideEnabled = overrideEnabled;
            MaxTextureSize = maxTextureSize;
            Compression = compression;
            Validate();
        }

        /// <summary>対象機種別の個別設定を有効にするかどうか。</summary>
        public bool OverrideEnabled { get; }

        /// <summary>対象機種別に指定する、取込後の最大テクスチャー寸法。</summary>
        public int MaxTextureSize { get; }

        /// <summary>対象機種別のテクスチャー圧縮方式。</summary>
        public TextureImporterCompression Compression { get; }

        internal void Validate()
        {
            AssetImportAuditTextureSize.Validate(MaxTextureSize, nameof(MaxTextureSize));
            if (!Enum.IsDefined(typeof(TextureImporterCompression), Compression))
                throw new ArgumentOutOfRangeException(nameof(Compression), "圧縮方法に対応していない値が指定されています。");
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

        /// <summary>対象機種別の取込設定がすべて同じかどうかを調べます。</summary>
        /// <param name="other">比較する対象機種別設定です。</param>
        /// <returns>すべての値が同じ場合は真、それ以外は偽です。</returns>
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
