// SPDX-License-Identifier: MIT

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
                var snapshot = new ProjectSetupSnapshot(
                    SerializationMode.Mixed,
                    "Hidden Meta Files",
                    true,
                    EnterPlayModeOptions.DisableSceneReload,
                    ColorSpace.Linear,
                    true,
                    "Company",
                    "Product",
                    "3.0.0");

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
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }
}
