// SPDX-License-Identifier: MIT

using System;
using System.IO;
using BuildGuard.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BuildGuard.Tests
{
    /// <summary>
    /// Verifies the manual scan window state and Scene navigation contract.
    /// </summary>
    [Parallelizable(ParallelScope.None)]
    internal sealed class BuildGuardScanWindowTests
    {
        private string _temporaryFolder;
        private EditorBuildSettingsScene[] _originalScenes;
        private BuildGuardScanWindow _window;

        [SetUp]
        public void SetUp()
        {
            _temporaryFolder = $"Assets/__BuildGuardWindowTests_{Guid.NewGuid():N}";
            Assert.IsNotEmpty(AssetDatabase.CreateFolder("Assets", Path.GetFileName(_temporaryFolder)));
            _originalScenes = EditorBuildSettings.scenes;
            _window = ScriptableObject.CreateInstance<BuildGuardScanWindow>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null)
            {
                UnityEngine.Object.DestroyImmediate(_window);
            }

            EditorBuildSettings.scenes = _originalScenes;
            Selection.activeObject = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (AssetDatabase.IsValidFolder(_temporaryFolder))
            {
                Assert.IsTrue(AssetDatabase.DeleteAsset(_temporaryFolder));
            }
        }

        [Test]
        public void RunScan_ValidBuildScene_ShowsClearSuccessStatus()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Root");
            var scenePath = $"{_temporaryFolder}/Valid.unity";
            Assert.IsTrue(EditorSceneManager.SaveScene(scene, scenePath));
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };

            _window.RunScan();

            Assert.That(_window.IssueCount, Is.Zero);
            Assert.That(_window.StatusText, Is.EqualTo("Scanned 1 Scene(s). No missing references found."));
        }

        [Test]
        public void RunScan_NoEnabledScenes_ShowsConfigurationGuidance()
        {
            EditorBuildSettings.scenes = Array.Empty<EditorBuildSettingsScene>();

            _window.RunScan();

            Assert.That(_window.IssueCount, Is.Zero);
            Assert.That(_window.StatusText, Does.Contain("No enabled Scenes"));
        }

        [Test]
        public void ClearResults_AfterFinding_RemovesWindowState()
        {
            var fixture = new MissingScriptSceneScannerTests();
            fixture.SetUp();
            try
            {
                var scene = fixture.OpenSceneFixture();
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scene.path, true) };

                _window.RunScan();
                Assert.That(_window.IssueCount, Is.EqualTo(2));

                _window.ClearResults();

                Assert.That(_window.IssueCount, Is.Zero);
                Assert.That(_window.StatusText, Does.Contain("Results cleared"));
            }
            finally
            {
                fixture.TearDown();
            }
        }

        [Test]
        public void TryOpenIssue_LoadsSceneAndSelectsExactHierarchyObject()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var target = new GameObject("Target");
            target.transform.SetParent(root.transform, false);
            var scenePath = $"{_temporaryFolder}/Navigation.unity";
            Assert.IsTrue(EditorSceneManager.SaveScene(scene, scenePath));
            var hierarchyPath = BuildGuardHierarchyPath.Create(target.transform);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var issue = new BuildGuardScanIssue(
                BuildGuardIssueKind.MissingScript,
                scenePath,
                hierarchyPath,
                "Missing Scripts: 1");

            var opened = BuildGuardScanWindow.TryOpenIssue(issue, false);

            Assert.That(opened, Is.True);
            Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, Is.EqualTo(scenePath));
            Assert.That(Selection.activeGameObject, Is.Not.Null);
            Assert.That(BuildGuardHierarchyPath.Create(Selection.activeGameObject.transform), Is.EqualTo(hierarchyPath));
        }

        [Test]
        public void TryRemoveMissingScripts_OpensExactObjectAndLeavesSceneDirtyWithUndo()
        {
            var hostScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.That(EditorSceneManager.SaveScene(hostScene, $"{_temporaryFolder}/Host.unity"), Is.True);
            var fixture = new MissingScriptSceneScannerTests();
            fixture.SetUp();
            try
            {
                var scene = fixture.OpenSceneFixture();
                var finding = MissingScriptSceneScanner.Scan(scene)[0];
                var issue = new BuildGuardScanIssue(
                    BuildGuardIssueKind.MissingScript,
                    scene.path,
                    finding.HierarchyPath,
                    $"Missing Scripts: {finding.MissingScriptCount}");
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(hostScene);
                Assert.That(EditorSceneManager.CloseScene(scene, true), Is.True);

                var removed = BuildGuardScanWindow.TryRemoveMissingScripts(
                    issue,
                    false,
                    false,
                    out var removedCount);

                Assert.That(removed, Is.True);
                Assert.That(removedCount, Is.EqualTo(1));
                Assert.That(Selection.activeGameObject, Is.Not.Null);
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(Selection.activeGameObject), Is.Zero);
                Assert.That(Selection.activeGameObject.scene.isDirty, Is.True);

                Undo.PerformUndo();
                var restored = BuildGuardHierarchyPath.Find(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene(),
                    issue.HierarchyPath);
                Assert.That(restored, Is.Not.Null);
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(restored), Is.EqualTo(1));
            }
            finally
            {
                fixture.TearDown();
            }
        }

        [Test]
        public void TryRemoveMissingScripts_MissingObjectReferenceIssueDoesNothing()
        {
            var issue = new BuildGuardScanIssue(
                BuildGuardIssueKind.MissingObjectReference,
                "Assets/Missing.unity",
                "Root[0]",
                "Camera[0].m_TargetTexture");

            var removed = BuildGuardScanWindow.TryRemoveMissingScripts(
                issue,
                false,
                false,
                out var removedCount);

            Assert.That(removed, Is.False);
            Assert.That(removedCount, Is.Zero);
        }
    }
}
