// SPDX-License-Identifier: MIT

using System.Linq;
using NUnit.Framework;
using ProjectSetup.Editor;
using UnityEditor;
using UnityEngine;

namespace ProjectSetup.Tests
{
    internal sealed class ProjectSetupProfileTests
    {
        [Test]
        public void RecommendedDefaults_OwnOnlyVersionControlSafetySettings()
        {
            var profile = ScriptableObject.CreateInstance<ProjectSetupProfile>();
            try
            {
                profile.SetRecommendedDefaults();

                Assert.That(profile.ConfigureAssetSerialization, Is.True);
                Assert.That(profile.AssetSerialization, Is.EqualTo(SerializationMode.ForceText));
                Assert.That(profile.ConfigureVersionControl, Is.True);
                Assert.That(profile.VersionControlMode, Is.EqualTo("Visible Meta Files"));
                Assert.That(profile.ConfigureEnterPlayMode, Is.False);
                Assert.That(profile.ConfigureColorSpace, Is.False);
                Assert.That(profile.ConfigureRunInBackground, Is.False);
                Assert.That(profile.ConfigureCompanyName, Is.False);
                Assert.That(profile.ConfigureProductName, Is.False);
                Assert.That(profile.ConfigureBundleVersion, Is.False);
                Assert.That(profile.ConfigureBuildScenes, Is.False);
                Assert.That(profile.BuildScenes, Is.Empty);
                Assert.That(profile.ConfigureTags, Is.False);
                Assert.That(profile.Tags, Is.Empty);
                Assert.That(profile.ConfigureLayers, Is.False);
                Assert.That(profile.Layers, Is.Empty);
                Assert.That(profile.ConfigureSortingLayers, Is.False);
                Assert.That(profile.SortingLayers, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Capture_EnablesAndCopiesEverySupportedSetting()
        {
            var profile = ScriptableObject.CreateInstance<ProjectSetupProfile>();
            try
            {
                var layers = new string[32];
                layers[8] = "Gameplay";
                layers[12] = "Interaction";
                var snapshot = new ProjectSetupSnapshot(
                    SerializationMode.Mixed,
                    "Hidden Meta Files",
                    true,
                    EnterPlayModeOptions.DisableSceneReload,
                    ColorSpace.Linear,
                    true,
                    "Company",
                    "Product",
                    "3.0.0",
                    true,
                    new[] { "Untagged", "Player", "Collectible" },
                    new[] { "Collectible" },
                    layers,
                    new[]
                    {
                        new ProjectSetupSortingLayer("Default", 0, false),
                        new ProjectSetupSortingLayer("Foreground", 15, false)
                    },
                    string.Empty,
                    true,
                    "global",
                    "Global Build Scenes",
                    new[]
                    {
                        new ProjectSetupBuildSceneState("guid-a", "Assets/Bootstrap.unity", true),
                        new ProjectSetupBuildSceneState("guid-b", "Assets/Gameplay.unity", false)
                    });

                profile.Capture(snapshot);

                Assert.That(profile.ConfigureAssetSerialization, Is.True);
                Assert.That(profile.AssetSerialization, Is.EqualTo(SerializationMode.Mixed));
                Assert.That(profile.ConfigureVersionControl, Is.True);
                Assert.That(profile.VersionControlMode, Is.EqualTo("Hidden Meta Files"));
                Assert.That(profile.ConfigureEnterPlayMode, Is.True);
                Assert.That(profile.EnterPlayModeOptions, Is.EqualTo(EnterPlayModeOptions.DisableSceneReload));
                Assert.That(profile.ConfigureColorSpace, Is.True);
                Assert.That(profile.ColorSpace, Is.EqualTo(ColorSpace.Linear));
                Assert.That(profile.ConfigureRunInBackground, Is.True);
                Assert.That(profile.RunInBackground, Is.True);
                Assert.That(profile.ConfigureCompanyName, Is.True);
                Assert.That(profile.CompanyName, Is.EqualTo("Company"));
                Assert.That(profile.ConfigureProductName, Is.True);
                Assert.That(profile.ProductName, Is.EqualTo("Product"));
                Assert.That(profile.ConfigureBundleVersion, Is.True);
                Assert.That(profile.BundleVersion, Is.EqualTo("3.0.0"));
                Assert.That(profile.ConfigureBuildScenes, Is.True);
                Assert.That(profile.BuildScenes.Select(scene => scene.SceneGuid), Is.EqualTo(new[] { "guid-a", "guid-b" }));
                Assert.That(profile.BuildScenes.Select(scene => scene.Enabled), Is.EqualTo(new[] { true, false }));
                Assert.That(profile.ConfigureTags, Is.True);
                Assert.That(profile.Tags, Is.EqualTo(new[] { "Collectible" }));
                Assert.That(profile.ConfigureLayers, Is.True);
                Assert.That(profile.Layers, Is.EqualTo(new[] { "Gameplay", "Interaction" }));
                Assert.That(profile.ConfigureSortingLayers, Is.True);
                Assert.That(profile.SortingLayers, Is.EqualTo(new[] { "Foreground" }));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }
}
