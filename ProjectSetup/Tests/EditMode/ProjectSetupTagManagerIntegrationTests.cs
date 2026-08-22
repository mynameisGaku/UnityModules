// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProjectSetup.Editor;
using UnityEditor;
using UnityEngine;

namespace ProjectSetup.Tests
{
    [Parallelizable(ParallelScope.None)]
    internal sealed class ProjectSetupTagManagerIntegrationTests
    {
        [Test]
        public void ApplyAndRestore_AddsMissingNamesThenRestoresTagManagerBytesExactly()
        {
            var tagManagerPath = Path.GetFullPath("ProjectSettings/TagManager.asset");
            var originalBytes = File.ReadAllBytes(tagManagerPath);
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
                var snapshot = environment.Capture();

                environment.Apply(profile);
                var changed = environment.Capture();

                Assert.That(changed.Tags, Does.Contain(tag));
                Assert.That(changed.Layers.Skip(8), Does.Contain(layer));
                Assert.That(changed.SortingLayers.Select(value => value.Name), Does.Contain(sortingLayer));
                Assert.That(changed.SortingLayers.Single(value => value.Name == sortingLayer).UniqueId, Is.GreaterThan(0));

                environment.Apply(snapshot);

                CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(tagManagerPath));
            }
            finally
            {
                if (!File.ReadAllBytes(tagManagerPath).SequenceEqual(originalBytes))
                {
                    File.WriteAllBytes(tagManagerPath, originalBytes);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                }

                UnityEngine.Object.DestroyImmediate(profile);
            }
        }
    }
}
