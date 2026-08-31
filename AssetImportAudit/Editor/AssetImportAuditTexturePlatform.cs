using System;
using UnityEditor;

namespace AssetImportAudit.Editor
{
    /// <summary>対応しているUnityの対象機種別テクスチャー設定を識別します。</summary>
    public enum AssetImportAuditTexturePlatform
    {
        /// <summary>共通設定だけを扱い、対象機種を指定しません。</summary>
        None = 0,

        /// <summary>パソコン向け設定を扱います。</summary>
        Standalone = 1,

        /// <summary>Android向け設定を扱います。</summary>
        Android = 2,

        /// <summary>iOS向け設定を扱います。</summary>
        iOS = 3
    }

    /// <summary>公開対象機種とUnity内部名の対応を扱います。</summary>
    internal static class AssetImportAuditTexturePlatformUtility
    {
        /// <summary>対象機種をUnity内部名へ変換し、未指定または未知の値では失敗します。</summary>
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
                    throw new ArgumentOutOfRangeException(nameof(platform), platform, "対象機種には、パソコン、Android、iOSのいずれかを指定してください。");
            }
        }

        /// <summary>指定した対象機種の取込設定を読み取り、未指定または未知の値では失敗します。</summary>
        internal static TextureImporterPlatformSettings Read(TextureImporter importer, AssetImportAuditTexturePlatform platform)
        {
            return importer.GetPlatformTextureSettings(ToUnityName(platform));
        }
    }
}
