// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BuildGuard.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildGuard.Tests
{
    /// <summary>
    /// Verifies missing script scanning across hierarchy, inactive objects, and Prefab instances.
    /// </summary>
    [Parallelizable(ParallelScope.None)]
    internal sealed class MissingScriptSceneScannerTests
    {
        /// <summary>
        /// Identifies the missing script Scene fixture.
        /// </summary>
        internal const string BrokenSceneFixtureGuid = "62568305b48f4bfb8de5c5786171f370";

        /// <summary>
        /// Identifies the missing script Prefab fixture.
        /// </summary>
        internal const string BrokenPrefabFixtureGuid = "1288dc4ed86b4939a6b9be1a70cf5ef5";

        /// <summary>
        /// Stores the temporary asset folder for the current test.
        /// </summary>
        private string _temporaryFolder;

        /// <summary>
        /// Gets the temporary asset folder for processor tests.
        /// </summary>
        internal string TemporaryFolder => _temporaryFolder;

        /// <summary>
        /// Creates a dedicated temporary asset folder for each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _temporaryFolder = $"Assets/__BuildGuardTests_{Guid.NewGuid():N}";
            Assert.IsNotEmpty(AssetDatabase.CreateFolder("Assets", Path.GetFileName(_temporaryFolder)));
        }

        /// <summary>
        /// Removes temporary Scenes and assets after each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            for (var index = SceneManager.sceneCount - 1; index >= 0; index--)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (!string.IsNullOrEmpty(scene.path) && scene.path.StartsWith(_temporaryFolder, StringComparison.Ordinal))
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            if (AssetDatabase.IsValidFolder(_temporaryFolder))
            {
                Assert.IsTrue(AssetDatabase.DeleteAsset(_temporaryFolder));
            }
        }

        /// <summary>
        /// Verifies that all missing scripts are counted in active and inactive hierarchy nodes.
        /// </summary>
        [Test]
        public void Scan_BrokenScene_IncludesInactiveHierarchy()
        {
            var scene = OpenSceneFixture();

            var findings = MissingScriptSceneScanner.Scan(scene);

            Assert.That(findings.Count, Is.EqualTo(2));
            Assert.That(findings[0].HierarchyPath, Is.EqualTo("Broken\\/Root[0]"));
            Assert.That(findings[0].MissingScriptCount, Is.EqualTo(1));
            Assert.That(findings[1].HierarchyPath, Is.EqualTo("Broken\\/Root[0]/Inactive Child[0]"));
            Assert.That(findings[1].MissingScriptCount, Is.EqualTo(2));
        }

        /// <summary>
        /// Verifies that a valid inactive hierarchy produces no findings.
        /// </summary>
        [Test]
        public void Scan_ValidInactiveHierarchy_ReturnsEmpty()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Valid Root");
            SceneManager.MoveGameObjectToScene(root, scene);
            var child = new GameObject("Inactive Child");
            child.transform.SetParent(root.transform, false);
            child.SetActive(false);

            var findings = MissingScriptSceneScanner.Scan(scene);

            Assert.That(findings, Is.Empty);
        }

        /// <summary>
        /// Verifies that missing scripts inside a Prefab instance are reported as Scene hierarchy paths.
        /// </summary>
        [Test]
        public void Scan_BrokenPrefabInstance_FindsNestedObject()
        {
            var prefabPath = CopyFixture(BrokenPrefabFixtureGuid, "BrokenPrefab.prefab");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            Assert.That(instance, Is.Not.Null);

            var findings = MissingScriptSceneScanner.Scan(scene);

            Assert.That(findings.Count, Is.EqualTo(1));
            Assert.That(findings[0].HierarchyPath, Is.EqualTo("BrokenPrefab[0]"));
            Assert.That(findings[0].MissingScriptCount, Is.EqualTo(1));
        }

        /// <summary>
        /// Verifies that path separators and control characters are escaped into one line.
        /// </summary>
        [Test]
        public void EscapePathText_ControlCharacters_AreEscaped()
        {
            Assert.That(MissingScriptSceneScanner.EscapePathText("A/B\\C\r\n\t"), Is.EqualTo("A\\/B\\\\C\\r\\n\\t"));
        }

        /// <summary>
        /// Verifies that an invalid Scene is rejected instead of treated as empty.
        /// </summary>
        [Test]
        public void Scan_InvalidScene_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => MissingScriptSceneScanner.Scan(default));
        }

        /// <summary>
        /// Opens the missing script Scene fixture as a temporary asset.
        /// </summary>
        internal Scene OpenSceneFixture()
        {
            var scenePath = CopyFixture(BrokenSceneFixtureGuid, "BrokenScene.unity");
            return EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        }

        /// <summary>
        /// Copies a text fixture resolved by GUID into a temporary asset with the requested extension.
        /// </summary>
        private string CopyFixture(string fixtureGuid, string destinationName)
        {
            var sourcePath = AssetDatabase.GUIDToAssetPath(fixtureGuid);
            Assert.That(sourcePath, Is.Not.Empty);
            var destinationPath = $"{_temporaryFolder}/{destinationName}";
            File.WriteAllText(destinationPath, File.ReadAllText(sourcePath), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return destinationPath;
        }
    }
}
