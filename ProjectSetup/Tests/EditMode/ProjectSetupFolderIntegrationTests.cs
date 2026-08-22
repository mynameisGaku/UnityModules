// SPDX-License-Identifier: MIT

using System;
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
                var environment = new UnityProjectSetupEnvironment();
                var before = environment.Capture();

                var created = environment.Apply(profile);

                Assert.That(created, Is.EquivalentTo(new[]
                {
                    rootPath + "/Empty",
                    rootPath + "/Used",
                    rootPath + "/Nested",
                    rootPath + "/Nested/Leaf"
                }));
                Assert.That(AssetDatabase.IsValidFolder(rootPath + "/Empty"), Is.True);
                Assert.That(AssetDatabase.IsValidFolder(rootPath + "/Nested/Leaf"), Is.True);
                var retainedAsset = ScriptableObject.CreateInstance<ProjectSetupProfile>();
                AssetDatabase.CreateAsset(retainedAsset, rootPath + "/Used/Keep.asset");
                AssetDatabase.SaveAssets();

                environment.Apply(before.WithCreatedProjectFolders(created));

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
