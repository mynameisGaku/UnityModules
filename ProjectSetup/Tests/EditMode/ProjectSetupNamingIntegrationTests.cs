// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using ProjectSetup.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectSetup.Tests
{
    internal sealed class ProjectSetupNamingIntegrationTests
    {
        [UnityTest]
        public IEnumerator ApplyAndRestore_RoundTripsDuplicateNamingSettings()
        {
            while (EditorApplication.isCompiling
                || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                yield return null;
            }

            var originalScheme = EditorSettings.gameObjectNamingScheme;
            var originalDigits = EditorSettings.gameObjectNamingDigits;
            var originalSpacing = EditorSettings.assetNamingUsesSpace;
            var desiredScheme = originalScheme == EditorSettings.NamingScheme.Underscore
                ? EditorSettings.NamingScheme.Dot
                : EditorSettings.NamingScheme.Underscore;
            var desiredDigits = originalDigits == 3 ? 4 : 3;
            var backupPath = Path.Combine(Path.GetTempPath(), "ProjectSetupTests", Guid.NewGuid().ToString("N"), "backup.json");
            var profile = ScriptableObject.CreateInstance<ProjectSetupProfile>();
            profile.SetRecommendedDefaults();
            profile.ConfigureAssetSerialization = false;
            profile.ConfigureVersionControl = false;
            profile.ConfigureNamingDefaults = true;
            profile.GameObjectNamingScheme = desiredScheme;
            profile.GameObjectNamingDigits = desiredDigits;
            profile.AssetNamingUsesSpace = !originalSpacing;
            var service = new ProjectSetupService(
                new UnityProjectSetupEnvironment(),
                new ProjectSetupBackupStore(backupPath));

            try
            {
                var result = service.Apply(profile);

                Assert.That(result.Succeeded, Is.True, result.Message);
                Assert.That(EditorSettings.gameObjectNamingScheme, Is.EqualTo(desiredScheme));
                Assert.That(EditorSettings.gameObjectNamingDigits, Is.EqualTo(desiredDigits));
                Assert.That(EditorSettings.assetNamingUsesSpace, Is.EqualTo(!originalSpacing));

                var restore = service.RestoreLast();

                Assert.That(restore.Succeeded, Is.True, restore.Message);
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
                var directory = Path.GetDirectoryName(backupPath);
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }

        }
    }
}
