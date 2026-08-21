// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Text;
using BuildGuard.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BuildGuard.Tests
{
    /// <summary>
    /// Verifies deterministic scanning of selected Prefab assets.
    /// </summary>
    [Parallelizable(ParallelScope.None)]
    internal sealed class BuildGuardPrefabScannerTests
    {
        private string _temporaryFolder;

        [SetUp]
        public void SetUp()
        {
            _temporaryFolder = $"Assets/__BuildGuardPrefabScannerTests_{Guid.NewGuid():N}";
            Assert.IsNotEmpty(AssetDatabase.CreateFolder("Assets", Path.GetFileName(_temporaryFolder)));
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(_temporaryFolder))
            {
                Assert.IsTrue(AssetDatabase.DeleteAsset(_temporaryFolder));
            }
        }

        [Test]
        public void Scan_BrokenPrefab_ReturnsMissingScriptWithExactPath()
        {
            var prefabPath = CopyBrokenPrefab("Broken.prefab");

            var result = BuildGuardPrefabScanner.Scan(new[] { prefabPath });

            Assert.That(result.Cancelled, Is.False);
            Assert.That(result.ScannedPrefabCount, Is.EqualTo(1));
            Assert.That(result.Issues, Has.Count.EqualTo(1));
            Assert.That(result.Issues[0].Kind, Is.EqualTo(BuildGuardIssueKind.MissingScript));
            Assert.That(result.Issues[0].PrefabPath, Is.EqualTo(prefabPath));
            Assert.That(result.Issues[0].HierarchyPath, Is.EqualTo("Broken[0]"));
            Assert.That(result.Issues[0].Details, Is.EqualTo("Missing Scripts: 1"));
        }

        [Test]
        public void Scan_DeletedPrefabReference_ReturnsComponentAndProperty()
        {
            var prefabPath = CreatePrefabWithDeletedCameraTarget();

            var result = BuildGuardPrefabScanner.Scan(new[] { prefabPath });

            Assert.That(result.Issues, Has.Count.EqualTo(1));
            Assert.That(result.Issues[0].Kind, Is.EqualTo(BuildGuardIssueKind.MissingObjectReference));
            Assert.That(result.Issues[0].HierarchyPath, Is.EqualTo("MissingObjectReference[0]"));
            Assert.That(result.Issues[0].Details, Is.EqualTo("UnityEngine.Camera[1].m_TargetTexture"));
        }

        [Test]
        public void Scan_DuplicatePaths_UsesOrdinalOrderOnceAndSupportsCancellation()
        {
            var first = CreateValidPrefab("A.prefab");
            var second = CreateValidPrefab("Z.prefab");
            var visited = string.Empty;

            var result = BuildGuardPrefabScanner.Scan(
                new[] { second, first, first.Replace('/', '\\') },
                (index, total, path) =>
                {
                    visited += $"{index}/{total}:{path}|";
                    return index == 1;
                });

            Assert.That(result.Cancelled, Is.True);
            Assert.That(result.ScannedPrefabCount, Is.EqualTo(1));
            Assert.That(result.Issues, Is.Empty);
            Assert.That(visited, Is.EqualTo($"0/2:{first}|1/2:{second}|"));
        }

        [Test]
        public void NormalizePrefabPaths_RejectsFoldersScenesAndPackageFixtures()
        {
            Assert.That(
                () => BuildGuardPrefabScanner.NormalizePrefabPaths(new[] { _temporaryFolder }),
                Throws.TypeOf<ArgumentException>());
            Assert.That(
                () => BuildGuardPrefabScanner.NormalizePrefabPaths(new[] { "Assets/Missing.prefab" }),
                Throws.TypeOf<ArgumentException>());
            Assert.That(
                () => BuildGuardPrefabScanner.NormalizePrefabPaths(new[]
                {
                    AssetDatabase.GUIDToAssetPath(MissingScriptSceneScannerTests.BrokenPrefabFixtureGuid)
                }),
                Throws.TypeOf<ArgumentException>());
        }

        internal string CopyBrokenPrefab(string fileName)
        {
            var sourcePath = AssetDatabase.GUIDToAssetPath(MissingScriptSceneScannerTests.BrokenPrefabFixtureGuid);
            Assert.That(sourcePath, Is.Not.Empty);
            var destinationPath = $"{_temporaryFolder}/{fileName}";
            File.WriteAllText(destinationPath, File.ReadAllText(sourcePath), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(
                destinationPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return destinationPath;
        }

        private string CreateValidPrefab(string fileName)
        {
            var instance = new GameObject(Path.GetFileNameWithoutExtension(fileName));
            try
            {
                return PrefabUtility.SaveAsPrefabAsset(instance, $"{_temporaryFolder}/{fileName}") != null
                    ? $"{_temporaryFolder}/{fileName}"
                    : throw new InvalidOperationException("Prefab creation failed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private string CreatePrefabWithDeletedCameraTarget()
        {
            var texturePath = $"{_temporaryFolder}/DeletedTarget.renderTexture";
            var texture = new RenderTexture(16, 16, 0);
            AssetDatabase.CreateAsset(texture, texturePath);
            var instance = new GameObject("Camera Root", typeof(Camera));
            instance.GetComponent<Camera>().targetTexture = texture;
            var prefabPath = $"{_temporaryFolder}/MissingObjectReference.prefab";
            try
            {
                Assert.That(PrefabUtility.SaveAsPrefabAsset(instance, prefabPath), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            Assert.IsTrue(AssetDatabase.DeleteAsset(texturePath));
            return prefabPath;
        }
    }
}
