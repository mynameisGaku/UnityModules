// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using BuildGuard.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BuildGuard.Tests
{
    /// <summary>
    /// 複数の閉じたシーンを対象とする手動検査の動作を確認します。
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
                Assert.That(result.Issues[0].Details, Is.EqualTo("欠落スクリプト: 1"));
                Assert.That(result.Issues[1].Details, Is.EqualTo("欠落スクリプト: 2"));
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

        [Test]
        public void TryResolveSelectedScenePaths_MixedSelection_ReturnsDirectSceneAssetsInOrdinalOrder()
        {
            var selectedAssetGuids = new[]
            {
                "scene-zeta",
                "folder",
                "package-scene",
                "text",
                "scene-zeta-duplicate",
                "scene-alpha",
                "not-scene-asset"
            };
            var pathsByGuid = new Dictionary<string, string>
            {
                { "scene-zeta", "Assets/Zeta.unity" },
                { "folder", "Assets/Folder" },
                { "package-scene", "Packages/com.example/Package.unity" },
                { "text", "Assets/Readme.txt" },
                { "scene-zeta-duplicate", "Assets/Zeta.unity" },
                { "scene-alpha", "Assets/Alpha.unity" },
                { "not-scene-asset", "Assets/LooksLikeScene.unity" }
            };

            var succeeded = BuildGuardManualScanner.TryResolveSelectedScenePaths(
                selectedAssetGuids,
                guid => pathsByGuid[guid],
                path => path != "Assets/LooksLikeScene.unity",
                out var scenePaths,
                out var errorMessage);

            Assert.That(succeeded, Is.True, errorMessage);
            Assert.That(errorMessage, Is.Empty);
            Assert.That(scenePaths, Is.EqualTo(new[]
            {
                "Assets/Alpha.unity",
                "Assets/Zeta.unity"
            }));
        }

        [Test]
        public void TryResolveSelectedScenePaths_MissingSource_ReturnsExactJapaneseError()
        {
            var succeeded = BuildGuardManualScanner.TryResolveSelectedScenePaths(
                null,
                _ => string.Empty,
                _ => false,
                out var scenePaths,
                out var errorMessage);

            Assert.That(succeeded, Is.False);
            Assert.That(scenePaths, Is.Empty);
            Assert.That(errorMessage, Is.EqualTo("選択シーンの取得元を利用できません。"));
        }

        [Test]
        public void TryResolveSelectedScenePaths_AssetCandidateLimit_AcceptsExactAndRejectsOneOver()
        {
            var exactLimit = new string[BuildGuardManualScanner.MaximumSelectedAssetCandidates];
            var overLimit = new string[BuildGuardManualScanner.MaximumSelectedAssetCandidates + 1];

            var exactSucceeded = BuildGuardManualScanner.TryResolveSelectedScenePaths(
                exactLimit,
                guid => throw new AssertionException($"空の識別子は無視される必要があります: {guid}"),
                _ => throw new AssertionException("シーン判定は呼び出されない必要があります。"),
                out var exactPaths,
                out var exactError);
            var overSucceeded = BuildGuardManualScanner.TryResolveSelectedScenePaths(
                overLimit,
                _ => string.Empty,
                _ => false,
                out var overPaths,
                out var overError);

            Assert.That(exactSucceeded, Is.True, exactError);
            Assert.That(exactPaths, Is.Empty);
            Assert.That(exactError, Is.Empty);
            Assert.That(overSucceeded, Is.False);
            Assert.That(overPaths, Is.Empty);
            Assert.That(overError, Is.EqualTo(
                $"選択中のアセットが多すぎます。選択できるアセットは最大{BuildGuardManualScanner.MaximumSelectedAssetCandidates}件です。"));
        }

        [Test]
        public void TryResolveSelectedScenePaths_SceneLimit_AcceptsExactAndRejectsOneOver()
        {
            var exactLimit = CreateSceneGuids(BuildGuardManualScanner.MaximumSelectedScenes);
            var overLimit = CreateSceneGuids(BuildGuardManualScanner.MaximumSelectedScenes + 1);

            var exactSucceeded = BuildGuardManualScanner.TryResolveSelectedScenePaths(
                exactLimit,
                guid => $"Assets/{guid}.unity",
                _ => true,
                out var exactPaths,
                out var exactError);
            var overSucceeded = BuildGuardManualScanner.TryResolveSelectedScenePaths(
                overLimit,
                guid => $"Assets/{guid}.unity",
                _ => true,
                out var overPaths,
                out var overError);

            Assert.That(exactSucceeded, Is.True, exactError);
            Assert.That(exactPaths.Count, Is.EqualTo(BuildGuardManualScanner.MaximumSelectedScenes));
            Assert.That(exactError, Is.Empty);
            Assert.That(overSucceeded, Is.False);
            Assert.That(overPaths, Is.Empty);
            Assert.That(overError, Is.EqualTo(
                $"選択中のシーンが多すぎます。選択できるシーンアセットは最大{BuildGuardManualScanner.MaximumSelectedScenes}件です。"));
        }

        [Test]
        public void TryGetSelectedScenePaths_SelectionProviderThrows_HidesExceptionDetail()
        {
            LogAssert.Expect(LogType.Exception, new Regex("INTERNAL_ENGLISH_DETAIL"));

            var succeeded = BuildGuardManualScanner.TryGetSelectedScenePaths(
                () => throw new InvalidOperationException("INTERNAL_ENGLISH_DETAIL"),
                out var scenePaths,
                out var errorMessage);

            Assert.That(succeeded, Is.False);
            Assert.That(scenePaths, Is.Empty);
            Assert.That(errorMessage, Is.EqualTo(
                "選択シーンを取得できませんでした。Unityのログで原因を確認し、もう一度「現在の選択を使用」を押してください。"));
            Assert.That(errorMessage, Does.Not.Contain("INTERNAL_ENGLISH_DETAIL"));
        }

        [Test]
        public void TryResolveSelectedScenePaths_ResolverThrows_ReturnsFailureWithoutPaths()
        {
            LogAssert.Expect(LogType.Exception, new Regex("INTERNAL_ENGLISH_DETAIL"));

            var succeeded = BuildGuardManualScanner.TryResolveSelectedScenePaths(
                new[] { "scene" },
                _ => throw new InvalidOperationException("INTERNAL_ENGLISH_DETAIL"),
                _ => true,
                out var scenePaths,
                out var errorMessage);

            Assert.That(succeeded, Is.False);
            Assert.That(scenePaths, Is.Empty);
            Assert.That(errorMessage, Is.EqualTo(
                "選択シーンを取得できませんでした。Unityのログで原因を確認し、もう一度「現在の選択を使用」を押してください。"));
            Assert.That(errorMessage, Does.Not.Contain("INTERNAL_ENGLISH_DETAIL"));
        }

        [Test]
        public void TryResolveSelectedScenePaths_PredicateThrows_ReturnsFailureWithoutPaths()
        {
            LogAssert.Expect(LogType.Exception, new Regex("INTERNAL_ENGLISH_DETAIL"));

            var succeeded = BuildGuardManualScanner.TryResolveSelectedScenePaths(
                new[] { "scene" },
                _ => "Assets/Scene.unity",
                _ => throw new InvalidOperationException("INTERNAL_ENGLISH_DETAIL"),
                out var scenePaths,
                out var errorMessage);

            Assert.That(succeeded, Is.False);
            Assert.That(scenePaths, Is.Empty);
            Assert.That(errorMessage, Is.EqualTo(
                "選択シーンを取得できませんでした。Unityのログで原因を確認し、もう一度「現在の選択を使用」を押してください。"));
            Assert.That(errorMessage, Does.Not.Contain("INTERNAL_ENGLISH_DETAIL"));
        }

        [Test]
        public void TryScanSelectedScenes_NullEmptyAndOverLimitSnapshots_ReturnFailureWithoutIssues()
        {
            AssertSelectedScanRejected(
                null,
                "プロジェクトウィンドウでシーンアセットを1件以上選択してください。");
            AssertSelectedScanRejected(
                Array.Empty<string>(),
                "プロジェクトウィンドウでシーンアセットを1件以上選択してください。");
            AssertSelectedScanRejected(
                new string[BuildGuardManualScanner.MaximumSelectedScenes + 1],
                $"選択中のシーンが多すぎます。選択できるシーンアセットは最大{BuildGuardManualScanner.MaximumSelectedScenes}件です。");
        }

        [Test]
        public void TryScanSelectedScenes_SnapshotAccessThrows_ReturnsExactJapaneseError()
        {
            LogAssert.Expect(LogType.Exception, new Regex("INTERNAL_ENGLISH_DETAIL"));

            var succeeded = BuildGuardManualScanner.TryScanSelectedScenes(
                new ThrowingScenePathList(),
                null,
                out var result,
                out var errorMessage);

            Assert.That(succeeded, Is.False);
            Assert.That(result.ScannedSceneCount, Is.Zero);
            Assert.That(result.Issues, Is.Empty);
            Assert.That(errorMessage, Is.EqualTo(
                "選択シーンを検査できませんでした。Unityのログで原因を確認してください。"));
            Assert.That(errorMessage, Does.Not.Contain("INTERNAL_ENGLISH_DETAIL"));
        }

        [Test]
        public void TryScanSelectedScenes_FailureAfterFirstScene_DiscardsIssuesAndRestoresSceneState()
        {
            LogAssert.Expect(LogType.Exception, new Regex("INTERNAL_ENGLISH_DETAIL"));

            var fixture = new MissingScriptSceneScannerTests();
            fixture.SetUp();
            try
            {
                var brokenScene = fixture.OpenSceneFixture();
                var brokenPath = brokenScene.path;
                Assert.That(EditorSceneManager.CloseScene(brokenScene, true), Is.True);
                var validScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var validPath = $"{fixture.TemporaryFolder}/Z_Valid.unity";
                Assert.That(EditorSceneManager.SaveScene(validScene, validPath), Is.True);
                var activeScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(activeScene));

                var succeeded = BuildGuardManualScanner.TryScanSelectedScenes(
                    new[] { brokenPath, validPath },
                    (index, _, _) => index == 1
                        ? throw new InvalidOperationException("INTERNAL_ENGLISH_DETAIL")
                        : false,
                    out var result,
                    out var errorMessage);

                Assert.That(succeeded, Is.False);
                Assert.That(result.Cancelled, Is.False);
                Assert.That(result.ScannedSceneCount, Is.Zero);
                Assert.That(result.Issues, Is.Empty);
                Assert.That(errorMessage, Is.EqualTo(
                    "選択シーンを検査できませんでした。Unityのログで原因を確認してください。"));
                Assert.That(errorMessage, Does.Not.Contain("INTERNAL_ENGLISH_DETAIL"));
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(activeScene));
                Assert.That(SceneManager.GetSceneByPath(brokenPath).isLoaded, Is.False);
                Assert.That(SceneManager.GetSceneByPath(validPath).isLoaded, Is.False);
            }
            finally
            {
                fixture.TearDown();
            }
        }

        [Test]
        public void TryScanSelectedScenes_DeletedCapturedScene_ReturnsStaleFailureWithoutIssues()
        {
            var fixture = new MissingScriptSceneScannerTests();
            fixture.SetUp();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var scenePath = $"{fixture.TemporaryFolder}/Captured.unity";
                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Assert.That(AssetDatabase.DeleteAsset(scenePath), Is.True);

                var succeeded = BuildGuardManualScanner.TryScanSelectedScenes(
                    new[] { scenePath },
                    null,
                    out var result,
                    out var errorMessage);

                Assert.That(succeeded, Is.False);
                Assert.That(result.ScannedSceneCount, Is.Zero);
                Assert.That(result.Issues, Is.Empty);
                Assert.That(errorMessage, Is.EqualTo(
                    "選択シーンの状態が変わりました。「現在の選択を使用」を押してから、もう一度検査してください。"));
            }
            finally
            {
                fixture.TearDown();
            }
        }

        [Test]
        public void TryScanSelectedScenes_CancelAfterDeletingNextScene_DiscardsPartialResult()
        {
            var fixture = new MissingScriptSceneScannerTests();
            fixture.SetUp();
            try
            {
                var brokenScene = fixture.OpenSceneFixture();
                var brokenPath = brokenScene.path;
                Assert.That(EditorSceneManager.CloseScene(brokenScene, true), Is.True);
                var disposableScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var disposablePath = $"{fixture.TemporaryFolder}/Z_Disposable.unity";
                Assert.That(EditorSceneManager.SaveScene(disposableScene, disposablePath), Is.True);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var visitedDeletionPoint = false;
                var deletedDisposableScene = false;

                var succeeded = BuildGuardManualScanner.TryScanSelectedScenes(
                    new[] { brokenPath, disposablePath },
                    (index, _, scenePath) =>
                    {
                        if (index == 1)
                        {
                            visitedDeletionPoint = scenePath == disposablePath;
                            deletedDisposableScene = AssetDatabase.DeleteAsset(scenePath);
                        }

                        return index == 1;
                    },
                    out var result,
                    out var errorMessage);

                Assert.That(visitedDeletionPoint, Is.True);
                Assert.That(deletedDisposableScene, Is.True);
                Assert.That(succeeded, Is.False);
                Assert.That(result.ScannedSceneCount, Is.Zero);
                Assert.That(result.Issues, Is.Empty);
                Assert.That(errorMessage, Is.EqualTo(
                    "選択シーンの状態が変わりました。「現在の選択を使用」を押してから、もう一度検査してください。"));
            }
            finally
            {
                fixture.TearDown();
            }
        }

        [Test]
        public void TryScanSelectedScenes_CancelAfterFirstScene_ReturnsRetainedPartialResult()
        {
            var fixture = new MissingScriptSceneScannerTests();
            fixture.SetUp();
            try
            {
                var brokenScene = fixture.OpenSceneFixture();
                var brokenPath = brokenScene.path;
                Assert.That(EditorSceneManager.CloseScene(brokenScene, true), Is.True);
                var validScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var validPath = $"{fixture.TemporaryFolder}/Z_Valid.unity";
                Assert.That(EditorSceneManager.SaveScene(validScene, validPath), Is.True);

                var succeeded = BuildGuardManualScanner.TryScanSelectedScenes(
                    new[] { brokenPath, validPath },
                    (index, _, _) => index == 1,
                    out var result,
                    out var errorMessage);

                Assert.That(succeeded, Is.True, errorMessage);
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
        public void TryScanSelectedScenes_BuildDisabledScene_IsStillScannedAndClosedAgain()
        {
            var originalScenes = EditorBuildSettings.scenes;
            var fixture = new MissingScriptSceneScannerTests();
            fixture.SetUp();
            try
            {
                var brokenScene = fixture.OpenSceneFixture();
                var brokenPath = brokenScene.path;
                Assert.That(EditorSceneManager.CloseScene(brokenScene, true), Is.True);
                var activeScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorBuildSettings.scenes = new[]
                {
                    new EditorBuildSettingsScene(brokenPath, false)
                };

                var succeeded = BuildGuardManualScanner.TryScanSelectedScenes(
                    new[] { brokenPath },
                    null,
                    out var result,
                    out var errorMessage);

                Assert.That(succeeded, Is.True, errorMessage);
                Assert.That(result.Cancelled, Is.False);
                Assert.That(result.ScannedSceneCount, Is.EqualTo(1));
                Assert.That(result.Issues.Count, Is.EqualTo(2));
                Assert.That(SceneManager.GetSceneByPath(brokenPath).isLoaded, Is.False);
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(activeScene));
            }
            finally
            {
                EditorBuildSettings.scenes = originalScenes;
                fixture.TearDown();
            }
        }

        [Test]
        public void TryScanSelectedScenes_LoadedDirtyScene_UsesMemoryStateAndPreservesScene()
        {
            var fixture = new MissingScriptSceneScannerTests();
            fixture.SetUp();
            try
            {
                var scene = fixture.OpenSceneFixture();
                var scenePath = scene.path;
                var originalFileBytes = File.ReadAllBytes(scenePath);
                var removedCount = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        removedCount += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
                    }
                }

                Assert.That(removedCount, Is.EqualTo(3));
                Assert.That(EditorSceneManager.MarkSceneDirty(scene), Is.True);
                Assert.That(SceneManager.SetActiveScene(scene), Is.True);
                var originalHandle = scene.handle;

                var succeeded = BuildGuardManualScanner.TryScanSelectedScenes(
                    new[] { scenePath },
                    null,
                    out var result,
                    out var errorMessage);

                Assert.That(succeeded, Is.True, errorMessage);
                Assert.That(result.Cancelled, Is.False);
                Assert.That(result.ScannedSceneCount, Is.EqualTo(1));
                Assert.That(result.Issues, Is.Empty);
                Assert.That(scene.handle, Is.EqualTo(originalHandle));
                Assert.That(scene.isLoaded, Is.True);
                Assert.That(scene.isDirty, Is.True);
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene));
                Assert.That(File.ReadAllBytes(scenePath), Is.EqualTo(originalFileBytes));
            }
            finally
            {
                fixture.TearDown();
            }
        }

        private static string[] CreateSceneGuids(int count)
        {
            var guids = new string[count];
            for (var index = 0; index < count; index++)
            {
                guids[index] = $"Scene{index:D4}";
            }

            return guids;
        }

        private static void AssertSelectedScanRejected(
            IReadOnlyList<string> scenePaths,
            string expectedErrorText)
        {
            var succeeded = BuildGuardManualScanner.TryScanSelectedScenes(
                scenePaths,
                null,
                out var result,
                out var errorMessage);

            Assert.That(succeeded, Is.False);
            Assert.That(result.ScannedSceneCount, Is.Zero);
            Assert.That(result.Issues, Is.Empty);
            Assert.That(errorMessage, Is.EqualTo(expectedErrorText));
        }

        /// <summary>シーン一覧の読み取り失敗を再現する試験用一覧です。</summary>
        private sealed class ThrowingScenePathList : IReadOnlyList<string>
        {
            /// <summary>読み取り処理を添字アクセスまで進めるため、1件を返します。</summary>
            public int Count => 1;

            /// <summary>シーンパスの読み取り失敗を発生させます。</summary>
            public string this[int index] => throw new InvalidOperationException("INTERNAL_ENGLISH_DETAIL");

            /// <summary>この試験では列挙を使用しないため、未対応として通知します。</summary>
            public IEnumerator<string> GetEnumerator()
            {
                throw new NotSupportedException();
            }

            /// <summary>この試験では列挙を使用しないため、型付き列挙処理へ転送します。</summary>
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
