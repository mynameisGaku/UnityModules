// SPDX-License-Identifier: MIT

using System;
using System.IO;
using NUnit.Framework;
using ProjectSetup.Editor;
using UnityEditor;
using UnityEngine;

namespace ProjectSetup.Tests
{
    [Parallelizable(ParallelScope.None)]
    internal sealed class ProjectSetupFolderIntegrationTests
    {
        [Test]
        public void ApplyAndRestore_CreatesMissingFoldersAndRemovesOnlyEmptyOwnedFolders()
        {
            var rootName = "ProjectSetupFolderTests_" + Guid.NewGuid().ToString("N");
            var rootPath = "Assets/" + rootName;
            var backupPath = Path.Combine(Path.GetTempPath(), rootName + ".json");
            var profile = ScriptableObject.CreateInstance<ProjectSetupProfile>();
            try
            {
                CreateExactFolder("Assets", rootName);
                CreateExactFolder(rootPath, "Existing");
                profile.SetRecommendedDefaults();
                profile.ConfigureAssetSerialization = false;
                profile.ConfigureVersionControl = false;
                profile.ConfigureProjectFolders = true;
                profile.ProjectFolders = new[]
                {
                    rootPath + "/Existing",
                    rootPath + "/Empty",
                    rootPath + "/Used",
                    rootPath + "/Nested/Leaf"
                };
                var service = new ProjectSetupService(
                    new UnityProjectSetupEnvironment(),
                    new ProjectSetupBackupStore(backupPath));

                var apply = service.Apply(profile);

                Assert.That(apply.Succeeded, Is.True, apply.Message);
                Assert.That(AssetDatabase.IsValidFolder(rootPath + "/Empty"), Is.True);
                Assert.That(AssetDatabase.IsValidFolder(rootPath + "/Nested/Leaf"), Is.True);
                var retainedAsset = ScriptableObject.CreateInstance<ProjectSetupProfile>();
                AssetDatabase.CreateAsset(retainedAsset, rootPath + "/Used/Keep.asset");
                AssetDatabase.SaveAssets();

                var restore = service.RestoreLast();

                Assert.That(restore.Succeeded, Is.True, restore.Message);
                Assert.That(AssetDatabase.IsValidFolder(rootPath + "/Empty"), Is.False);
                Assert.That(AssetDatabase.IsValidFolder(rootPath + "/Nested"), Is.False);
                Assert.That(AssetDatabase.IsValidFolder(rootPath + "/Used"), Is.True);
                Assert.That(AssetDatabase.LoadAssetAtPath<ProjectSetupProfile>(rootPath + "/Used/Keep.asset"), Is.Not.Null);
                Assert.That(AssetDatabase.IsValidFolder(rootPath + "/Existing"), Is.True);
                Assert.That(AssetDatabase.IsValidFolder(rootPath), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                if (AssetDatabase.IsValidFolder(rootPath))
                {
                    AssetDatabase.DeleteAsset(rootPath);
                }

                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }

                if (File.Exists(backupPath + ".tmp"))
                {
                    File.Delete(backupPath + ".tmp");
                }

                AssetDatabase.Refresh();
            }
        }

        private static void CreateExactFolder(string parent, string name)
        {
            var guid = AssetDatabase.CreateFolder(parent, name);
            var path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
            Assert.That(path, Is.EqualTo(parent + "/" + name));
        }
    }
}
