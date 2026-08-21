// SPDX-License-Identifier: MIT

using System;
using BuildGuard.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace BuildGuard.Tests
{
    /// <summary>
    /// Verifies manual scanning across multiple closed build Scenes.
    /// </summary>
    [Parallelizable(ParallelScope.None)]
    internal sealed class BuildGuardManualScannerTests
    {
        [Test]
        public void Scan_MultipleClosedScenes_ReturnsAllIssuesAndPreservesActiveScene()
        {
            var scriptFixture = new MissingScriptSceneScannerTests();
            var objectFixture = new MissingObjectReferenceSceneScannerTests();
            scriptFixture.SetUp();
            objectFixture.SetUp();
            try
            {
                var brokenScriptScene = scriptFixture.OpenSceneFixture();
                var brokenScriptPath = brokenScriptScene.path;
                EditorSceneManager.CloseScene(brokenScriptScene, true);

                var brokenObjectScene = objectFixture.CreateSceneWithMissingCameraTargetTexture();
                var brokenObjectPath = brokenObjectScene.path;
                EditorSceneManager.CloseScene(brokenObjectScene, true);

                var activeScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                SceneManager.SetActiveScene(activeScene);

                var result = BuildGuardManualScanner.Scan(new[]
                {
                    brokenScriptPath,
                    brokenObjectPath,
                    brokenScriptPath
                });

                Assert.That(result.Cancelled, Is.False);
                Assert.That(result.ScannedSceneCount, Is.EqualTo(2));
                Assert.That(result.Issues.Count, Is.EqualTo(3));
                Assert.That(result.Issues[0].Kind, Is.EqualTo(BuildGuardIssueKind.MissingScript));
                Assert.That(result.Issues[1].Kind, Is.EqualTo(BuildGuardIssueKind.MissingScript));
                Assert.That(result.Issues[2].Kind, Is.EqualTo(BuildGuardIssueKind.MissingObjectReference));
                Assert.That(result.Issues[2].Details, Is.EqualTo("UnityEngine.Camera[1].m_TargetTexture"));
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(activeScene));
                Assert.That(SceneManager.GetSceneByPath(brokenScriptPath).isLoaded, Is.False);
                Assert.That(SceneManager.GetSceneByPath(brokenObjectPath).isLoaded, Is.False);
            }
            finally
            {
                objectFixture.TearDown();
                scriptFixture.TearDown();
            }
        }

        [Test]
        public void Scan_CancelBeforeSecondScene_ReturnsPartialResult()
        {
            var fixture = new MissingScriptSceneScannerTests();
            fixture.SetUp();
            try
            {
                var brokenScene = fixture.OpenSceneFixture();
                var brokenPath = brokenScene.path;
                EditorSceneManager.CloseScene(brokenScene, true);
                var validScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var validPath = $"{fixture.TemporaryFolder}/Valid.unity";
                Assert.IsTrue(EditorSceneManager.SaveScene(validScene, validPath));

                var result = BuildGuardManualScanner.Scan(
                    new[] { brokenPath, validPath },
                    (index, _, _) => index == 1);

                Assert.That(result.Cancelled, Is.True);
                Assert.That(result.ScannedSceneCount, Is.EqualTo(1));
                Assert.That(result.Issues.Count, Is.EqualTo(2));
            }
            finally
            {
                fixture.TearDown();
            }
        }

        [Test]
        public void GetEnabledBuildScenePaths_FiltersDisabledAndEmptyEntries()
        {
            var originalScenes = EditorBuildSettings.scenes;
            try
            {
                EditorBuildSettings.scenes = new[]
                {
                    new EditorBuildSettingsScene("Assets/Enabled.unity", true),
                    new EditorBuildSettingsScene("Assets/Disabled.unity", false),
                    new EditorBuildSettingsScene(string.Empty, true)
                };

                var paths = BuildGuardManualScanner.GetEnabledBuildScenePaths();

                Assert.That(paths, Is.EqualTo(new[] { "Assets/Enabled.unity" }));
            }
            finally
            {
                EditorBuildSettings.scenes = originalScenes;
            }
        }
    }
}
