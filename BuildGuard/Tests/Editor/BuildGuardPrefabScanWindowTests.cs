// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BuildGuard.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace BuildGuard.Tests
{
    /// <summary>
    /// 選択プレハブの検査画面、移動、欠落スクリプト修復を確認します。
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
            Assert.That(_window.StatusText, Is.EqualTo(
                "選択中のプレハブアセットを2件記録しました。"));
            _window.RunScan();

            Assert.That(_window.SelectedPrefabCount, Is.EqualTo(2));
            Assert.That(_window.IssueCount, Is.Zero);
            Assert.That(_window.StatusText, Is.EqualTo(
                "プレハブを2件検査しました。欠落参照は見つかりませんでした。"));
        }

        [Test]
        public void TryResolveSelectedPrefabPaths_SelectedCandidateLimitHasExactBoundary()
        {
            var exactCandidates = CreateIndexedPaths(
                "Packages/対象",
                ".asset",
                BuildGuardPrefabScanWindow.MaximumSelectedAssetCandidates);

            var exactSucceeded = BuildGuardPrefabScanWindow.TryResolveSelectedPrefabPaths(
                exactCandidates,
                _ => false,
                _ => throw new InvalidOperationException("フォルダー展開は呼ばれないはずです。"),
                _ => throw new InvalidOperationException("プレハブ判定は呼ばれないはずです。"),
                out var exactPaths,
                out var exactError);

            Assert.That(exactSucceeded, Is.True);
            Assert.That(exactPaths, Is.Empty);
            Assert.That(exactError, Is.Empty);

            var excessiveCandidates = CreateIndexedPaths(
                "Packages/対象",
                ".asset",
                BuildGuardPrefabScanWindow.MaximumSelectedAssetCandidates + 1);
            var excessiveSucceeded = BuildGuardPrefabScanWindow.TryResolveSelectedPrefabPaths(
                excessiveCandidates,
                _ => throw new InvalidOperationException("フォルダー判定は呼ばれないはずです。"),
                _ => throw new InvalidOperationException("フォルダー展開は呼ばれないはずです。"),
                _ => throw new InvalidOperationException("プレハブ判定は呼ばれないはずです。"),
                out var excessivePaths,
                out var excessiveError);

            Assert.That(excessiveSucceeded, Is.False);
            Assert.That(excessivePaths, Is.Empty);
            Assert.That(excessiveError, Is.EqualTo(
                "選択中のアセット候補が多すぎます。選択できる候補は最大4,096件です。"));
        }

        [Test]
        public void TryResolveSelectedPrefabPaths_FolderExpansionCountsTowardCandidateLimit()
        {
            var exactExpansion = CreateIndexedPaths(
                "Packages/対象",
                ".asset",
                BuildGuardPrefabScanWindow.MaximumSelectedAssetCandidates - 1);
            var exactSucceeded = BuildGuardPrefabScanWindow.TryResolveSelectedPrefabPaths(
                new[] { "Assets/対象フォルダー" },
                path => path == "Assets/対象フォルダー",
                _ => exactExpansion,
                _ => throw new InvalidOperationException("プレハブ判定は呼ばれないはずです。"),
                out var exactPaths,
                out var exactError);

            Assert.That(exactSucceeded, Is.True);
            Assert.That(exactPaths, Is.Empty);
            Assert.That(exactError, Is.Empty);

            var excessiveExpansion = CreateIndexedPaths(
                "Packages/対象",
                ".asset",
                BuildGuardPrefabScanWindow.MaximumSelectedAssetCandidates + 1);
            var excessiveSucceeded = BuildGuardPrefabScanWindow.TryResolveSelectedPrefabPaths(
                new[] { "Assets/対象フォルダー" },
                path => path == "Assets/対象フォルダー",
                _ => excessiveExpansion,
                _ => throw new InvalidOperationException("プレハブ判定は呼ばれないはずです。"),
                out var excessivePaths,
                out var excessiveError);

            Assert.That(excessiveSucceeded, Is.False);
            Assert.That(excessivePaths, Is.Empty);
            Assert.That(excessiveError, Is.EqualTo(
                "選択中のアセット候補が多すぎます。選択できる候補は最大4,096件です。"));
        }

        [Test]
        public void TryResolveSelectedPrefabPaths_ResolvedPrefabLimitHasExactBoundary()
        {
            var exactPrefabs = CreateIndexedPaths(
                "Assets/対象フォルダー/Prefab",
                ".prefab",
                BuildGuardPrefabScanWindow.MaximumSelectedPrefabs);
            var exactSucceeded = BuildGuardPrefabScanWindow.TryResolveSelectedPrefabPaths(
                new[] { "Assets/対象フォルダー" },
                path => path == "Assets/対象フォルダー",
                _ => exactPrefabs,
                _ => true,
                out var exactPaths,
                out var exactError);

            Assert.That(exactSucceeded, Is.True);
            Assert.That(exactPaths, Has.Count.EqualTo(
                BuildGuardPrefabScanWindow.MaximumSelectedPrefabs));
            Assert.That(exactError, Is.Empty);

            var excessivePrefabs = CreateIndexedPaths(
                "Assets/対象フォルダー/Prefab",
                ".prefab",
                BuildGuardPrefabScanWindow.MaximumSelectedPrefabs + 1);
            var excessiveSucceeded = BuildGuardPrefabScanWindow.TryResolveSelectedPrefabPaths(
                new[] { "Assets/対象フォルダー" },
                path => path == "Assets/対象フォルダー",
                _ => excessivePrefabs,
                _ => true,
                out var excessivePaths,
                out var excessiveError);

            Assert.That(excessiveSucceeded, Is.False);
            Assert.That(excessivePaths, Is.Empty);
            Assert.That(excessiveError, Is.EqualTo(
                "対象のプレハブが多すぎます。1回に記録できるプレハブは最大256件です。"));
        }

        [Test]
        public void TryResolveSelectedPrefabPaths_ExpandsFoldersNormalizesAndRemovesDuplicates()
        {
            var folderExpansionCount = 0;
            var succeeded = BuildGuardPrefabScanWindow.TryResolveSelectedPrefabPaths(
                new[]
                {
                    "Assets/Z.prefab",
                    "Assets/対象フォルダー",
                    "Assets\\A.prefab"
                },
                path => path == "Assets/対象フォルダー",
                _ =>
                {
                    folderExpansionCount++;
                    return new[]
                    {
                        "Assets/対象フォルダー/B.prefab",
                        "Assets/Z.prefab",
                        "Assets\\対象フォルダー\\B.prefab",
                        "Packages/対象外.prefab"
                    };
                },
                _ => true,
                out var prefabPaths,
                out var errorMessage);

            Assert.That(succeeded, Is.True);
            Assert.That(folderExpansionCount, Is.EqualTo(1));
            Assert.That(prefabPaths, Is.EqualTo(new[]
            {
                "Assets/A.prefab",
                "Assets/Z.prefab",
                "Assets/対象フォルダー/B.prefab"
            }));
            Assert.That(errorMessage, Is.Empty);
        }

        [Test]
        public void TryResolveSelectedPrefabPaths_UnexpectedExceptionIsLoggedWithoutExposingDetails()
        {
            LogAssert.Expect(
                LogType.Exception,
                new Regex("^InvalidOperationException: 試験用のフォルダー展開失敗"));

            var succeeded = BuildGuardPrefabScanWindow.TryResolveSelectedPrefabPaths(
                new[] { "Assets/対象フォルダー" },
                _ => true,
                _ => throw new InvalidOperationException("試験用のフォルダー展開失敗: 内部情報"),
                _ => true,
                out var prefabPaths,
                out var errorMessage);

            Assert.That(succeeded, Is.False);
            Assert.That(prefabPaths, Is.Empty);
            Assert.That(errorMessage, Is.EqualTo(
                "選択中のプレハブを取得できませんでした。Unityのログで原因を確認し、もう一度「現在の選択を使用」を押してください。"));
            Assert.That(errorMessage, Does.Not.Contain("内部情報"));
        }

        [Test]
        public void TryResolveSelectedPrefabPaths_MissingDependencyReturnsExactJapaneseReason()
        {
            var succeeded = BuildGuardPrefabScanWindow.TryResolveSelectedPrefabPaths(
                Array.Empty<string>(),
                null,
                _ => Array.Empty<string>(),
                _ => true,
                out var prefabPaths,
                out var errorMessage);

            Assert.That(succeeded, Is.False);
            Assert.That(prefabPaths, Is.Empty);
            Assert.That(errorMessage, Is.EqualTo(
                "選択中のプレハブを取得するための処理を利用できません。"));
        }

        [Test]
        public void HasSelectedPrefabCandidate_ChecksOnlyDirectPrefabOrAssetsFolder()
        {
            var prefabCheckCount = 0;
            Assert.That(BuildGuardPrefabScanWindow.HasSelectedPrefabCandidate(
                new[] { "Assets/空フォルダー" },
                path => path == "Assets/空フォルダー",
                _ =>
                {
                    prefabCheckCount++;
                    return false;
                }), Is.True);
            Assert.That(prefabCheckCount, Is.Zero);

            Assert.That(BuildGuardPrefabScanWindow.HasSelectedPrefabCandidate(
                new[] { "Assets/対象.prefab" },
                _ => false,
                _ => true), Is.True);
            Assert.That(BuildGuardPrefabScanWindow.HasSelectedPrefabCandidate(
                new[] { "Assets/対象.asset", "Packages/対象.prefab" },
                _ => false,
                _ => true), Is.False);

            var excessiveNonAssets = CreateIndexedPaths(
                "Packages/対象",
                ".prefab",
                BuildGuardPrefabScanWindow.MaximumSelectedAssetCandidates + 1);
            Assert.That(BuildGuardPrefabScanWindow.HasSelectedPrefabCandidate(
                excessiveNonAssets,
                _ => true,
                _ => true), Is.False);
        }

        [Test]
        public void JapaneseMenuAndVisibleLabels_AreConfiguredExactly()
        {
            var toolMenu = GetMenuItem("ShowFromTools");
            var assetMenu = GetMenuItem("ShowFromAssets");
            var assetMenuValidation = GetMenuItem("ValidateShowFromAssets");

            Assert.That(_window.StatusText, Is.EqualTo(
                "プレハブアセットを選択し、「選択プレハブを検査」を押してください。"));
            Assert.That(GetPrivateStaticField<string>("ToolMenuPath"), Is.EqualTo(
                "Tools/ビルドガード/選択プレハブを検査"));
            Assert.That(GetPrivateStaticField<string>("AssetMenuPath"), Is.EqualTo(
                "Assets/ビルドガード/選択プレハブを検査"));
            Assert.That(toolMenu.menuItem, Is.EqualTo("Tools/ビルドガード/選択プレハブを検査"));
            Assert.That(toolMenu.validate, Is.False);
            Assert.That(toolMenu.priority, Is.EqualTo(2001));
            Assert.That(assetMenu.menuItem, Is.EqualTo("Assets/ビルドガード/選択プレハブを検査"));
            Assert.That(assetMenu.validate, Is.False);
            Assert.That(assetMenu.priority, Is.EqualTo(2001));
            Assert.That(assetMenuValidation.menuItem, Is.EqualTo(
                "Assets/ビルドガード/選択プレハブを検査"));
            Assert.That(assetMenuValidation.validate, Is.True);
            Assert.That(GetPrivateStaticField<string>("WindowTitleText"), Is.EqualTo(
                "ビルドガード - プレハブ検査"));
            Assert.That(GetPrivateStaticField<string>("HeadingText"), Is.EqualTo(
                "選択プレハブの参照検査"));
            Assert.That(GetPrivateStaticField<string>("DescriptionText"), Is.EqualTo(
                "選択したプレハブを一時的に読み込み、保存せずに欠落スクリプトと欠落オブジェクト参照を検査します。"));
            Assert.That(GetPrivateStaticField<string>("SelectedPrefabsLabelText"), Is.EqualTo(
                "記録済みの選択プレハブ"));
            Assert.That(GetPrivateStaticField<string>("CaptureSelectionButtonText"), Is.EqualTo(
                "現在の選択を使用"));
            Assert.That(GetPrivateStaticField<string>("ScanButtonText"), Is.EqualTo(
                "選択プレハブを検査"));
            Assert.That(GetPrivateStaticField<string>("ClearButtonText"), Is.EqualTo(
                "結果を消去"));
            Assert.That(GetPrivateStaticField<string>("OpenButtonText"), Is.EqualTo(
                "プレハブを開く"));
            Assert.That(GetPrivateStaticField<string>("CopyButtonText"), Is.EqualTo(
                "内容をコピー"));
            Assert.That(GetPrivateStaticField<string>("RemoveButtonText"), Is.EqualTo(
                "開いて除去"));
        }

        [Test]
        public void ValidateSelectedPrefabMenu_UsesCurrentProjectSelection()
        {
            Assert.That(InvokePrivateStaticMethod<bool>("ValidateShowFromAssets"), Is.False);

            var texture = new Texture2D(1, 1);
            var texturePath = $"{_temporaryFolder}/Texture.asset";
            AssetDatabase.CreateAsset(texture, texturePath);
            Selection.activeObject = texture;
            Assert.That(InvokePrivateStaticMethod<bool>("ValidateShowFromAssets"), Is.False);

            var emptyFolderGuid = AssetDatabase.CreateFolder(_temporaryFolder, "空フォルダー");
            var emptyFolderPath = AssetDatabase.GUIDToAssetPath(emptyFolderGuid);
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(emptyFolderPath);
            Assert.That(InvokePrivateStaticMethod<bool>("ValidateShowFromAssets"), Is.True);

            var prefabPath = CreateValidPrefab("Menu.prefab");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(InvokePrivateStaticMethod<bool>("ValidateShowFromAssets"), Is.True);

            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(_temporaryFolder);
            Assert.That(InvokePrivateStaticMethod<bool>("ValidateShowFromAssets"), Is.True);
        }

        [Test]
        public void CaptureSelection_EmptySelection_ShowsExactJapaneseGuidance()
        {
            _window.CaptureSelection();
            _window.RunScan();

            Assert.That(_window.SelectedPrefabCount, Is.Zero);
            Assert.That(_window.IssueCount, Is.Zero);
            Assert.That(_window.StatusText, Is.EqualTo(
                "プロジェクトウィンドウでプレハブアセットを1件以上選択してください。"));
        }

        [Test]
        public void RunScan_BrokenPrefab_ShowsExactJapaneseFindingStatus()
        {
            var prefabPath = CopyBrokenPrefab();
            SelectAssets(prefabPath);
            _window.CaptureSelection();

            _window.RunScan();

            Assert.That(_window.SelectedPrefabCount, Is.EqualTo(1));
            Assert.That(_window.IssueCount, Is.EqualTo(1));
            Assert.That(_window.StatusText, Is.EqualTo(
                "プレハブを1件検査し、1件の問題を見つけました。"));
        }

        [Test]
        public void RunScan_CancelAfterBrokenPrefab_ShowsExactJapaneseCancellationStatus()
        {
            var brokenPath = CopyBrokenPrefab();
            var validPath = CreateValidPrefab("Z_Valid.prefab");
            SelectAssets(validPath, brokenPath);
            _window.CaptureSelection();

            _window.RunScan((index, _, _) => index == 1);

            Assert.That(_window.IssueCount, Is.EqualTo(1));
            Assert.That(_window.StatusText, Is.EqualTo(
                "プレハブを1件検査した時点で中止しました。1件の問題を保持しています。"));
        }

        [Test]
        public void RunScan_DeletedCapturedPrefab_ShowsExactJapaneseStaleGuidance()
        {
            var prefabPath = CopyBrokenPrefab();
            SelectAssets(prefabPath);
            _window.CaptureSelection();
            _window.RunScan();
            Assert.That(_window.IssueCount, Is.EqualTo(1));
            Assert.That(AssetDatabase.DeleteAsset(prefabPath), Is.True);

            _window.RunScan();

            Assert.That(_window.SelectedPrefabCount, Is.EqualTo(1));
            Assert.That(_window.IssueCount, Is.Zero);
            Assert.That(_window.StatusText, Is.EqualTo(
                "記録済みのプレハブが移動または削除されています。「現在の選択を使用」を押してから、もう一度検査してください。"));
            Assert.That(InvokePrivateInstanceMethod<MessageType>("GetStatusMessageType"), Is.EqualTo(
                MessageType.Error));
        }

        [Test]
        public void RunScan_FailureAfterFinding_DiscardsOldAndPartialIssues()
        {
            var brokenPath = CopyBrokenPrefab();
            var validPath = CreateValidPrefab("Z_Valid.prefab");
            SelectAssets(validPath, brokenPath);
            _window.CaptureSelection();
            _window.RunScan((index, _, _) => index == 1);
            Assert.That(_window.IssueCount, Is.EqualTo(1));
            LogAssert.Expect(
                LogType.Exception,
                new Regex("^InvalidOperationException: 試験用の検査失敗"));

            _window.RunScan((index, _, _) => index == 1
                ? throw new InvalidOperationException("試験用の検査失敗")
                : false);

            Assert.That(_window.SelectedPrefabCount, Is.EqualTo(2));
            Assert.That(_window.IssueCount, Is.Zero);
            Assert.That(_window.StatusText, Is.EqualTo(
                "プレハブの検査に失敗したため、結果を破棄しました。Unityのログで原因を確認してください。"));
            Assert.That(InvokePrivateInstanceMethod<MessageType>("GetStatusMessageType"), Is.EqualTo(
                MessageType.Error));
        }

        [Test]
        public void CaptureSelection_Failure_DiscardsOldStateAndShowsJapaneseGuidance()
        {
            var prefabPath = CopyBrokenPrefab();
            SelectAssets(prefabPath);
            _window.CaptureSelection();
            _window.RunScan();
            Assert.That(_window.IssueCount, Is.EqualTo(1));
            LogAssert.Expect(
                LogType.Exception,
                new Regex("^InvalidOperationException: 試験用の選択取得失敗"));

            _window.CaptureSelection(
                () => throw new InvalidOperationException("試験用の選択取得失敗"));

            Assert.That(_window.SelectedPrefabCount, Is.Zero);
            Assert.That(_window.IssueCount, Is.Zero);
            Assert.That(_window.StatusText, Is.EqualTo(
                "選択中のプレハブを取得できませんでした。Unityのログで原因を確認し、もう一度「現在の選択を使用」を押してください。"));
            Assert.That(InvokePrivateInstanceMethod<MessageType>("GetStatusMessageType"), Is.EqualTo(
                MessageType.Error));
        }

        [Test]
        public void CaptureSelection_ExpectedLimitDiscardsOldStateWithoutLoggingAnException()
        {
            var prefabPath = CopyBrokenPrefab();
            SelectAssets(prefabPath);
            _window.CaptureSelection();
            _window.RunScan();
            Assert.That(_window.IssueCount, Is.EqualTo(1));

            _window.CaptureSelection(
                () => throw new BuildGuardPrefabScanWindow.SelectionCaptureException(
                    "選択中のアセット候補が多すぎます。選択できる候補は最大4,096件です。"));

            Assert.That(_window.SelectedPrefabCount, Is.Zero);
            Assert.That(_window.IssueCount, Is.Zero);
            Assert.That(_window.StatusText, Is.EqualTo(
                "選択中のアセット候補が多すぎます。選択できる候補は最大4,096件です。"));
            Assert.That(InvokePrivateInstanceMethod<MessageType>("GetStatusMessageType"), Is.EqualTo(
                MessageType.Error));
        }

        [Test]
        public void ClearResults_AfterFinding_PreservesCaptureAndShowsExactJapaneseStatus()
        {
            var prefabPath = CopyBrokenPrefab();
            SelectAssets(prefabPath);
            _window.CaptureSelection();
            _window.RunScan();
            Assert.That(_window.IssueCount, Is.EqualTo(1));

            _window.ClearResults();

            Assert.That(_window.SelectedPrefabCount, Is.EqualTo(1));
            Assert.That(_window.IssueCount, Is.Zero);
            Assert.That(_window.StatusText, Is.EqualTo(
                "結果を消去しました。「選択プレハブを検査」を押して、もう一度検査してください。"));
            Assert.That(InvokePrivateInstanceMethod<MessageType>("GetStatusMessageType"), Is.EqualTo(
                MessageType.Info));
        }

        [Test]
        public void TryRemoveMissingScripts_OpensPrefabStageAndSupportsUndo()
        {
            var prefabPath = CopyBrokenPrefab();
            var issue = BuildGuardPrefabScanner.Scan(new[] { prefabPath }).Issues[0];
            Assert.That(issue.TargetGlobalObjectId, Is.Not.Empty);
            try
            {
                Undo.IncrementCurrentGroup();
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
                Assert.That(Undo.GetCurrentGroupName(), Is.EqualTo("欠落スクリプトを除去"));

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
        public void TryRemoveMissingScripts_EmptyTargetIdDoesNotOpenOrModifyPrefab()
        {
            var prefabPath = CopyBrokenPrefab();
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var issue = new BuildGuardPrefabScanIssue(
                BuildGuardIssueKind.MissingScript,
                prefabPath,
                "Broken[0]",
                "欠落スクリプト: 1");

            var opened = BuildGuardPrefabScanWindow.TryOpenIssue(issue, false);
            var removed = BuildGuardPrefabScanWindow.TryRemoveMissingScripts(
                issue,
                false,
                false,
                out var removedCount);

            Assert.That(opened, Is.False);
            Assert.That(removed, Is.False);
            Assert.That(removedCount, Is.Zero);
            Assert.That(PrefabStageUtility.GetCurrentPrefabStage(), Is.Null);
            Assert.That(Selection.activeObject, Is.EqualTo(prefabAsset));
            Assert.That(BuildGuardPrefabScanner.Scan(new[] { prefabPath }).Issues, Has.Count.EqualTo(1));
        }

        [Test]
        public void OldIssue_TargetIdentityReplaced_DoesNotNavigateOrRemove()
        {
            var prefabPath = CopyBrokenPrefab();
            var originalGuid = AssetDatabase.AssetPathToGUID(prefabPath);
            var oldIssue = BuildGuardPrefabScanner.Scan(new[] { prefabPath }).Issues[0];
            ReplaceBrokenPrefabObjectIdentity(prefabPath);
            var currentIssue = BuildGuardPrefabScanner.Scan(new[] { prefabPath }).Issues[0];
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            Assert.That(AssetDatabase.AssetPathToGUID(prefabPath), Is.EqualTo(originalGuid));
            Assert.That(currentIssue.Kind, Is.EqualTo(oldIssue.Kind));
            Assert.That(currentIssue.PrefabPath, Is.EqualTo(oldIssue.PrefabPath));
            Assert.That(currentIssue.HierarchyPath, Is.EqualTo(oldIssue.HierarchyPath));
            Assert.That(currentIssue.Details, Is.EqualTo(oldIssue.Details));
            Assert.That(currentIssue.TargetGlobalObjectId, Is.Not.EqualTo(
                oldIssue.TargetGlobalObjectId));

            var opened = BuildGuardPrefabScanWindow.TryOpenIssue(oldIssue, false);
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            Assert.That(opened, Is.False);
            Assert.That(stage, Is.Not.Null);
            Assert.That(stage.assetPath, Is.EqualTo(prefabPath));
            Assert.That(Selection.activeObject, Is.EqualTo(prefabAsset));
            Assert.That(
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(stage.prefabContentsRoot),
                Is.EqualTo(1));
            Assert.That(stage.scene.isDirty, Is.False);

            var removed = BuildGuardPrefabScanWindow.TryRemoveMissingScripts(
                oldIssue,
                false,
                false,
                out var removedCount);

            Assert.That(removed, Is.False);
            Assert.That(removedCount, Is.Zero);
            Assert.That(Selection.activeObject, Is.EqualTo(prefabAsset));
            Assert.That(
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(stage.prefabContentsRoot),
                Is.EqualTo(1));
            Assert.That(stage.scene.isDirty, Is.False);
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

        [Test]
        public void RemovalDialogProgressAndStatus_UseExactJapaneseText()
        {
            var issue = new BuildGuardPrefabScanIssue(
                BuildGuardIssueKind.MissingScript,
                "Assets/検査対象.prefab",
                "ルート[0]/子[0]",
                "欠落スクリプト: 2");

            Assert.That(GetPrivateStaticField<string>("ProgressTitleText"), Is.EqualTo(
                "ビルドガード"));
            Assert.That(GetPrivateStaticField<string>("RemovalDialogTitleText"), Is.EqualTo(
                "欠落スクリプトを除去"));
            Assert.That(GetPrivateStaticField<string>("RemovalCancelButtonText"), Is.EqualTo(
                "キャンセル"));
            Assert.That(GetPrivateStaticField<string>("RemovalUndoName"), Is.EqualTo(
                "欠落スクリプトを除去"));
            Assert.That(InvokePrivateStaticMethod<string>("FormatProgressText", 1, 3, issue.PrefabPath),
                Is.EqualTo("検査対象を検査中 (2/3)"));
            Assert.That(InvokePrivateStaticMethod<string>("FormatRemovalDialogMessage", issue), Is.EqualTo(
                "「Assets/検査対象.prefab」を開き、「ルート[0]/子[0]」から欠落スクリプトをすべて除去しますか？\n\n"
                + "自動保存が有効な場合は変更が保存され、無効な場合は未保存のままです。"
                + "除去後に保存状態を確認し、必要に応じて保存するか元に戻してください。"));
            Assert.That(InvokePrivateStaticMethod<string>("FormatRemovalStatus", 2), Is.EqualTo(
                "欠落スクリプトを2件除去しました。自動保存の設定によって保存状態が異なります。"
                + "プレハブ編集画面を確認し、必要に応じて保存するか元に戻してください。"));
        }

        [TestCase(
            BuildGuardIssueKind.MissingScript,
            "欠落スクリプト: 2",
            "欠落スクリプト",
            "欠落スクリプト: 2")]
        [TestCase(
            BuildGuardIssueKind.MissingObjectReference,
            "UnityEngine.Camera[1].m_TargetTexture",
            "欠落オブジェクト参照",
            "UnityEngine.Camera[1].m_TargetTexture")]
        public void FormatClipboardText_UsesExactJapaneseLabels(
            BuildGuardIssueKind kind,
            string details,
            string expectedKind,
            string expectedDetails)
        {
            var issue = new BuildGuardPrefabScanIssue(
                kind,
                "Assets/検査対象.prefab",
                "ルート[0]/子[0]",
                details);

            var text = InvokePrivateStaticMethod<string>("FormatClipboardText", issue);

            Assert.That(text, Is.EqualTo(
                $"{expectedKind} | プレハブ: Assets/検査対象.prefab | ゲームオブジェクト: ルート[0]/子[0] | 詳細: {expectedDetails}"));
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

        /// <summary>同じ階層と問題を保ったまま、プレハブ内オブジェクトの識別値だけを置き換えます。</summary>
        private static void ReplaceBrokenPrefabObjectIdentity(string prefabPath)
        {
            var contents = File.ReadAllText(prefabPath);
            Assert.That(contents, Does.Contain("--- !u!1 &1000"));
            Assert.That(contents, Does.Contain("--- !u!4 &1001"));
            Assert.That(contents, Does.Contain("--- !u!114 &1002"));
            var replaced = contents
                .Replace("&1000", "&2000")
                .Replace("{fileID: 1000}", "{fileID: 2000}")
                .Replace("&1001", "&2001")
                .Replace("{fileID: 1001}", "{fileID: 2001}")
                .Replace("&1002", "&2002")
                .Replace("{fileID: 1002}", "{fileID: 2002}");
            File.WriteAllText(prefabPath, replaced, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(
                prefabPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        /// <summary>上限試験用に、番号が重ならないパス一覧を作成します。</summary>
        private static IReadOnlyList<string> CreateIndexedPaths(
            string prefix,
            string extension,
            int count)
        {
            var paths = new string[count];
            for (var index = 0; index < count; index++)
            {
                paths[index] = $"{prefix}{index:D4}{extension}";
            }

            return paths;
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

        /// <summary>指定したアセットをプロジェクトウィンドウの選択状態へ設定します。</summary>
        private static void SelectAssets(params string[] assetPaths)
        {
            var assets = new UnityEngine.Object[assetPaths.Length];
            for (var index = 0; index < assetPaths.Length; index++)
            {
                assets[index] = AssetDatabase.LoadMainAssetAtPath(assetPaths[index]);
                Assert.That(assets[index], Is.Not.Null, assetPaths[index]);
            }

            Selection.objects = assets;
        }

        /// <summary>指定した私有静的フィールドの値を取得します。</summary>
        private static T GetPrivateStaticField<T>(string fieldName)
        {
            var field = typeof(BuildGuardPrefabScanWindow).GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(null);
        }

        /// <summary>指定したメニュー関数の属性を取得します。</summary>
        private static MenuItem GetMenuItem(string methodName)
        {
            var method = typeof(BuildGuardPrefabScanWindow).GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, methodName);
            var menuItem = method.GetCustomAttribute<MenuItem>();
            Assert.That(menuItem, Is.Not.Null, methodName);
            return menuItem;
        }

        /// <summary>指定した私有静的関数を呼び出します。</summary>
        private static T InvokePrivateStaticMethod<T>(string methodName, params object[] arguments)
        {
            var method = typeof(BuildGuardPrefabScanWindow).GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, methodName);
            return (T)method.Invoke(null, arguments);
        }

        /// <summary>指定した私有インスタンス関数を呼び出します。</summary>
        private T InvokePrivateInstanceMethod<T>(string methodName, params object[] arguments)
        {
            var method = typeof(BuildGuardPrefabScanWindow).GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, methodName);
            return (T)method.Invoke(_window, arguments);
        }
    }
}
