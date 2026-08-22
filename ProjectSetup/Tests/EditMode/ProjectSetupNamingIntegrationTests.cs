// SPDX-License-Identifier: MIT

using NUnit.Framework;
using ProjectSetup.Editor;
using UnityEditor;
using UnityEngine;

namespace ProjectSetup.Tests
{
    internal sealed class ProjectSetupNamingIntegrationTests
    {
        [Test]
        public void ApplyAndRestore_RoundTripsDuplicateNamingSettings()
        {
            var originalScheme = EditorSettings.gameObjectNamingScheme;
            var originalDigits = EditorSettings.gameObjectNamingDigits;
            var originalSpacing = EditorSettings.assetNamingUsesSpace;
            var desiredScheme = originalScheme == EditorSettings.NamingScheme.Underscore
                ? EditorSettings.NamingScheme.Dot
                : EditorSettings.NamingScheme.Underscore;
            var desiredDigits = originalDigits == 3 ? 4 : 3;
            var profile = ScriptableObject.CreateInstance<ProjectSetupProfile>();
            profile.SetRecommendedDefaults();
            profile.ConfigureAssetSerialization = false;
            profile.ConfigureVersionControl = false;
            profile.ConfigureNamingDefaults = true;
            profile.GameObjectNamingScheme = desiredScheme;
            profile.GameObjectNamingDigits = desiredDigits;
            profile.AssetNamingUsesSpace = !originalSpacing;
            var environment = new UnityProjectSetupEnvironment();
            var snapshot = environment.Capture();

            try
            {
                environment.Apply(profile);

                Assert.That(EditorSettings.gameObjectNamingScheme, Is.EqualTo(desiredScheme));
                Assert.That(EditorSettings.gameObjectNamingDigits, Is.EqualTo(desiredDigits));
                Assert.That(EditorSettings.assetNamingUsesSpace, Is.EqualTo(!originalSpacing));

                environment.Apply(snapshot);

                Assert.That(EditorSettings.gameObjectNamingScheme, Is.EqualTo(originalScheme));
                Assert.That(EditorSettings.gameObjectNamingDigits, Is.EqualTo(originalDigits));
                Assert.That(EditorSettings.assetNamingUsesSpace, Is.EqualTo(originalSpacing));
            }
            finally
            {
                EditorSettings.gameObjectNamingScheme = originalScheme;
                EditorSettings.gameObjectNamingDigits = originalDigits;
                EditorSettings.assetNamingUsesSpace = originalSpacing;
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }
    }
}
