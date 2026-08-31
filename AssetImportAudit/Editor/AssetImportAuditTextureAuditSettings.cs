using System;

namespace AssetImportAudit.Editor
{
    /// <summary>監査対象が共通の取り込み設定、1つのプラットフォーム、またはその両方かを定義します。</summary>
    public readonly struct AssetImportAuditTextureAuditSettings : IEquatable<AssetImportAuditTextureAuditSettings>
    {
        private AssetImportAuditTextureAuditSettings(bool includesShared, AssetImportAuditTextureSettings sharedSettings, bool includesPlatform, AssetImportAuditTexturePlatform platform, AssetImportAuditTexturePlatformSettings platformSettings)
        {
            if (includesPlatform && (!Enum.IsDefined(typeof(AssetImportAuditTexturePlatform), platform) || platform == AssetImportAuditTexturePlatform.None))
                throw new ArgumentOutOfRangeException(nameof(platform), "対象機種には、パソコン、Android、iOSのいずれかを指定してください。");

            IncludesShared = includesShared;
            SharedSettings = sharedSettings;
            IncludesPlatform = includesPlatform;
            Platform = platform;
            PlatformSettings = platformSettings;
            Validate();
        }

        /// <summary>共通の取り込み設定項目を監査対象に含むかどうか。</summary>
        public bool IncludesShared { get; }

        /// <summary>共通設定を含む場合に使用する取り込み設定値。</summary>
        public AssetImportAuditTextureSettings SharedSettings { get; }

        /// <summary>1つのプラットフォーム別上書きを監査対象に含むかどうか。</summary>
        public bool IncludesPlatform { get; }

        /// <summary>プラットフォーム別設定を含む場合に使用するプラットフォーム。</summary>
        public AssetImportAuditTexturePlatform Platform { get; }

        /// <summary>プラットフォーム別設定を含む場合に使用する設定値。</summary>
        public AssetImportAuditTexturePlatformSettings PlatformSettings { get; }

        /// <summary>共通の取り込み設定だけを対象とする監査設定を作成します。</summary>
        /// <param name="sharedSettings">共通設定として要求する取込設定です。</param>
        /// <returns>共通設定だけを検査する監査設定です。</returns>
        /// <exception cref="ArgumentOutOfRangeException">共通設定に対応範囲外の値があります。</exception>
        public static AssetImportAuditTextureAuditSettings ForShared(AssetImportAuditTextureSettings sharedSettings)
        {
            return new AssetImportAuditTextureAuditSettings(true, sharedSettings, false, AssetImportAuditTexturePlatform.None, default);
        }

        /// <summary>1つのプラットフォーム別上書きだけを対象とする監査設定を作成します。</summary>
        /// <param name="platform">検査する対象機種です。</param>
        /// <param name="platformSettings">対象機種別設定として要求する値です。</param>
        /// <returns>指定した対象機種別設定だけを検査する監査設定です。</returns>
        /// <exception cref="ArgumentOutOfRangeException">対象機種または対象機種別設定に対応範囲外の値があります。</exception>
        public static AssetImportAuditTextureAuditSettings ForPlatform(AssetImportAuditTexturePlatform platform, AssetImportAuditTexturePlatformSettings platformSettings)
        {
            return new AssetImportAuditTextureAuditSettings(false, default, true, platform, platformSettings);
        }

        /// <summary>共通設定と1つのプラットフォーム別上書きを対象とする監査設定を作成します。</summary>
        /// <param name="sharedSettings">共通設定として要求する取込設定です。</param>
        /// <param name="platform">検査する対象機種です。</param>
        /// <param name="platformSettings">対象機種別設定として要求する値です。</param>
        /// <returns>共通設定と指定した対象機種別設定を検査する監査設定です。</returns>
        /// <exception cref="ArgumentOutOfRangeException">共通設定、対象機種、または対象機種別設定に対応範囲外の値があります。</exception>
        public static AssetImportAuditTextureAuditSettings ForSharedAndPlatform(AssetImportAuditTextureSettings sharedSettings, AssetImportAuditTexturePlatform platform, AssetImportAuditTexturePlatformSettings platformSettings)
        {
            return new AssetImportAuditTextureAuditSettings(true, sharedSettings, true, platform, platformSettings);
        }

        /// <summary>実用上の既定値で共通設定を監査する設定を作成します。</summary>
        public static AssetImportAuditTextureAuditSettings Default => ForShared(AssetImportAuditTextureSettings.Default);

        internal void Validate()
        {
            if (!IncludesShared && !IncludesPlatform)
                throw new ArgumentException("監査対象を1つ以上指定する必要があります。");
            if (IncludesShared)
                SharedSettings.Validate();
            if (IncludesPlatform)
            {
                if (!Enum.IsDefined(typeof(AssetImportAuditTexturePlatform), Platform) || Platform == AssetImportAuditTexturePlatform.None)
                    throw new ArgumentOutOfRangeException(nameof(Platform), "対象機種には、パソコン、Android、iOSのいずれかを指定してください。");
                PlatformSettings.Validate();
            }
        }

        /// <summary>監査範囲と要求する取込設定がすべて同じかどうかを調べます。</summary>
        /// <param name="other">比較する監査設定です。</param>
        /// <returns>すべての値が同じ場合は真、それ以外は偽です。</returns>
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
