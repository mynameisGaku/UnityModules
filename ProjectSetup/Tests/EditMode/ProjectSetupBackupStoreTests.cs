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
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(File.Exists(_path + ".tmp"), Is.False);
            var bytes = File.ReadAllBytes(_path);
            Assert.That(bytes, Has.Length.GreaterThan(0));
            Assert.That(bytes.Take(3).ToArray(), Is.Not.EqualTo(Encoding.UTF8.GetPreamble()));
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

        private static ProjectSetupSnapshot Snapshot(string companyName)
        {
            return new ProjectSetupSnapshot(
                SerializationMode.ForceText,
                "Visible Meta Files",
                true,
                EnterPlayModeOptions.DisableDomainReload,
                ColorSpace.Linear,
                true,
                companyName,
                "Product",
                "2.5.0");
        }
    }
}
