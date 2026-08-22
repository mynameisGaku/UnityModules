using NUnit.Framework;
using UnityEditor;

namespace AssetImportAudit.Tests
{
    public sealed class AssetImportAuditTexturePlatformSettingsTests
    {
        [Test]
        public void Constructor_StoresBoundedPlatformValues()
        {
            var settings = new AssetImportAudit.Editor.AssetImportAuditTexturePlatformSettings(true, 1024, TextureImporterCompression.CompressedHQ);

            Assert.That(settings.OverrideEnabled, Is.True);
            Assert.That(settings.MaxTextureSize, Is.EqualTo(1024));
            Assert.That(settings.Compression, Is.EqualTo(TextureImporterCompression.CompressedHQ));
        }

        [Test]
        public void Factories_ExposeRequestedScope()
        {
            var shared = AssetImportAudit.Editor.AssetImportAuditTextureSettings.Default;
            var platform = new AssetImportAudit.Editor.AssetImportAuditTexturePlatformSettings(false, 512, TextureImporterCompression.Uncompressed);
            var settings = AssetImportAudit.Editor.AssetImportAuditTextureAuditSettings.ForSharedAndPlatform(shared, AssetImportAudit.Editor.AssetImportAuditTexturePlatform.Android, platform);

            Assert.That(settings.IncludesShared, Is.True);
            Assert.That(settings.IncludesPlatform, Is.True);
            Assert.That(settings.Platform, Is.EqualTo(AssetImportAudit.Editor.AssetImportAuditTexturePlatform.Android));
            Assert.That(settings.SharedSettings, Is.EqualTo(shared));
            Assert.That(settings.PlatformSettings, Is.EqualTo(platform));
        }

        [Test]
        public void PlatformFactory_RejectsNone()
        {
            var platform = new AssetImportAudit.Editor.AssetImportAuditTexturePlatformSettings(true, 1024, TextureImporterCompression.Compressed);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => AssetImportAudit.Editor.AssetImportAuditTextureAuditSettings.ForPlatform(AssetImportAudit.Editor.AssetImportAuditTexturePlatform.None, platform));
        }

        [Test]
        public void Constructor_RejectsUndefinedCompression()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new AssetImportAudit.Editor.AssetImportAuditTexturePlatformSettings(true, 1024, (TextureImporterCompression)int.MaxValue));
        }

        [TestCase(3)]
        [TestCase(16)]
        [TestCase(32768)]
        public void Constructors_RejectUnsupportedMaxTextureSize(int maxTextureSize)
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new AssetImportAudit.Editor.AssetImportAuditTexturePlatformSettings(true, maxTextureSize, TextureImporterCompression.Compressed));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new AssetImportAudit.Editor.AssetImportAuditTextureSettings(maxTextureSize, TextureImporterCompression.Compressed, false, true, false, UnityEngine.FilterMode.Bilinear, 1));
        }

        [TestCase(32)]
        [TestCase(16384)]
        public void Constructors_AcceptBoundaryImporterPresets(int maxTextureSize)
        {
            Assert.DoesNotThrow(() => new AssetImportAudit.Editor.AssetImportAuditTexturePlatformSettings(true, maxTextureSize, TextureImporterCompression.Compressed));
            Assert.DoesNotThrow(() => new AssetImportAudit.Editor.AssetImportAuditTextureSettings(maxTextureSize, TextureImporterCompression.Compressed, false, true, false, UnityEngine.FilterMode.Bilinear, 1));
        }

        [Test]
        public void PlatformFactory_RejectsDefaultSettingsValue()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => AssetImportAudit.Editor.AssetImportAuditTextureAuditSettings.ForPlatform(AssetImportAudit.Editor.AssetImportAuditTexturePlatform.Android, default(AssetImportAudit.Editor.AssetImportAuditTexturePlatformSettings)));
        }

        [Test]
        public void SharedFactory_RejectsDefaultSettingsValue()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => AssetImportAudit.Editor.AssetImportAuditTextureAuditSettings.ForShared(default(AssetImportAudit.Editor.AssetImportAuditTextureSettings)));
        }

        [Test]
        public void PlatformOnlyPlan_ExposesSafeLegacySharedDefault()
        {
            var platform = new AssetImportAudit.Editor.AssetImportAuditTexturePlatformSettings(true, 1024, TextureImporterCompression.Compressed);
            var audit = AssetImportAudit.Editor.AssetImportAuditTextureAuditSettings.ForPlatform(AssetImportAudit.Editor.AssetImportAuditTexturePlatform.Android, platform);
            var plan = new AssetImportAudit.Editor.AssetImportAuditPlan("Assets", audit, System.Array.Empty<AssetImportAudit.Editor.AssetImportAuditIssue>(), System.Array.Empty<AssetImportAudit.Editor.AssetImportAuditPlanEntry>());

            Assert.That(plan.IncludesShared, Is.False);
            Assert.That(plan.ExpectedSettings, Is.EqualTo(AssetImportAudit.Editor.AssetImportAuditTextureSettings.Default));
            Assert.That(plan.ExpectedPlatformSettings, Is.EqualTo(platform));
        }
    }
}
