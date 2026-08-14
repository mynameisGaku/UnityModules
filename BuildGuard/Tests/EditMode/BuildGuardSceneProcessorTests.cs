// SPDX-License-Identifier: MIT

using BuildGuard.Editor;
using NUnit.Framework;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace BuildGuard.Tests
{
    /// <summary>
    /// build callbackの対象範囲と失敗契約を検証します。
    /// </summary>
    [Parallelizable(ParallelScope.None)]
    internal sealed class BuildGuardSceneProcessorTests
    {
        /// <summary>
        /// PlayMode相当のnull reportではMissing Script Sceneを拒否しないことを検証します。
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
        /// Missing Script Sceneの明示検査が詳細message付きで失敗することを検証します。
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
                Assert.That(exception.Message, Does.Contain("Broken\\/Root[0]").And.Contain("Inactive Child[0]").And.Contain("合計: 3"));
            }
            finally
            {
                fixture.TearDown();
            }
        }

        /// <summary>
        /// Missing ScriptのないSceneを明示検査しても失敗しないことを検証します。
        /// </summary>
        [Test]
        public void ValidateScene_ValidScene_DoesNotThrow()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.DoesNotThrow(() => BuildGuardSceneProcessor.ValidateScene(scene));
        }

        /// <summary>
        /// 閉じたbuild対象Sceneを検査し、元のactive Sceneを維持したまま失敗することを検証します。
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
        /// 同じScene pathが重複しても一度だけ検査し、閉じた状態へ戻すことを検証します。
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
        /// Scene変換より早い固定callback順序を公開することを検証します。
        /// </summary>
        [Test]
        public void CallbackOrder_IsEarlyAndStable()
        {
            Assert.That(new BuildGuardSceneProcessor().callbackOrder, Is.EqualTo(-10000));
            Assert.That(new BuildGuardPreflightProcessor().callbackOrder, Is.EqualTo(-10000));
        }
    }
}
