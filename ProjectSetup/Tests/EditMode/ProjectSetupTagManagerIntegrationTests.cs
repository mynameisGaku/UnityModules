// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProjectSetup.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectSetup.Tests
{
    [Parallelizable(ParallelScope.None)]
    internal sealed class ProjectSetupTagManagerIntegrationTests
    {
        [UnityTest]
        public IEnumerator ApplyAndRestore_AddsMissingNamesThenRestoresTagManagerBytesExactly()
        {
            while (EditorApplication.isCompiling
                || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                yield return null;
            }

            var tagManagerPath = Path.GetFullPath("ProjectSettings/TagManager.asset");
            var originalBytes = File.ReadAllBytes(tagManagerPath);
            var backupDirectory = Path.Combine(Path.GetTempPath(), "ProjectSetupTagManagerTests", Guid.NewGuid().ToString("N"));
            var backupPath = Path.Combine(backupDirectory, "backup.json");
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 10);
            var tag = $"ProjectSetupTag{suffix}";
            var layer = $"ProjectSetupLayer{suffix}";
            var sortingLayer = $"ProjectSetupSorting{suffix}";
            var profile = ScriptableObject.CreateInstance<ProjectSetupProfile>();
            try
            {
                profile.SetRecommendedDefaults();
                profile.ConfigureAssetSerialization = false;
                profile.ConfigureVersionControl = false;
                profile.ConfigureTags = true;
                profile.Tags = new[] { tag };
                profile.ConfigureLayers = true;
                profile.Layers = new[] { layer };
                profile.ConfigureSortingLayers = true;
                profile.SortingLayers = new[] { sortingLayer };
                var environment = new UnityProjectSetupEnvironment();
                var service = new ProjectSetupService(environment, new ProjectSetupBackupStore(backupPath));

                var applied = service.Apply(profile);
                var changed = environment.Capture();

                Assert.That(applied.Succeeded, Is.True, applied.Message);
                Assert.That(changed.Tags, Does.Contain(tag));
                Assert.That(changed.Layers.Skip(8), Does.Contain(layer));
                Assert.That(changed.SortingLayers.Select(value => value.Name), Does.Contain(sortingLayer));
                Assert.That(changed.SortingLayers.Single(value => value.Name == sortingLayer).UniqueId, Is.GreaterThan(0));

                var restored = service.RestoreLast();

                Assert.That(restored.Succeeded, Is.True, restored.Message);
                CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(tagManagerPath));
                Assert.That(File.Exists(backupPath + ".tmp"), Is.False);
            }
            finally
            {
                if (!File.ReadAllBytes(tagManagerPath).SequenceEqual(originalBytes))
                {
                    File.WriteAllBytes(tagManagerPath, originalBytes);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                }

                if (Directory.Exists(backupDirectory))
                {
                    Directory.Delete(backupDirectory, true);
                }

                UnityEngine.Object.DestroyImmediate(profile);
            }

            while (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                yield return null;
            }
        }
    }
}
