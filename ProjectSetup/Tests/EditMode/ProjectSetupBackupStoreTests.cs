// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using ProjectSetup.Editor;
using UnityEditor;
using UnityEngine;

namespace ProjectSetup.Tests
{
    internal sealed class ProjectSetupBackupStoreTests
    {
        private string _directory;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "ProjectSetupTests", Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_directory, "backup.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public void SaveAndLoad_RoundTripsSnapshotWithoutBomOrTemporaryFile()
        {
            var expected = Snapshot("Company \uD83D\uDE80");
            var store = new ProjectSetupBackupStore(_path);

            store.Save(expected);
            var loaded = store.TryLoad(out var actual, out var error);

            Assert.That(loaded, Is.True, error);
            Assert.That(actual.Tags, Is.EqualTo(expected.Tags), "Available Tags changed during backup serialization.");
            Assert.That(actual.CustomTags, Is.EqualTo(expected.CustomTags), "Custom Tags changed during backup serialization.");
            Assert.That(actual.Layers, Is.EqualTo(expected.Layers), "Layers changed during backup serialization.");
            Assert.That(actual.SortingLayers, Is.EqualTo(expected.SortingLayers), "Sorting Layers changed during backup serialization.");
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(File.Exists(_path + ".tmp"), Is.False);
            var bytes = File.ReadAllBytes(_path);
            Assert.That(bytes, Has.Length.GreaterThan(0));
            Assert.That(bytes.Take(3).ToArray(), Is.Not.EqualTo(Encoding.UTF8.GetPreamble()));
            Assert.That(actual.BuildScenes, Is.EqualTo(expected.BuildScenes));
            Assert.That(actual.PlayModeStartSceneGuid, Is.EqualTo(expected.PlayModeStartSceneGuid));
            Assert.That(actual.ScriptingDefineSymbols, Is.EqualTo(expected.ScriptingDefineSymbols));
            Assert.That(actual.RootNamespace, Is.EqualTo(expected.RootNamespace));
            Assert.That(actual.NewScriptLineEndings, Is.EqualTo(expected.NewScriptLineEndings));
            Assert.That(actual.GameObjectNamingScheme, Is.EqualTo(expected.GameObjectNamingScheme));
            Assert.That(actual.GameObjectNamingDigits, Is.EqualTo(expected.GameObjectNamingDigits));
            Assert.That(actual.AssetNamingUsesSpace, Is.EqualTo(expected.AssetNamingUsesSpace));
            Assert.That(actual.CreatedProjectFolders, Is.EqualTo(expected.CreatedProjectFolders));
            Assert.That(File.ReadAllText(_path), Does.Contain("\"schemaVersion\": 8"));
        }

        [Test]
        public void Save_ReplacesExistingBackup()
        {
            var store = new ProjectSetupBackupStore(_path);
            store.Save(Snapshot("First"));

            store.Save(Snapshot("Second"));
            var loaded = store.TryLoad(out var actual, out var error);

            Assert.That(loaded, Is.True, error);
            Assert.That(actual.CompanyName, Is.EqualTo("Second"));
        }

        [Test]
        public void SaveAndLoad_PreservesSnapshotWithoutBuildSceneData()
        {
            var expected = new ProjectSetupSnapshot(
                SerializationMode.ForceText,
                "Visible Meta Files",
                false,
                EnterPlayModeOptions.None,
                ColorSpace.Gamma,
                false,
                "Company",
                "Product",
                "1.0.0");
            var store = new ProjectSetupBackupStore(_path);

            store.Save(expected);
            var loaded = store.TryLoad(out var actual, out var error);

            Assert.That(loaded, Is.True, error);
            Assert.That(actual.HasBuildSceneData, Is.False);
            Assert.That(actual.CompanyName, Is.EqualTo(expected.CompanyName));
            Assert.That(actual.ProductName, Is.EqualTo(expected.ProductName));
        }

        [Test]
        public void TryLoad_MissingFileReturnsExplicitError()
        {
            var store = new ProjectSetupBackupStore(_path);

            var loaded = store.TryLoad(out _, out var error);

            Assert.That(loaded, Is.False);
            Assert.That(error, Does.Contain("No Project Setup backup"));
        }

        [Test]
        public void TryLoad_UnsupportedSchemaReturnsExplicitError()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_path, "{\"schemaVersion\":99}", new UTF8Encoding(false));
            var store = new ProjectSetupBackupStore(_path);

            var loaded = store.TryLoad(out _, out var error);

            Assert.That(loaded, Is.False);
            Assert.That(error, Does.Contain("schema"));
        }

        [Test]
        public void TryLoad_SchemaOneRemainsCompatibleWithoutTagManagerRestoreData()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(
                _path,
                "{\"schemaVersion\":1,\"assetSerialization\":2,\"versionControlMode\":\"Visible Meta Files\",\"enterPlayModeOptionsEnabled\":false,\"enterPlayModeOptions\":0,\"colorSpace\":0,\"runInBackground\":false,\"companyName\":\"Legacy\",\"productName\":\"Product\",\"bundleVersion\":\"1.0.0\"}",
                new UTF8Encoding(false));
            var store = new ProjectSetupBackupStore(_path);

            var loaded = store.TryLoad(out var snapshot, out var error);

            Assert.That(loaded, Is.True, error);
            Assert.That(snapshot.CompanyName, Is.EqualTo("Legacy"));
            Assert.That(snapshot.HasTagManagerData, Is.False);
            Assert.That(snapshot.CustomTags, Is.Empty);
            Assert.That(snapshot.HasBuildSceneData, Is.False);
            Assert.That(snapshot.HasPlayModeStartSceneData, Is.False);
            Assert.That(snapshot.HasCodeGenerationData, Is.False);
            Assert.That(snapshot.HasNamingData, Is.False);
            Assert.That(snapshot.CreatedProjectFolders, Is.Empty);
        }

        private static ProjectSetupSnapshot Snapshot(string companyName)
        {
            var layers = Enumerable.Repeat(string.Empty, 32).ToArray();
            layers[8] = "Gameplay";
            return new ProjectSetupSnapshot(
                SerializationMode.ForceText,
                "Visible Meta Files",
                true,
                EnterPlayModeOptions.DisableDomainReload,
                ColorSpace.Linear,
                true,
                companyName,
                "Product",
                "2.5.0",
                true,
                new[] { "Untagged", "Checkpoint" },
                new[] { "Checkpoint" },
                layers,
                new[]
                {
                    new ProjectSetupSortingLayer("Default", 0, false),
                    new ProjectSetupSortingLayer("Foreground", 12, false)
                },
                "tag manager backup",
                true,
                "global",
                "Global Build Scenes",
                new[]
                {
                    new ProjectSetupBuildSceneState("guid-bootstrap", "Assets/Bootstrap.unity", true),
                    new ProjectSetupBuildSceneState("guid-gameplay", "Assets/Gameplay.unity", false)
                },
                true,
                "guid-bootstrap",
                "Assets/Bootstrap.unity",
                true,
                "Standalone",
                "Standalone",
                new[] { "PROJECT_FEATURE", "DEBUG_MENU" },
                true,
                "Studio.Game",
                LineEndingsMode.Unix,
                true,
                EditorSettings.NamingScheme.Dot,
                4,
                false,
                createdProjectFolders: new[] { "Assets/Generated", "Assets/Generated/Data" });
        }
    }
}
