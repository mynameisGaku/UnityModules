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
    }
}
