using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AssetImportAudit.Editor.Tests
{
    public sealed class AssetImportAuditTextureSettingsTests
    {
        [Test]
        public void Default_IsDeterministicAndEqualToCopy()
        {
            var settings = AssetImportAudit.Editor.AssetImportAuditTextureSettings.Default;
            Assert.That(settings.MaxTextureSize, Is.EqualTo(2048));
            Assert.That(settings.Compression, Is.EqualTo(TextureImporterCompression.Compressed));
            Assert.That(settings.SRgbTexture, Is.True);
            Assert.That(settings, Is.EqualTo(settings));
            Assert.That(settings.GetHashCode(), Is.EqualTo(settings.GetHashCode()));
        }

        [Test]
        public void InvalidAnisoLevel_IsRejected()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new AssetImportAudit.Editor.AssetImportAuditTextureSettings(2048, TextureImporterCompression.Compressed, false, true, false, FilterMode.Bilinear, 17));
        }
    }
}
