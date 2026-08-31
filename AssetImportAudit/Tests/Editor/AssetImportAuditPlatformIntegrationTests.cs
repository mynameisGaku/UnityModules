using System;
using System.IO;
using System.Linq;
using AssetImportAudit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AssetImportAudit.Editor.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public sealed class AssetImportAuditPlatformIntegrationTests
    {
        private const string FolderPath = "Assets/AssetImportAuditPlatformTests";
        private const string FirstAssetPath = FolderPath + "/First.png";
        private const string SecondAssetPath = FolderPath + "/Second.png";
        private const string PngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(FolderPath);
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "AssetImportAuditPlatformTests"));
            var bytes = Convert.FromBase64String(PngBase64);
            File.WriteAllBytes(ToAbsolutePath(FirstAssetPath), bytes);
            File.WriteAllBytes(ToAbsolutePath(SecondAssetPath), bytes);
            AssetDatabase.ImportAsset(FirstAssetPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(SecondAssetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(FolderPath);
            AssetDatabase.Refresh();
        }

        [Test]
        public void Preview_MapsIosToIPhoneAndReportsPlatformMetadata()
        {
            var importer = GetImporter(FirstAssetPath);
            SetPlatformOverride(importer, "iPhone", false, 512);
            var expected = new AssetImportAuditTexturePlatformSettings(true, 2048, importer.GetPlatformTextureSettings("iPhone").textureCompression);
            var plan = AssetImportAuditService.Preview(FolderPath, AssetImportAuditTextureAuditSettings.ForPlatform(AssetImportAuditTexturePlatform.iOS, expected));

            Assert.That(plan.Issues.Any(issue => issue.AssetPath == FirstAssetPath && issue.Platform == AssetImportAuditTexturePlatform.iOS && issue.SettingName == "overridden"), Is.True);
            Assert.That(plan.Entries[0].Platforms, Is.EqualTo(new[] { AssetImportAuditTexturePlatform.iOS }));
        }

        [Test]
        public void Apply_IosMapsToIPhoneAndLeavesAndroidUnchanged()
        {
            var importer = GetImporter(FirstAssetPath);
            SetPlatformOverride(importer, "iPhone", false, 512);
            SetPlatformOverride(importer, "Android", false, 768);
            var androidBefore = GetPlatformSettings(FirstAssetPath, "Android");
            var iphoneCompression = GetPlatformSettings(FirstAssetPath, "iPhone").textureCompression;
            var expected = new AssetImportAuditTexturePlatformSettings(true, 2048, iphoneCompression);
            var plan = AssetImportAuditService.Preview(FolderPath, AssetImportAuditTextureAuditSettings.ForPlatform(AssetImportAuditTexturePlatform.iOS, expected));

            var result = AssetImportAuditService.Apply(plan, new[] { FirstAssetPath });
            var iphoneAfter = GetPlatformSettings(FirstAssetPath, "iPhone");
            var androidAfter = GetPlatformSettings(FirstAssetPath, "Android");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(iphoneAfter.overridden, Is.True);
            Assert.That(iphoneAfter.maxTextureSize, Is.EqualTo(2048));
            AssertPlatformValuesEqual(androidBefore, androidAfter);
        }

        [Test]
        public void Preview_RejectsEmptyAuditScope()
        {
            Assert.Throws<ArgumentException>(() => AssetImportAuditService.Preview(FolderPath, default(AssetImportAuditTextureAuditSettings)));
        }

        [Test]
        public void Apply_SelectedAndAll_ChangesOnlyRequestedPlatformOverrides()
        {
            var firstImporter = GetImporter(FirstAssetPath);
            var secondImporter = GetImporter(SecondAssetPath);
            SetPlatformOverride(firstImporter, "Android", false, 512);
            SetPlatformOverride(secondImporter, "Android", false, 512);
            var expected = CreateAndroidAuditSettings(firstImporter, true, 2048);
            var plan = AssetImportAuditService.Preview(FolderPath, expected);

            var selectedResult = AssetImportAuditService.Apply(plan, new[] { FirstAssetPath });
            Assert.That(selectedResult.Succeeded, Is.True);
            Assert.That(GetPlatformSettings(FirstAssetPath, "Android").overridden, Is.True);
            Assert.That(GetPlatformSettings(FirstAssetPath, "Android").maxTextureSize, Is.EqualTo(2048));
            Assert.That(GetPlatformSettings(SecondAssetPath, "Android").overridden, Is.False);

            plan = AssetImportAuditService.Preview(FolderPath, expected);
            var allResult = AssetImportAuditService.Apply(plan);
            Assert.That(allResult.Succeeded, Is.True);
            Assert.That(GetPlatformSettings(SecondAssetPath, "Android").overridden, Is.True);
            Assert.That(GetPlatformSettings(SecondAssetPath, "Android").maxTextureSize, Is.EqualTo(2048));
        }

        [Test]
        public void Apply_StaleUnselectedEntryDoesNotBlockSelectedEntry()
        {
            var firstImporter = GetImporter(FirstAssetPath);
            var secondImporter = GetImporter(SecondAssetPath);
            SetPlatformOverride(firstImporter, "Android", false, 512);
            SetPlatformOverride(secondImporter, "Android", false, 512);
            var expected = CreateAndroidAuditSettings(firstImporter, true, 2048);
            var plan = AssetImportAuditService.Preview(FolderPath, expected);
            SetPlatformOverride(secondImporter, "Android", true, 1024);

            var result = AssetImportAuditService.Apply(plan, new[] { FirstAssetPath });

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.StaleAssetPaths, Is.Empty);
            Assert.That(GetPlatformSettings(FirstAssetPath, "Android").overridden, Is.True);
            Assert.That(GetPlatformSettings(SecondAssetPath, "Android").maxTextureSize, Is.EqualTo(1024));
        }

        [Test]
        public void Apply_StaleSelectedEntriesRejectsAllSelectedEntries()
        {
            var firstImporter = GetImporter(FirstAssetPath);
            var secondImporter = GetImporter(SecondAssetPath);
            SetPlatformOverride(firstImporter, "Android", false, 512);
            SetPlatformOverride(secondImporter, "Android", false, 512);
            var expected = CreateAndroidAuditSettings(firstImporter, true, 2048);
            var plan = AssetImportAuditService.Preview(FolderPath, expected);
            SetPlatformOverride(firstImporter, "Android", true, 1024);
            SetPlatformOverride(secondImporter, "Android", true, 1024);

            var result = AssetImportAuditService.Apply(plan, new[] { FirstAssetPath, SecondAssetPath });

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(AssetImportAuditError.StalePlan));
            Assert.That(result.AppliedAssetCount, Is.EqualTo(0));
            Assert.That(result.StaleAssetPaths, Is.EqualTo(new[] { FirstAssetPath, SecondAssetPath }));
            Assert.That(GetPlatformSettings(FirstAssetPath, "Android").maxTextureSize, Is.EqualTo(1024));
            Assert.That(GetPlatformSettings(SecondAssetPath, "Android").maxTextureSize, Is.EqualTo(1024));
        }

        [Test]
        public void Apply_FreshAndStaleSelectedEntriesRejectsBothWithoutWrites()
        {
            var firstImporter = GetImporter(FirstAssetPath);
            var secondImporter = GetImporter(SecondAssetPath);
            SetPlatformOverride(firstImporter, "Android", false, 512);
            SetPlatformOverride(secondImporter, "Android", false, 512);
            var expected = CreateAndroidAuditSettings(firstImporter, true, 2048);
            var plan = AssetImportAuditService.Preview(FolderPath, expected);
            SetPlatformOverride(secondImporter, "Android", true, 1024);

            var result = AssetImportAuditService.Apply(plan, new[] { FirstAssetPath, SecondAssetPath });

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(AssetImportAuditError.StalePlan));
            Assert.That(result.AppliedAssetCount, Is.EqualTo(0));
            Assert.That(result.StaleAssetPaths, Is.EqualTo(new[] { SecondAssetPath }));
            Assert.That(GetPlatformSettings(FirstAssetPath, "Android").overridden, Is.False);
            Assert.That(GetPlatformSettings(FirstAssetPath, "Android").maxTextureSize, Is.EqualTo(512));
            Assert.That(GetPlatformSettings(SecondAssetPath, "Android").maxTextureSize, Is.EqualTo(1024));
        }

        [Test]
        public void Apply_DisabledOverridePreservesExistingPlatformValues()
        {
            var importer = GetImporter(FirstAssetPath);
            var current = importer.GetPlatformTextureSettings("Android");
            current.name = "Android";
            current.overridden = true;
            current.maxTextureSize = 512;
            current.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(current);
            importer.SaveAndReimport();
            var expected = CreateAndroidAuditSettings(importer, false, 2048);
            var plan = AssetImportAuditService.Preview(FolderPath, expected);

            current = importer.GetPlatformTextureSettings("Android");
            current.maxTextureSize = 1024;
            current.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(current);
            importer.SaveAndReimport();

            var result = AssetImportAuditService.Apply(plan, new[] { FirstAssetPath });
            var after = GetPlatformSettings(FirstAssetPath, "Android");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(after.overridden, Is.False);
            Assert.That(after.maxTextureSize, Is.EqualTo(1024));
            Assert.That(after.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        }

        [Test]
        public void Apply_EnablingOverrideIgnoresChangesToInactivePlatformValues()
        {
            var importer = GetImporter(FirstAssetPath);
            var current = importer.GetPlatformTextureSettings("Android");
            current.name = "Android";
            current.overridden = false;
            current.maxTextureSize = 512;
            current.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(current);
            importer.SaveAndReimport();
            var expected = new AssetImportAuditTexturePlatformSettings(true, 2048, TextureImporterCompression.Compressed);
            var plan = AssetImportAuditService.Preview(FolderPath, AssetImportAuditTextureAuditSettings.ForPlatform(AssetImportAuditTexturePlatform.Android, expected));

            current = GetPlatformSettings(FirstAssetPath, "Android");
            current.maxTextureSize = 1024;
            current.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(current);
            importer.SaveAndReimport();

            var result = AssetImportAuditService.Apply(plan, new[] { FirstAssetPath });
            var after = GetPlatformSettings(FirstAssetPath, "Android");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(after.overridden, Is.True);
            Assert.That(after.maxTextureSize, Is.EqualTo(2048));
            Assert.That(after.textureCompression, Is.EqualTo(TextureImporterCompression.Compressed));
        }

        [Test]
        public void Apply_EnabledOverridePreservesUnownedPlatformFields()
        {
            var importer = GetImporter(FirstAssetPath);
            var current = importer.GetPlatformTextureSettings("Android");
            current.name = "Android";
            current.overridden = true;
            current.maxTextureSize = 512;
            current.format = TextureImporterFormat.RGBA32;
            current.compressionQuality = 17;
            current.crunchedCompression = false;
            current.resizeAlgorithm = TextureResizeAlgorithm.Mitchell;
            importer.SetPlatformTextureSettings(current);
            importer.SaveAndReimport();
            current = GetPlatformSettings(FirstAssetPath, "Android");
            var expectedFormat = current.format;
            var expectedQuality = current.compressionQuality;
            var expectedCrunch = current.crunchedCompression;
            var expectedResize = current.resizeAlgorithm;
            var expected = CreateAndroidAuditSettings(importer, true, 2048);
            var plan = AssetImportAuditService.Preview(FolderPath, expected);

            var result = AssetImportAuditService.Apply(plan, new[] { FirstAssetPath });
            var after = GetPlatformSettings(FirstAssetPath, "Android");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(after.overridden, Is.True);
            Assert.That(after.maxTextureSize, Is.EqualTo(2048));
            Assert.That(after.format, Is.EqualTo(expectedFormat));
            Assert.That(after.compressionQuality, Is.EqualTo(expectedQuality));
            Assert.That(after.crunchedCompression, Is.EqualTo(expectedCrunch));
            Assert.That(after.resizeAlgorithm, Is.EqualTo(expectedResize));
        }

        [Test]
        public void Apply_RecordsImporterChangeForUndo()
        {
            var importer = GetImporter(FirstAssetPath);
            SetPlatformOverride(importer, "Android", false, 512);
            var before = GetPlatformSettings(FirstAssetPath, "Android");
            var expected = CreateAndroidAuditSettings(importer, true, 2048);
            var plan = AssetImportAuditService.Preview(FolderPath, expected);
            Undo.ClearAll();
            Assert.That(AssetImportAuditService.Apply(plan, new[] { FirstAssetPath }).Succeeded, Is.True);

            Undo.PerformUndo();
            AssetDatabase.ImportAsset(FirstAssetPath, ImportAssetOptions.ForceSynchronousImport);
            var afterUndo = GetPlatformSettings(FirstAssetPath, "Android");

            Assert.That(afterUndo.overridden, Is.EqualTo(before.overridden));
            Assert.That(afterUndo.maxTextureSize, Is.EqualTo(before.maxTextureSize));
        }

        [Test]
        public void Apply_SharedAndPlatformSettingsRestoresTogetherWithUndo()
        {
            var importer = GetImporter(FirstAssetPath);
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            SetPlatformOverride(importer, "iPhone", false, 512);
            var sharedBefore = AssetImportAuditService.Read(importer);
            var platformBefore = GetPlatformSettings(FirstAssetPath, "iPhone");
            var expectedShared = new AssetImportAuditTextureSettings(sharedBefore.MaxTextureSize, sharedBefore.Compression, false, sharedBefore.SRgbTexture, sharedBefore.Readable, sharedBefore.FilterMode, sharedBefore.AnisoLevel);
            var expectedPlatform = new AssetImportAuditTexturePlatformSettings(true, 2048, platformBefore.textureCompression);
            var expected = AssetImportAuditTextureAuditSettings.ForSharedAndPlatform(expectedShared, AssetImportAuditTexturePlatform.iOS, expectedPlatform);
            var plan = AssetImportAuditService.Preview(FolderPath, expected);
            Undo.ClearAll();

            var result = AssetImportAuditService.Apply(plan, new[] { FirstAssetPath });
            Assert.That(result.Succeeded, Is.True);
            Assert.That(GetImporter(FirstAssetPath).mipmapEnabled, Is.False);
            Assert.That(GetPlatformSettings(FirstAssetPath, "iPhone").overridden, Is.True);
            Assert.That(GetPlatformSettings(FirstAssetPath, "iPhone").maxTextureSize, Is.EqualTo(2048));

            Undo.PerformUndo();
            AssetDatabase.ImportAsset(FirstAssetPath, ImportAssetOptions.ForceSynchronousImport);
            var sharedAfterUndo = AssetImportAuditService.Read(GetImporter(FirstAssetPath));
            var platformAfterUndo = GetPlatformSettings(FirstAssetPath, "iPhone");

            Assert.That(sharedAfterUndo, Is.EqualTo(sharedBefore));
            AssertPlatformValuesEqual(platformBefore, platformAfterUndo);
        }

        [Test]
        public void Apply_LegacySharedOnlySettingsLeavesPlatformOverridesUntouched()
        {
            var firstImporter = GetImporter(FirstAssetPath);
            var secondImporter = GetImporter(SecondAssetPath);
            firstImporter.mipmapEnabled = true;
            secondImporter.mipmapEnabled = true;
            firstImporter.SaveAndReimport();
            secondImporter.SaveAndReimport();
            SetPlatformOverride(firstImporter, "Android", false, 512);
            SetPlatformOverride(firstImporter, "iPhone", false, 768);
            SetPlatformOverride(secondImporter, "Android", false, 512);
            SetPlatformOverride(secondImporter, "iPhone", false, 768);
            var firstAndroidBefore = GetPlatformSettings(FirstAssetPath, "Android");
            var firstIphoneBefore = GetPlatformSettings(FirstAssetPath, "iPhone");
            var secondAndroidBefore = GetPlatformSettings(SecondAssetPath, "Android");
            var secondIphoneBefore = GetPlatformSettings(SecondAssetPath, "iPhone");
            var plan = AssetImportAuditService.Preview(FolderPath, AssetImportAuditTextureSettings.Default);

            Assert.That(plan.Issues.All(issue => !issue.IsPlatformSetting), Is.True);
            var result = AssetImportAuditService.Apply(plan);

            Assert.That(result.Succeeded, Is.True);
            AssertPlatformValuesEqual(firstAndroidBefore, GetPlatformSettings(FirstAssetPath, "Android"));
            AssertPlatformValuesEqual(firstIphoneBefore, GetPlatformSettings(FirstAssetPath, "iPhone"));
            AssertPlatformValuesEqual(secondAndroidBefore, GetPlatformSettings(SecondAssetPath, "Android"));
            AssertPlatformValuesEqual(secondIphoneBefore, GetPlatformSettings(SecondAssetPath, "iPhone"));
        }

        [Test]
        public void Audit_DoesNotChangeActiveBuildTarget()
        {
            var importer = GetImporter(FirstAssetPath);
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            var activeBuildTargetBefore = EditorUserBuildSettings.activeBuildTarget;
            var plan = AssetImportAuditService.Preview(FolderPath, AssetImportAuditTextureSettings.Default);

            var result = AssetImportAuditService.Apply(plan);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(EditorUserBuildSettings.activeBuildTarget, Is.EqualTo(activeBuildTargetBefore));
        }

        private static AssetImportAuditTextureAuditSettings CreateAndroidAuditSettings(TextureImporter importer, bool overrideEnabled, int maxTextureSize)
        {
            return AssetImportAuditTextureAuditSettings.ForPlatform(
                AssetImportAuditTexturePlatform.Android,
                new AssetImportAuditTexturePlatformSettings(overrideEnabled, maxTextureSize, importer.GetPlatformTextureSettings("Android").textureCompression));
        }

        private static TextureImporter GetImporter(string assetPath)
        {
            return AssetImporter.GetAtPath(assetPath) as TextureImporter;
        }

        private static TextureImporterPlatformSettings GetPlatformSettings(string assetPath, string platformName)
        {
            return GetImporter(assetPath).GetPlatformTextureSettings(platformName);
        }

        private static void AssertPlatformValuesEqual(TextureImporterPlatformSettings expected, TextureImporterPlatformSettings actual)
        {
            Assert.That(actual.overridden, Is.EqualTo(expected.overridden));
            Assert.That(actual.maxTextureSize, Is.EqualTo(expected.maxTextureSize));
            Assert.That(actual.textureCompression, Is.EqualTo(expected.textureCompression));
            Assert.That(actual.format, Is.EqualTo(expected.format));
            Assert.That(actual.compressionQuality, Is.EqualTo(expected.compressionQuality));
            Assert.That(actual.crunchedCompression, Is.EqualTo(expected.crunchedCompression));
            Assert.That(actual.resizeAlgorithm, Is.EqualTo(expected.resizeAlgorithm));
        }

        private static void SetPlatformOverride(TextureImporter importer, string platformName, bool overrideEnabled, int maxTextureSize)
        {
            var settings = importer.GetPlatformTextureSettings(platformName);
            settings.name = platformName;
            settings.overridden = overrideEnabled;
            settings.maxTextureSize = maxTextureSize;
            importer.SetPlatformTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static string ToAbsolutePath(string assetPath)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
