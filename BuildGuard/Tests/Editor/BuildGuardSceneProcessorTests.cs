// SPDX-License-Identifier: MIT

using BuildGuard.Editor;
using NUnit.Framework;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace BuildGuard.Tests
{
    /// <summary>
    /// ビルド処理の対象範囲と失敗時の契約を検証します。
    /// </summary>
    [Parallelizable(ParallelScope.None)]
    internal sealed class BuildGuardSceneProcessorTests
    {
        /// <summary>
        /// ビルド外で報告情報がない呼び出しは検証しないことを確認します。
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
        /// 明示検証が欠落スクリプトを操作可能な詳細付きで拒否することを確認します。
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
                Assert.That(exception.Message, Is.EqualTo(
                    "プレイヤービルド対象のシーンで、ビルドを停止する問題が見つかりました。\n" +
                    $"シーン: {scene.path}\n" +
                    "欠落スクリプト: 3\n" +
                    "- Broken\\/Root[0]: 1\n" +
                    "- Broken\\/Root[0]/Inactive Child[0]: 2\n" +
                    "再度ビルドする前に、一覧の欠落スクリプトまたはオブジェクト参照を修復するか、該当箇所を削除してください。"));
            }
            finally
            {
                fixture.TearDown();
            }
        }

        /// <summary>
        /// 明示検証が有効なシーンを受け入れることを確認します。
        /// </summary>
        [Test]
        public void ValidateScene_ValidScene_DoesNotThrow()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.DoesNotThrow(() => BuildGuardSceneProcessor.ValidateScene(scene));
        }

        /// <summary>
        /// 明示検証が直列化された欠落オブジェクト参照を拒否することを確認します。
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
                Assert.That(exception.Message, Is.EqualTo(
                    "プレイヤービルド対象のシーンで、ビルドを停止する問題が見つかりました。\n" +
                    $"シーン: {scene.path}\n" +
                    "欠落オブジェクト参照: 1\n" +
                    "- Camera Root[0] :: UnityEngine.Camera[1].m_TargetTexture\n" +
                    "再度ビルドする前に、一覧の欠落スクリプトまたはオブジェクト参照を修復するか、該当箇所を削除してください。"));
            }
            finally
            {
                fixture.TearDown();
            }
        }

        /// <summary>
        /// 事前検証が閉じていたシーンを閉じた状態へ戻し、作業中シーンを維持することを確認します。
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
        /// 重複したシーンパスを1回だけ走査し、閉じた状態へ戻すことを確認します。
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
        /// シーン変換前に使う早期処理の順序が固定されていることを確認します。
        /// </summary>
        [Test]
        public void CallbackOrder_IsEarlyAndStable()
        {
            Assert.That(new BuildGuardSceneProcessor().callbackOrder, Is.EqualTo(-10000));
            Assert.That(new BuildGuardPreflightProcessor().callbackOrder, Is.EqualTo(-10000));
        }
    }
}
