// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Text;
using BuildGuard.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BuildGuard.Tests
{
    /// <summary>
    /// Verifies selected Prefab navigation and missing-script repair.
    /// </summary>
    [Parallelizable(ParallelScope.None)]
    internal sealed class BuildGuardPrefabScanWindowTests
    {
        private string _temporaryFolder;
        private BuildGuardPrefabScanWindow _window;

        [SetUp]
        public void SetUp()
        {
            _temporaryFolder = $"Assets/__BuildGuardPrefabWindowTests_{Guid.NewGuid():N}";
            Assert.IsNotEmpty(AssetDatabase.CreateFolder("Assets", Path.GetFileName(_temporaryFolder)));
            _window = ScriptableObject.CreateInstance<BuildGuardPrefabScanWindow>();
            Selection.objects = Array.Empty<UnityEngine.Object>();
        }

        [TearDown]
        public void TearDown()
        {
            Selection.objects = Array.Empty<UnityEngine.Object>();
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                stage.ClearDirtiness();
                StageUtility.GoToMainStage();
            }

            UnityEngine.Object.DestroyImmediate(_window);
            if (AssetDatabase.IsValidFolder(_temporaryFolder))
            {
                Assert.IsTrue(AssetDatabase.DeleteAsset(_temporaryFolder));
            }
        }

        [Test]
        public void CaptureSelection_ExpandsSelectedFolderAndUsesOnlyPrefabAssets()
        {
            CreateValidPrefab("A.prefab");
            CreateValidPrefab("B.prefab");
            var texture = new Texture2D(1, 1);
            AssetDatabase.CreateAsset(texture, $"{_temporaryFolder}/Texture.asset");
            Selection.objects = new[] { AssetDatabase.LoadMainAssetAtPath(_temporaryFolder) };

            _window.CaptureSelection();
            _window.RunScan();

            Assert.That(_window.IssueCount, Is.Zero);
            Assert.That(_window.StatusText, Is.EqualTo("Scanned 2 Prefab(s). No missing references found."));
        }

        [Test]
        public void TryRemoveMissingScripts_OpensPrefabStageAndSupportsUndo()
        {
            var prefabPath = CopyBrokenPrefab();
            var issue = BuildGuardPrefabScanner.Scan(new[] { prefabPath }).Issues[0];
            try
            {
                var removed = BuildGuardPrefabScanWindow.TryRemoveMissingScripts(
                    issue,
                    false,
                    false,
                    out var removedCount);

                Assert.That(removed, Is.True);
                Assert.That(removedCount, Is.EqualTo(1));
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                Assert.That(stage, Is.Not.Null);
                Assert.That(stage.assetPath, Is.EqualTo(prefabPath));
                Assert.That(Selection.activeGameObject, Is.Not.Null);
                Assert.That(
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(stage.prefabContentsRoot),
                    Is.Zero);
                Assert.That(stage.scene.isDirty, Is.True);

                Undo.PerformUndo();
                Assert.That(
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(stage.prefabContentsRoot),
                    Is.EqualTo(1));
            }
            finally
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null)
                {
                    stage.ClearDirtiness();
                    StageUtility.GoToMainStage();
                }
            }
        }

        [Test]
        public void TryRemoveMissingScripts_MissingObjectReferenceDoesNothing()
        {
            var issue = new BuildGuardPrefabScanIssue(
                BuildGuardIssueKind.MissingObjectReference,
                "Assets/Unused.prefab",
                "Root[0]",
                "Component[1].field");

            var removed = BuildGuardPrefabScanWindow.TryRemoveMissingScripts(
                issue,
                false,
                false,
                out var removedCount);

            Assert.That(removed, Is.False);
            Assert.That(removedCount, Is.Zero);
            Assert.That(PrefabStageUtility.GetCurrentPrefabStage(), Is.Null);
        }

        private string CopyBrokenPrefab()
        {
            var sourcePath = AssetDatabase.GUIDToAssetPath(MissingScriptSceneScannerTests.BrokenPrefabFixtureGuid);
            Assert.That(sourcePath, Is.Not.Empty);
            var destinationPath = $"{_temporaryFolder}/Broken.prefab";
            File.WriteAllText(destinationPath, File.ReadAllText(sourcePath), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(
                destinationPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return destinationPath;
        }

        private string CreateValidPrefab(string fileName)
        {
            var instance = new GameObject(Path.GetFileNameWithoutExtension(fileName));
            var path = $"{_temporaryFolder}/{fileName}";
            try
            {
                Assert.That(PrefabUtility.SaveAsPrefabAsset(instance, path), Is.Not.Null);
                return path;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
