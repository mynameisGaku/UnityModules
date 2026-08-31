using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AssetImportAudit.Editor.Tests
{
    public sealed class AssetImportAuditInputExceptionTests
    {
        [Test]
        public void InvalidTextureSettings_KeepStandardMetadataAndStartWithJapaneseGuidance()
        {
            AssertLocalized<ArgumentOutOfRangeException>(
                () => new AssetImportAuditTextureSettings(2048, (TextureImporterCompression)999, false, true, false, FilterMode.Bilinear, 1),
                "Compression",
                "圧縮方法に対応していない値が指定されています。");
            AssertLocalized<ArgumentOutOfRangeException>(
                () => new AssetImportAuditTextureSettings(2048, TextureImporterCompression.Compressed, false, true, false, (FilterMode)999, 1),
                "FilterMode",
                "画素の補間方法に対応していない値が指定されています。");
            AssertLocalized<ArgumentOutOfRangeException>(
                () => new AssetImportAuditTextureSettings(2048, TextureImporterCompression.Compressed, false, true, false, FilterMode.Bilinear, 17),
                "AnisoLevel",
                "異方性レベルには0から16までの値を指定してください。");
            var sizeException = AssertLocalized<ArgumentOutOfRangeException>(
                () => new AssetImportAuditTextureSettings(16, TextureImporterCompression.Compressed, false, true, false, FilterMode.Bilinear, 1),
                "MaxTextureSize",
                "最大テクスチャー寸法には、32、64、128、256、512、1024、2048、4096、8192、16384のいずれかを指定してください。");

            Assert.That(sizeException.ActualValue, Is.EqualTo(16));
        }

        [Test]
        public void InvalidPlatformSettings_KeepStandardMetadataAndStartWithJapaneseGuidance()
        {
            AssertLocalized<ArgumentOutOfRangeException>(
                () => new AssetImportAuditTexturePlatformSettings(true, 1024, (TextureImporterCompression)999),
                "Compression",
                "圧縮方法に対応していない値が指定されています。");
            AssertLocalized<ArgumentOutOfRangeException>(
                () => AssetImportAuditTextureAuditSettings.ForPlatform(AssetImportAuditTexturePlatform.None, new AssetImportAuditTexturePlatformSettings(true, 1024, TextureImporterCompression.Compressed)),
                "platform",
                "対象機種には、パソコン、Android、iOSのいずれかを指定してください。");
            var platformException = AssertLocalized<ArgumentOutOfRangeException>(
                () => AssetImportAuditTexturePlatformUtility.ToUnityName((AssetImportAuditTexturePlatform)999),
                "platform",
                "対象機種には、パソコン、Android、iOSのいずれかを指定してください。");

            Assert.That(platformException.ActualValue, Is.EqualTo((AssetImportAuditTexturePlatform)999));
        }

        [Test]
        public void InvalidServiceInputs_KeepStandardMetadataAndStartWithJapaneseGuidance()
        {
            AssertLocalized<ArgumentNullException>(
                () => AssetImportAuditService.Apply(null),
                "plan",
                "差分確認計画を指定してください。");
            AssertLocalized<ArgumentException>(
                () => AssetImportAuditService.NormalizeRootFolder("Packages"),
                "rootFolder",
                "対象フォルダーはAssets以下を指定してください。");
            AssertLocalized<ArgumentNullException>(
                () => AssetImportAuditWindow.TryFormatInputError(null, out _),
                "exception",
                "入力不備の例外を指定してください。");
        }

        private static TException AssertLocalized<TException>(TestDelegate action, string expectedParameterName, string expectedMessageStart)
            where TException : ArgumentException
        {
            var exception = Assert.Throws<TException>(action);

            Assert.That(exception.ParamName, Is.EqualTo(expectedParameterName));
            Assert.That(exception.Message, Does.StartWith(expectedMessageStart));
            return exception;
        }
    }
}
