// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Reflection;
using BuildGuard.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BuildGuard.Tests
{
    /// <summary>
    /// 手動検査画面の状態とシーン移動の契約を検証します。
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
            Selection.objects = Array.Empty<UnityEngine.Object>();
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
            Assert.That(_window.StatusText, Is.EqualTo(
                "シーンを1件検査しました。欠落参照は見つかりませんでした。"));
        }

        [Test]
        public void RunScan_NoEnabledScenes_ShowsConfigurationGuidance()
        {
            EditorBuildSettings.scenes = Array.Empty<EditorBuildSettingsScene>();

            _window.RunScan();

            Assert.That(_window.IssueCount, Is.Zero);
            Assert.That(_window.StatusText, Is.EqualTo(
                "現在のビルドプロファイルに、有効なシーンが設定されていません。"));
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
                Assert.That(_window.StatusText, Is.EqualTo(
                    "シーンを1件検査し、2件の問題を見つけました。"));

                _window.ClearResults();

                Assert.That(_window.IssueCount, Is.Zero);
                Assert.That(_window.StatusText, Is.EqualTo(
                    "結果を消去しました。ビルド対象シーン、または記録済みの選択シーンを再度検査してください。"));
            }
            finally
            {
                fixture.TearDown();
            }
        }

        [Test]
        public void CaptureSelectedScenes_NoDirectSceneSelection_ShowsSelectionGuidance()
        {
            Selection.objects = new UnityEngine.Object[]
            {
                AssetDatabase.LoadAssetAtPath<DefaultAsset>(_temporaryFolder)
            };

            _window.CaptureSelectedScenes();

            Assert.That(_window.SelectedSceneCount, Is.Zero);
            Assert.That(_window.IssueCount, Is.Zero);
            Assert.That(_window.StatusText, Is.EqualTo(
                "プロジェクトウィンドウでシーンアセットを1件以上選択してください。フォルダーとシーン以外のアセットは無視されます。"));
        }

        [Test]
        public void CaptureSelectedScenes_TwoSceneAssets_CapturesStableSnapshot()
        {
            var zetaPath = CreateSavedScene("Zeta.unity");
            var alphaPath = CreateSavedScene("Alpha.unity");
            SelectAssets(zetaPath, alphaPath);

            _window.CaptureSelectedScenes();

            Assert.That(_window.SelectedSceneCount, Is.EqualTo(2));
            Assert.That(_window.IssueCount, Is.Zero);
            Assert.That(_window.StatusText, Is.EqualTo(
                "選択中のシーンアセットを2件記録しました。"));
        }

        [Test]
        public void RunSelectedScan_ValidCapturedScene_ShowsSelectedSuccessStatus()
        {
            var scenePath = CreateSavedScene("Selected.unity");
            SelectAssets(scenePath);
            _window.CaptureSelectedScenes();

            _window.RunSelectedScan();

            Assert.That(_window.IssueCount, Is.Zero);
            Assert.That(_window.StatusText, Is.EqualTo(
                "選択シーンを1件検査しました。欠落参照は見つかりませんでした。"));
        }

        [Test]
        public void RunSelectedScan_CancelAfterFirstScene_ShowsSelectedCancellationStatus()
        {
            var alphaPath = CreateSavedScene("Alpha.unity");
            var zetaPath = CreateSavedScene("Zeta.unity");
            SelectAssets(zetaPath, alphaPath);
            _window.CaptureSelectedScenes();

            _window.RunSelectedScan((index, _, _) => index == 1);

            Assert.That(_window.IssueCount, Is.Zero);
            Assert.That(_window.StatusText, Is.EqualTo(
                "選択シーンを1件検査した時点で中止しました。0件の問題を保持しています。"));
        }

        [Test]
        public void RunSelectedScan_CapturedSceneDeleted_ShowsStaleFailureWithoutIssues()
        {
            var scenePath = CreateSavedScene("Stale.unity");
            SelectAssets(scenePath);
            _window.CaptureSelectedScenes();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.That(AssetDatabase.DeleteAsset(scenePath), Is.True);

            _window.RunSelectedScan();

            Assert.That(_window.IssueCount, Is.Zero);
            Assert.That(_window.StatusText, Is.EqualTo(
                "選択シーンの状態が変わりました。「現在の選択を使用」を押してから、もう一度検査してください。"));
        }

        [Test]
        public void ClearResults_AfterSelectedCapture_PreservesCapturedSnapshot()
        {
            var scenePath = CreateSavedScene("Captured.unity");
            SelectAssets(scenePath);
            _window.CaptureSelectedScenes();

            _window.ClearResults();

            Assert.That(_window.SelectedSceneCount, Is.EqualTo(1));
            Assert.That(_window.IssueCount, Is.Zero);
            Assert.That(_window.StatusText, Is.EqualTo(
                "結果を消去しました。ビルド対象シーン、または記録済みの選択シーンを再度検査してください。"));
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
                "欠落スクリプト: 1");

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
                    $"欠落スクリプト: {finding.MissingScriptCount}");
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

        [Test]
        public void JapaneseMenuAndButtonLabels_AreConfiguredExactly()
        {
            var toolMenu = GetMenuItem("ShowFromTools");
            var assetMenu = GetMenuItem("ShowFromAssets");
            var assetMenuValidation = GetMenuItem("ValidateShowFromAssets");

            Assert.That(_window.StatusText, Is.EqualTo(
                "「ビルド対象シーンを検査」を押して、現在のビルドプロファイルを確認してください。"));
            Assert.That(GetPrivateStaticField<string>("ToolMenuPath"), Is.EqualTo(
                "Tools/ビルドガード/ビルド対象シーンを検査"));
            Assert.That(GetPrivateStaticField<string>("AssetMenuPath"), Is.EqualTo(
                "Assets/ビルドガード/選択シーンを検査"));
            Assert.That(GetPrivateStaticField<GUIContent>("ScanBuildScenesButtonContent").text, Is.EqualTo(
                "ビルド対象シーンを検査"));
            Assert.That(GetPrivateStaticField<GUIContent>("ScanSelectedScenesButtonContent").text, Is.EqualTo(
                "選択シーンを検査"));
            Assert.That(GetPrivateStaticField<GUIContent>("ClearButtonContent").text, Is.EqualTo(
                "結果を消去"));
            Assert.That(toolMenu.menuItem, Is.EqualTo("Tools/ビルドガード/ビルド対象シーンを検査"));
            Assert.That(toolMenu.validate, Is.False);
            Assert.That(toolMenu.priority, Is.EqualTo(2000));
            Assert.That(assetMenu.menuItem, Is.EqualTo("Assets/ビルドガード/選択シーンを検査"));
            Assert.That(assetMenu.validate, Is.False);
            Assert.That(assetMenu.priority, Is.EqualTo(2000));
            Assert.That(assetMenuValidation.menuItem, Is.EqualTo(
                "Assets/ビルドガード/選択シーンを検査"));
            Assert.That(assetMenuValidation.validate, Is.True);
        }

        [Test]
        public void ValidateSelectedSceneMenu_EmptySelectionIsDisabledAndSceneSelectionIsEnabled()
        {
            Selection.objects = Array.Empty<UnityEngine.Object>();
            Assert.That(InvokePrivateStaticMethod<bool>("ValidateShowFromAssets"), Is.False);

            var scenePath = CreateSavedScene("MenuSelection.unity");
            SelectAssets(scenePath);

            Assert.That(InvokePrivateStaticMethod<bool>("ValidateShowFromAssets"), Is.True);
        }

        [Test]
        public void FormatBuildSceneCancellationStatus_ShowsRetainedIssueCountInJapanese()
        {
            var issue = new BuildGuardScanIssue(
                BuildGuardIssueKind.MissingScript,
                "Assets/検査対象.unity",
                "ルート[0]",
                "欠落スクリプト: 1");
            var result = new BuildGuardManualScanResult(
                new[] { issue },
                3,
                true);

            var text = InvokePrivateStaticMethod<string>("FormatStatus", result);

            Assert.That(text, Is.EqualTo(
                "シーンを3件検査した時点で中止しました。1件の問題を保持しています。"));
        }

        [Test]
        public void FormatSelectedSceneFindingStatus_ShowsIssueCountInJapanese()
        {
            var issue = new BuildGuardScanIssue(
                BuildGuardIssueKind.MissingScript,
                "Assets/検査対象.unity",
                "ルート[0]",
                "欠落スクリプト: 1");
            var result = new BuildGuardManualScanResult(
                new[] { issue },
                2,
                false);

            var text = InvokePrivateStaticMethod<string>("FormatSelectedStatus", result);

            Assert.That(text, Is.EqualTo(
                "選択シーンを2件検査し、1件の問題を見つけました。"));
        }

        [Test]
        public void FormatRemovalStatus_ShowsRemovedCountAndNextActionInJapanese()
        {
            var text = InvokePrivateStaticMethod<string>("FormatRemovalStatus", 2);

            Assert.That(text, Is.EqualTo(
                "欠落スクリプトを2件除去しました。未保存のシーンを確認し、保存するか元に戻してください。"));
        }

        [TestCase(BuildGuardIssueKind.MissingScript, "欠落スクリプト")]
        [TestCase(BuildGuardIssueKind.MissingObjectReference, "欠落オブジェクト参照")]
        public void FormatClipboardText_JapaneseKindAndPropertyLabels_ArePreserved(
            BuildGuardIssueKind kind,
            string expectedKind)
        {
            var issue = new BuildGuardScanIssue(
                kind,
                "Assets/検査対象.unity",
                "ルート[0]/子[0]",
                "欠落箇所");

            var text = InvokePrivateStaticMethod<string>("FormatClipboardText", issue);

            Assert.That(text, Is.EqualTo(
                $"{expectedKind} | シーン: Assets/検査対象.unity | ゲームオブジェクト: ルート[0]/子[0] | 詳細: 欠落箇所"));
        }

        private string CreateSavedScene(string fileName)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject(Path.GetFileNameWithoutExtension(fileName));
            var scenePath = $"{_temporaryFolder}/{fileName}";
            Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
            return scenePath;
        }

        private static void SelectAssets(params string[] assetPaths)
        {
            var assets = new UnityEngine.Object[assetPaths.Length];
            for (var index = 0; index < assetPaths.Length; index++)
            {
                assets[index] = AssetDatabase.LoadAssetAtPath<SceneAsset>(assetPaths[index]);
                Assert.That(assets[index], Is.Not.Null, assetPaths[index]);
            }

            Selection.objects = assets;
        }

        private static T GetPrivateStaticField<T>(string fieldName)
        {
            var field = typeof(BuildGuardScanWindow).GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(null);
        }

        private static MenuItem GetMenuItem(string methodName)
        {
            var method = typeof(BuildGuardScanWindow).GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, methodName);
            var menuItem = method.GetCustomAttribute<MenuItem>();
            Assert.That(menuItem, Is.Not.Null, methodName);
            return menuItem;
        }

        private static T InvokePrivateStaticMethod<T>(string methodName, params object[] arguments)
        {
            var method = typeof(BuildGuardScanWindow).GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, methodName);
            return (T)method.Invoke(null, arguments);
        }
    }
}
