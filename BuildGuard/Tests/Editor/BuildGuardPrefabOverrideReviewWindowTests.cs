// SPDX-License-Identifier: MIT

using System;
using System.Reflection;
using BuildGuard.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BuildGuard.Tests
{
    /// <summary>
    /// Verifies the separate review Window menu, snapshot state and stale-result guidance.
    /// </summary>
    [Parallelizable(ParallelScope.None)]
    internal sealed class BuildGuardPrefabOverrideReviewWindowTests
    {
        private BuildGuardPrefabOverrideTestFixture _fixture;
        private EditorBuildSettingsScene[] _originalBuildScenes;
        private BuildGuardPrefabOverrideReviewWindow _window;

        [SetUp]
        public void SetUp()
        {
            _fixture = new BuildGuardPrefabOverrideTestFixture();
            _fixture.SetUp();
            _originalBuildScenes = EditorBuildSettings.scenes;
            _window = ScriptableObject.CreateInstance<BuildGuardPrefabOverrideReviewWindow>();
            Selection.activeObject = null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null)
            {
                UnityEngine.Object.DestroyImmediate(_window);
            }

            EditorBuildSettings.scenes = _originalBuildScenes;
            Selection.activeObject = null;
            _fixture?.TearDown();
        }

        [Test]
        public void Menu_UsesDedicatedReviewPathAndPriority()
        {
            var method = typeof(BuildGuardPrefabOverrideReviewWindow).GetMethod(
                "ShowWindow",
                BindingFlags.Static | BindingFlags.NonPublic);
            var attribute = method?.GetCustomAttribute<MenuItem>();

            Assert.That(BuildGuardPrefabOverrideReviewWindow.MenuPath, Is.EqualTo(
                "Tools/Build Guard/Review Prefab Overrides"));
            Assert.That(attribute, Is.Not.Null);
            Assert.That(attribute.menuItem, Is.EqualTo(BuildGuardPrefabOverrideReviewWindow.MenuPath));
            Assert.That(attribute.priority, Is.EqualTo(2002));
            Assert.That(BuildGuardPrefabOverrideReviewWindow.MaximumDisplayedFindings, Is.EqualTo(1000));
        }

        [Test]
        public void RunScan_NoEnabledScenes_ShowsConfigurationGuidance()
        {
            EditorBuildSettings.scenes = Array.Empty<EditorBuildSettingsScene>();

            _window.RunScan();

            Assert.That(_window.FindingCount, Is.Zero);
            Assert.That(_window.FailureCount, Is.Zero);
            Assert.That(_window.StatusText, Does.Contain("No enabled Scenes"));
        }

        [Test]
        public void RunScan_EnabledScene_CapturesReviewOnlySnapshot()
        {
            var prefabPath = _fixture.CreatePrefab("WindowReview.prefab");
            var scene = _fixture.CreateSavedScene("WindowReview.unity");
            _fixture.InstantiatePrefab(prefabPath, scene).AddComponent<BoxCollider>();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scene.path, true) };

            _window.RunScan();

            Assert.That(_window.FindingCount, Is.EqualTo(1));
            Assert.That(_window.FailureCount, Is.Zero);
            Assert.That(_window.StatusText, Does.Contain("Found 1 structural Prefab override"));
            Assert.That(
                _window.GetFinding(0).Kind,
                Is.EqualTo(BuildGuardPrefabOverrideKind.AddedComponent));
        }

        [Test]
        public void RunScan_CancelledAfterFirstScene_DiscardsPartialWindowState()
        {
            var prefabPath = _fixture.CreatePrefab("WindowCancel.prefab");
            var firstScene = _fixture.CreateSavedScene("WindowFirst.unity");
            _fixture.InstantiatePrefab(prefabPath, firstScene).AddComponent<BoxCollider>();
            var secondScene = _fixture.CreateSavedScene("WindowSecond.unity");
            _fixture.InstantiatePrefab(prefabPath, secondScene).AddComponent<SphereCollider>();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(firstScene.path, true),
                new EditorBuildSettingsScene(secondScene.path, true),
            };

            _window.RunScan((index, _, _) => index == 1);

            Assert.That(_window.FindingCount, Is.Zero);
            Assert.That(_window.FailureCount, Is.Zero);
            Assert.That(_window.StatusText, Does.Contain("Partial findings were discarded"));
        }

        [Test]
        public void LocateFinding_ChangedSnapshot_ShowsStaleGuidanceWithoutRemovingSnapshot()
        {
            var prefabPath = _fixture.CreatePrefab("WindowStale.prefab");
            var scene = _fixture.CreateSavedScene("WindowStale.unity");
            var instance = _fixture.InstantiatePrefab(prefabPath, scene);
            var addedComponent = instance.AddComponent<BoxCollider>();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scene.path, true) };
            _window.RunScan();
            Assert.That(_window.FindingCount, Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(addedComponent);
            var wasDirty = scene.isDirty;

            var outcome = _window.LocateFinding(0);

            Assert.That(outcome, Is.EqualTo(BuildGuardPrefabOverrideNavigationOutcome.Stale));
            Assert.That(_window.FindingCount, Is.EqualTo(1));
            Assert.That(_window.StatusText, Does.Contain("stale"));
            Assert.That(scene.isDirty, Is.EqualTo(wasDirty));
        }

        [Test]
        public void ClearResults_AfterSnapshot_RemovesWindowStateOnly()
        {
            var prefabPath = _fixture.CreatePrefab("WindowClear.prefab");
            var scene = _fixture.CreateSavedScene("WindowClear.unity");
            _fixture.InstantiatePrefab(prefabPath, scene).AddComponent<BoxCollider>();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scene.path, true) };
            _window.RunScan();
            var wasDirty = scene.isDirty;

            _window.ClearResults();

            Assert.That(_window.FindingCount, Is.Zero);
            Assert.That(_window.FailureCount, Is.Zero);
            Assert.That(_window.StatusText, Does.Contain("Results cleared"));
            Assert.That(scene.isDirty, Is.EqualTo(wasDirty));
        }
    }
}
