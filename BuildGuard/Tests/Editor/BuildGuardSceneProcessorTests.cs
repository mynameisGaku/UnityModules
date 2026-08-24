// SPDX-License-Identifier: MIT

using BuildGuard.Editor;
using NUnit.Framework;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace BuildGuard.Tests
{
    /// <summary>
    /// Verifies build callback scope and failure contracts.
    /// </summary>
    [Parallelizable(ParallelScope.None)]
    internal sealed class BuildGuardSceneProcessorTests
    {
        /// <summary>
        /// Verifies that non-build callbacks with a null report skip validation.
        /// </summary>
        [Test]
        public void OnProcessScene_NullReport_DoesNotValidate()
        {
            var fixture = new MissingScriptSceneScannerTests();
            fixture.SetUp();
            try
            {
                var scene = fixture.OpenSceneFixture();
                Assert.DoesNotThrow(() => new BuildGuardSceneProcessor().OnProcessScene(scene, null));
            }
            finally
            {
                fixture.TearDown();
            }
        }

        /// <summary>
        /// Verifies that explicit validation rejects missing scripts with actionable details.
        /// </summary>
        [Test]
        public void ValidateScene_BrokenScene_ThrowsBuildFailedException()
        {
            var fixture = new MissingScriptSceneScannerTests();
            fixture.SetUp();
            try
            {
                var scene = fixture.OpenSceneFixture();
                var exception = Assert.Throws<BuildFailedException>(() => BuildGuardSceneProcessor.ValidateScene(scene));
                Assert.That(exception.Message, Does.Contain("Broken\\/Root[0]").And.Contain("Inactive Child[0]").And.Contain("Missing Scripts: 3"));
            }
            finally
            {
                fixture.TearDown();
            }
        }

        /// <summary>
        /// Verifies that explicit validation accepts a valid Scene.
        /// </summary>
        [Test]
        public void ValidateScene_ValidScene_DoesNotThrow()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.DoesNotThrow(() => BuildGuardSceneProcessor.ValidateScene(scene));
        }

        /// <summary>
        /// Verifies that explicit validation rejects missing serialized object references.
        /// </summary>
        [Test]
        public void ValidateScene_MissingObjectReference_ThrowsBuildFailedException()
        {
            var fixture = new MissingObjectReferenceSceneScannerTests();
            fixture.SetUp();
            try
            {
                var scene = fixture.CreateSceneWithMissingCameraTargetTexture();
                var exception = Assert.Throws<BuildFailedException>(() => BuildGuardSceneProcessor.ValidateScene(scene));
                Assert.That(exception.Message, Does.Contain("Missing Object References: 1")
                    .And.Contain("Camera Root[0]")
                    .And.Contain("UnityEngine.Camera[1].m_TargetTexture"));
            }
            finally
            {
                fixture.TearDown();
            }
        }

        /// <summary>
        /// Verifies that preflight validation restores a closed Scene and preserves the active Scene.
        /// </summary>
        [Test]
        public void Preflight_ClosedBrokenScene_PreservesActiveScene()
        {
            var fixture = new MissingScriptSceneScannerTests();
            fixture.SetUp();
            try
            {
                var originalActiveScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var originalPath = $"{fixture.TemporaryFolder}/OriginalActive.unity";
                Assert.IsTrue(EditorSceneManager.SaveScene(originalActiveScene, originalPath));
                SceneManager.SetActiveScene(originalActiveScene);
                var brokenScene = fixture.OpenSceneFixture();
                var brokenPath = brokenScene.path;
                EditorSceneManager.CloseScene(brokenScene, true);

                Assert.Throws<BuildFailedException>(() => BuildGuardPreflightProcessor.ValidateScenePaths(new[] { brokenPath }));
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(originalActiveScene));
                Assert.That(SceneManager.GetSceneByPath(brokenPath).isLoaded, Is.False);
            }
            finally
            {
                fixture.TearDown();
            }
        }

        /// <summary>
        /// Verifies that duplicate Scene paths are scanned once and restored to a closed state.
        /// </summary>
        [Test]
        public void Preflight_DuplicateValidScenePath_LeavesSceneClosed()
        {
            var fixture = new MissingScriptSceneScannerTests();
            fixture.SetUp();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var scenePath = $"{fixture.TemporaryFolder}/ValidScene.unity";
                Assert.IsTrue(EditorSceneManager.SaveScene(scene, scenePath));
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                Assert.DoesNotThrow(() => BuildGuardPreflightProcessor.ValidateScenePaths(new[] { scenePath, scenePath }));
                Assert.That(SceneManager.GetSceneByPath(scenePath).isLoaded, Is.False);
            }
            finally
            {
                fixture.TearDown();
            }
        }

        /// <summary>
        /// Verifies the stable early callback order used before Scene conversion.
        /// </summary>
        [Test]
        public void CallbackOrder_IsEarlyAndStable()
        {
            Assert.That(new BuildGuardSceneProcessor().callbackOrder, Is.EqualTo(-10000));
            Assert.That(new BuildGuardPreflightProcessor().callbackOrder, Is.EqualTo(-10000));
        }
    }
}
