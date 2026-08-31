// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BuildGuard.Editor
{
    /// <summary>
    /// 選択したプレハブアセットから見つかった欠落参照を表示します。
    /// </summary>
    internal sealed class BuildGuardPrefabScanWindow : EditorWindow
    {
        /// <summary>1回の取得で受け付ける選択アセット候補の最大数です。</summary>
        internal const int MaximumSelectedAssetCandidates = 4096;

        /// <summary>1回の取得で記録するプレハブアセットの最大数です。</summary>
        internal const int MaximumSelectedPrefabs = 256;

        /// <summary>Toolsメニューに登録する経路です。</summary>
        private const string ToolMenuPath = "Tools/ビルドガード/選択プレハブを検査";

        /// <summary>Assetsメニューに登録する経路です。</summary>
        private const string AssetMenuPath = "Assets/ビルドガード/選択プレハブを検査";

        /// <summary>画面のタイトルです。</summary>
        private const string WindowTitleText = "ビルドガード - プレハブ検査";

        /// <summary>画面上部の見出しです。</summary>
        private const string HeadingText = "選択プレハブの参照検査";

        /// <summary>検査内容を説明する案内文です。</summary>
        private const string DescriptionText =
            "選択したプレハブを一時的に読み込み、保存せずに欠落スクリプトと欠落オブジェクト参照を検査します。";

        /// <summary>記録済み件数の見出しです。</summary>
        private const string SelectedPrefabsLabelText = "記録済みの選択プレハブ";

        /// <summary>現在の選択を記録するボタンの文言です。</summary>
        private const string CaptureSelectionButtonText = "現在の選択を使用";

        /// <summary>検査を開始するボタンの文言です。</summary>
        private const string ScanButtonText = "選択プレハブを検査";

        /// <summary>検査結果を消去するボタンの文言です。</summary>
        private const string ClearButtonText = "結果を消去";

        /// <summary>問題のあるプレハブを開くボタンの文言です。</summary>
        private const string OpenButtonText = "プレハブを開く";

        /// <summary>問題内容を複写するボタンの文言です。</summary>
        private const string CopyButtonText = "内容をコピー";

        /// <summary>欠落スクリプト除去を開始するボタンの文言です。</summary>
        private const string RemoveButtonText = "開いて除去";

        /// <summary>検査進捗画面のタイトルです。</summary>
        private const string ProgressTitleText = "ビルドガード";

        /// <summary>欠落スクリプト除去確認画面のタイトルです。</summary>
        private const string RemovalDialogTitleText = "欠落スクリプトを除去";

        /// <summary>欠落スクリプト除去を中止するボタンの文言です。</summary>
        private const string RemovalCancelButtonText = "キャンセル";

        /// <summary>欠落スクリプト除去を元に戻す操作名です。</summary>
        private const string RemovalUndoName = "欠落スクリプトを除去";

        /// <summary>画面を開いた直後の案内文です。</summary>
        private const string InitialStatusText =
            "プレハブアセットを選択し、「選択プレハブを検査」を押してください。";

        /// <summary>選択にプレハブがない場合の案内文です。</summary>
        private const string EmptySelectionStatusText =
            "プロジェクトウィンドウでプレハブアセットを1件以上選択してください。";

        /// <summary>記録済みプレハブが利用できなくなった場合の案内文です。</summary>
        private const string StaleSelectionStatusText =
            "記録済みのプレハブが移動または削除されています。「現在の選択を使用」を押してから、もう一度検査してください。";

        /// <summary>予期しない選択取得失敗を知らせる案内文です。</summary>
        private const string SelectionCaptureFailureStatusText =
            "選択中のプレハブを取得できませんでした。Unityのログで原因を確認し、もう一度「現在の選択を使用」を押してください。";

        /// <summary>選択候補が上限を超えた場合の案内文です。</summary>
        private const string SelectionCandidateLimitStatusText =
            "選択中のアセット候補が多すぎます。選択できる候補は最大4,096件です。";

        /// <summary>解決済みプレハブが上限を超えた場合の案内文です。</summary>
        private const string SelectedPrefabLimitStatusText =
            "対象のプレハブが多すぎます。1回に記録できるプレハブは最大256件です。";

        /// <summary>予期しない検査失敗を知らせる案内文です。</summary>
        private const string GeneralScanFailureStatusText =
            "プレハブの検査に失敗したため、結果を破棄しました。Unityのログで原因を確認してください。";

        /// <summary>結果消去後に再検査を促す案内文です。</summary>
        private const string ClearedStatusText =
            "結果を消去しました。「選択プレハブを検査」を押して、もう一度検査してください。";

        /// <summary>現在画面に表示している問題一覧です。</summary>
        private readonly List<BuildGuardPrefabScanIssue> _issues = new List<BuildGuardPrefabScanIssue>();

        /// <summary>現在の選択から記録したプレハブパス一覧です。</summary>
        private string[] _prefabPaths = Array.Empty<string>();

        /// <summary>問題一覧の現在のスクロール位置です。</summary>
        private Vector2 _scrollPosition;

        /// <summary>状態欄を失敗表示にする必要があるかを表します。</summary>
        private bool _scanFailed;

        /// <summary>画面の状態欄へ表示する案内文です。</summary>
        private string _statusText = InitialStatusText;

        /// <summary>現在表示している問題の件数を返します。</summary>
        internal int IssueCount => _issues.Count;

        /// <summary>現在記録している選択プレハブの件数を返します。</summary>
        internal int SelectedPrefabCount => _prefabPaths.Length;

        /// <summary>現在の状態文を返します。</summary>
        internal string StatusText => _statusText;

        /// <summary>Toolsメニューからプレハブ検査画面を開きます。</summary>
        [MenuItem(ToolMenuPath, priority = 2001)]
        private static void ShowFromTools()
        {
            ShowWindow();
        }

        /// <summary>Assetsメニューからプレハブ検査画面を開きます。</summary>
        [MenuItem(AssetMenuPath, false, 2001)]
        private static void ShowFromAssets()
        {
            ShowWindow();
        }

        /// <summary>検査できるプレハブが選択されている場合だけ、Assetsメニューを有効にします。</summary>
        [MenuItem(AssetMenuPath, true)]
        private static bool ValidateShowFromAssets()
        {
            try
            {
                var selectedObjects = Selection.objects ?? Array.Empty<UnityEngine.Object>();
                var selectedAssetPaths = new string[selectedObjects.Length];
                for (var index = 0; index < selectedObjects.Length; index++)
                {
                    selectedAssetPaths[index] = selectedObjects[index] == null
                        ? string.Empty
                        : AssetDatabase.GetAssetPath(selectedObjects[index]);
                }

                return HasSelectedPrefabCandidate(
                    selectedAssetPaths,
                    AssetDatabase.IsValidFolder,
                    path => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>プレハブ検査画面を作成するか、既存の画面を再利用します。</summary>
        private static void ShowWindow()
        {
            var window = GetWindow<BuildGuardPrefabScanWindow>();
            window.titleContent = new GUIContent(WindowTitleText);
            window.minSize = new Vector2(720f, 320f);
            window.CaptureSelection();
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(HeadingText, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                DescriptionText,
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField(SelectedPrefabsLabelText, $"{_prefabPaths.Length}件");
            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(CaptureSelectionButtonText, GUILayout.Height(28f)))
                {
                    CaptureSelection();
                }

                using (new EditorGUI.DisabledScope(_prefabPaths.Length == 0))
                {
                    if (GUILayout.Button(ScanButtonText, GUILayout.Height(28f)))
                    {
                        ScanWithProgress();
                    }
                }

                using (new EditorGUI.DisabledScope(_issues.Count == 0))
                {
                    if (GUILayout.Button(ClearButtonText, GUILayout.Width(104f), GUILayout.Height(28f)))
                    {
                        ClearResults();
                    }
                }
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(_statusText, GetStatusMessageType());
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            for (var index = 0; index < _issues.Count; index++)
            {
                DrawIssue(index, _issues[index]);
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>現在選択されているプレハブを記録し、古い問題一覧を消去します。</summary>
        internal void CaptureSelection(Func<IReadOnlyList<string>> selectedPathProvider = null)
        {
            try
            {
                var selectedPaths = (selectedPathProvider ?? GetSelectedPrefabPaths)();
                _prefabPaths = selectedPaths == null
                    ? Array.Empty<string>()
                    : new List<string>(selectedPaths).ToArray();
            }
            catch (SelectionCaptureException exception)
            {
                SetSelectionCaptureFailure(exception.Message);
                return;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetSelectionCaptureFailure(SelectionCaptureFailureStatusText);
                return;
            }

            _issues.Clear();
            _scrollPosition = Vector2.zero;
            _scanFailed = false;
            _statusText = _prefabPaths.Length == 0
                ? EmptySelectionStatusText
                : $"選択中のプレハブアセットを{_prefabPaths.Length}件記録しました。";
            Repaint();
        }

        /// <summary>記録済みのプレハブを検査し、画面の問題一覧を置き換えます。</summary>
        internal void RunScan(Func<int, int, string, bool> shouldCancel = null)
        {
            if (_prefabPaths.Length == 0)
            {
                _issues.Clear();
                _scanFailed = false;
                _statusText = EmptySelectionStatusText;
                Repaint();
                return;
            }

            BuildGuardPrefabScanResult result;
            try
            {
                result = BuildGuardPrefabScanner.Scan(_prefabPaths, shouldCancel);
            }
            catch (ArgumentException)
            {
                _issues.Clear();
                _scanFailed = true;
                _statusText = StaleSelectionStatusText;
                Repaint();
                return;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _issues.Clear();
                _scanFailed = true;
                _statusText = GeneralScanFailureStatusText;
                Repaint();
                return;
            }

            _issues.Clear();
            _issues.AddRange(result.Issues);
            _scanFailed = false;
            _statusText = FormatStatus(result);
            Repaint();
        }

        /// <summary>記録済みの選択を保ったまま、問題一覧だけを消去します。</summary>
        internal void ClearResults()
        {
            _issues.Clear();
            _scrollPosition = Vector2.zero;
            _scanFailed = false;
            _statusText = ClearedStatusText;
            Repaint();
        }

        /// <summary>選択中のプレハブと、選択フォルダー内のプレハブをパス順で返します。</summary>
        internal static IReadOnlyList<string> GetSelectedPrefabPaths()
        {
            var selectedObjects = Selection.objects ?? Array.Empty<UnityEngine.Object>();
            if (selectedObjects.Length > MaximumSelectedAssetCandidates)
            {
                throw new SelectionCaptureException(SelectionCandidateLimitStatusText);
            }

            var selectedAssetPaths = new string[selectedObjects.Length];
            try
            {
                for (var index = 0; index < selectedObjects.Length; index++)
                {
                    selectedAssetPaths[index] = selectedObjects[index] == null
                        ? string.Empty
                        : AssetDatabase.GetAssetPath(selectedObjects[index]);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw new SelectionCaptureException(SelectionCaptureFailureStatusText);
            }

            if (!TryResolveSelectedPrefabPaths(
                    selectedAssetPaths,
                    AssetDatabase.IsValidFolder,
                    FindPrefabPathsInFolder,
                    path => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null,
                    out var prefabPaths,
                    out var errorMessage))
            {
                throw new SelectionCaptureException(errorMessage);
            }

            return prefabPaths;
        }

        /// <summary>
        /// 選択候補から直接プレハブと選択フォルダー内のプレハブを解決し、上限内の一覧を返します。
        /// </summary>
        internal static bool TryResolveSelectedPrefabPaths(
            IReadOnlyList<string> selectedAssetPaths,
            Func<string, bool> isFolder,
            Func<string, IReadOnlyList<string>> findPrefabPathsInFolder,
            Func<string, bool> isPrefabAsset,
            out IReadOnlyList<string> prefabPaths,
            out string errorMessage)
        {
            prefabPaths = Array.Empty<string>();
            errorMessage = string.Empty;
            if (selectedAssetPaths == null
                || isFolder == null
                || findPrefabPathsInFolder == null
                || isPrefabAsset == null)
            {
                errorMessage = "選択中のプレハブを取得するための処理を利用できません。";
                return false;
            }

            if (selectedAssetPaths.Count > MaximumSelectedAssetCandidates)
            {
                errorMessage = SelectionCandidateLimitStatusText;
                return false;
            }

            var paths = new SortedSet<string>(StringComparer.Ordinal);
            // 選択元とフォルダー展開結果を合算し、取得処理の反復回数を制限します。
            var candidateCount = selectedAssetPaths.Count;
            try
            {
                for (var selectionIndex = 0; selectionIndex < selectedAssetPaths.Count; selectionIndex++)
                {
                    var selectedPath = NormalizePath(selectedAssetPaths[selectionIndex]);
                    if (!IsAssetsChildPath(selectedPath))
                    {
                        continue;
                    }

                    if (isFolder(selectedPath))
                    {
                        var folderPrefabPaths = findPrefabPathsInFolder(selectedPath)
                            ?? Array.Empty<string>();
                        if (folderPrefabPaths.Count > MaximumSelectedAssetCandidates - candidateCount)
                        {
                            errorMessage = SelectionCandidateLimitStatusText;
                            return false;
                        }

                        candidateCount += folderPrefabPaths.Count;
                        for (var prefabIndex = 0; prefabIndex < folderPrefabPaths.Count; prefabIndex++)
                        {
                            var prefabPath = NormalizePath(folderPrefabPaths[prefabIndex]);
                            if (!TryAddPrefabPath(prefabPath, isPrefabAsset, paths, out errorMessage))
                            {
                                return false;
                            }
                        }

                        continue;
                    }

                    if (!TryAddPrefabPath(selectedPath, isPrefabAsset, paths, out errorMessage))
                    {
                        return false;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                errorMessage = SelectionCaptureFailureStatusText;
                return false;
            }

            prefabPaths = new List<string>(paths);
            return true;
        }

        /// <summary>選択にプレハブまたは検査対象となるフォルダーが含まれるか、展開せずに確認します。</summary>
        internal static bool HasSelectedPrefabCandidate(
            IReadOnlyList<string> selectedAssetPaths,
            Func<string, bool> isFolder,
            Func<string, bool> isPrefabAsset)
        {
            if (selectedAssetPaths == null || isFolder == null || isPrefabAsset == null)
            {
                return false;
            }

            try
            {
                for (var index = 0; index < selectedAssetPaths.Count; index++)
                {
                    var path = NormalizePath(selectedAssetPaths[index]);
                    if (!IsAssetsChildPath(path))
                    {
                        continue;
                    }

                    if (isFolder(path)
                        || (IsPrefabPath(path) && isPrefabAsset(path)))
                    {
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        /// <summary>選択フォルダー内にあるプレハブのパスを取得します。</summary>
        private static IReadOnlyList<string> FindPrefabPathsInFolder(string folderPath)
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            var paths = new string[guids.Length];
            for (var index = 0; index < guids.Length; index++)
            {
                paths[index] = AssetDatabase.GUIDToAssetPath(guids[index]);
            }

            return paths;
        }

        /// <summary>利用可能なプレハブパスを重複なく追加し、解決済み件数の上限を確認します。</summary>
        private static bool TryAddPrefabPath(
            string path,
            Func<string, bool> isPrefabAsset,
            ISet<string> paths,
            out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!IsAssetsChildPath(path)
                || !IsPrefabPath(path)
                || !isPrefabAsset(path))
            {
                return true;
            }

            paths.Add(path);
            if (paths.Count <= MaximumSelectedPrefabs)
            {
                return true;
            }

            paths.Clear();
            errorMessage = SelectedPrefabLimitStatusText;
            return false;
        }

        /// <summary>「Assets」直下より深いアセットパスか確認します。</summary>
        private static bool IsAssetsChildPath(string path)
        {
            return path.StartsWith("Assets/", StringComparison.Ordinal);
        }

        /// <summary>プレハブの拡張子を持つパスか確認します。</summary>
        private static bool IsPrefabPath(string path)
        {
            return path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>パス区切りと末尾区切りを統一します。</summary>
        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        }

        /// <summary>問題のあるプレハブを開き、対象が残っていれば該当ゲームオブジェクトを選択します。</summary>
        internal static bool TryOpenIssue(BuildGuardPrefabScanIssue issue, bool confirmSave)
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(issue.PrefabPath);
            if (prefabAsset == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(issue.TargetGlobalObjectId))
            {
                SelectPrefabFallback(prefabAsset);
                return false;
            }

            if (confirmSave && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return false;
            }

            var stage = PrefabStageUtility.OpenPrefab(issue.PrefabPath);
            if (stage == null)
            {
                SelectPrefabFallback(prefabAsset);
                return false;
            }

            try
            {
                if (!TryGetCurrentStageTarget(issue, out var target))
                {
                    SelectPrefabFallback(prefabAsset);
                    return false;
                }

                Selection.activeGameObject = target;
                EditorGUIUtility.PingObject(target);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SelectPrefabFallback(prefabAsset);
                return false;
            }
        }

        /// <summary>対象プレハブを開き、指定ゲームオブジェクトから欠落スクリプトだけを除去します。</summary>
        internal static bool TryRemoveMissingScripts(
            BuildGuardPrefabScanIssue issue,
            bool confirmSave,
            bool confirmRemoval,
            out int removedCount)
        {
            removedCount = 0;
            if (issue.Kind != BuildGuardIssueKind.MissingScript)
            {
                return false;
            }

            if (confirmRemoval && !EditorUtility.DisplayDialog(
                RemovalDialogTitleText,
                FormatRemovalDialogMessage(issue),
                RemoveButtonText,
                RemovalCancelButtonText))
            {
                return false;
            }

            if (!TryOpenIssue(issue, confirmSave))
            {
                return false;
            }

            GameObject target;
            try
            {
                if (!TryGetCurrentStageTarget(issue, out target))
                {
                    SelectPrefabFallback(AssetDatabase.LoadAssetAtPath<GameObject>(issue.PrefabPath));
                    return false;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SelectPrefabFallback(AssetDatabase.LoadAssetAtPath<GameObject>(issue.PrefabPath));
                return false;
            }

            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(target) == 0)
            {
                SelectPrefabFallback(AssetDatabase.LoadAssetAtPath<GameObject>(issue.PrefabPath));
                return false;
            }

            Selection.activeGameObject = target;
            Undo.RegisterFullObjectHierarchyUndo(target, RemovalUndoName);
            removedCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target);
            if (removedCount == 0)
            {
                return false;
            }

            EditorSceneManager.MarkSceneDirty(target.scene);
            EditorGUIUtility.PingObject(target);
            return true;
        }

        /// <summary>開いているプレハブを再検査し、記録時と同じ問題が残る対象だけを返します。</summary>
        private static bool TryGetCurrentStageTarget(
            BuildGuardPrefabScanIssue issue,
            out GameObject target)
        {
            target = null;
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            return stage != null
                && string.Equals(
                    NormalizePath(stage.assetPath),
                    NormalizePath(issue.PrefabPath),
                    StringComparison.Ordinal)
                && BuildGuardPrefabScanner.TryFindCurrentTarget(stage.scene, issue, out target);
        }

        /// <summary>古い検査結果では変更せず、現在のプレハブアセットだけを退避選択します。</summary>
        private static void SelectPrefabFallback(GameObject prefabAsset)
        {
            Selection.activeObject = prefabAsset;
            if (prefabAsset != null)
            {
                EditorGUIUtility.PingObject(prefabAsset);
            }
        }

        /// <summary>選択取得の失敗時に、記録済み対象と古い問題をすべて破棄します。</summary>
        private void SetSelectionCaptureFailure(string statusText)
        {
            _prefabPaths = Array.Empty<string>();
            _issues.Clear();
            _scrollPosition = Vector2.zero;
            _scanFailed = true;
            _statusText = statusText;
            Repaint();
        }

        /// <summary>中止可能な進捗表示を使って、記録済みのプレハブを検査します。</summary>
        private void ScanWithProgress()
        {
            try
            {
                RunScan((index, total, path) => EditorUtility.DisplayCancelableProgressBar(
                    ProgressTitleText,
                    FormatProgressText(index, total, path),
                    total == 0 ? 0f : (float)index / total));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>問題を1件描画します。</summary>
        private void DrawIssue(int index, BuildGuardPrefabScanIssue issue)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"{index + 1}. {FormatKind(issue.Kind)}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"プレハブ: {issue.PrefabPath}", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField($"ゲームオブジェクト: {issue.HierarchyPath}", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField($"詳細: {FormatDetails(issue)}", EditorStyles.wordWrappedLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(OpenButtonText, GUILayout.Width(120f)))
                    {
                        TryOpenIssue(issue, true);
                    }

                    if (GUILayout.Button(CopyButtonText, GUILayout.Width(104f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = FormatClipboardText(issue);
                    }

                    if (issue.Kind == BuildGuardIssueKind.MissingScript
                        && GUILayout.Button(RemoveButtonText, GUILayout.Width(120f)))
                    {
                        RemoveMissingScripts(index, issue);
                    }

                    GUILayout.FlexibleSpace();
                }
            }
        }

        /// <summary>画面上の問題を修復し、結果一覧と状態文を更新します。</summary>
        private void RemoveMissingScripts(int index, BuildGuardPrefabScanIssue issue)
        {
            if (!TryRemoveMissingScripts(issue, true, true, out var removedCount))
            {
                return;
            }

            _issues.RemoveAt(index);
            _statusText = FormatRemovalStatus(removedCount);
            Repaint();
            GUIUtility.ExitGUI();
        }

        /// <summary>検査結果に対応する日本語の状態文を作ります。</summary>
        private static string FormatStatus(BuildGuardPrefabScanResult result)
        {
            if (result.Cancelled)
            {
                return $"プレハブを{result.ScannedPrefabCount}件検査した時点で中止しました。{result.Issues.Count}件の問題を保持しています。";
            }

            return result.Issues.Count == 0
                ? $"プレハブを{result.ScannedPrefabCount}件検査しました。欠落参照は見つかりませんでした。"
                : $"プレハブを{result.ScannedPrefabCount}件検査し、{result.Issues.Count}件の問題を見つけました。";
        }

        /// <summary>欠落スクリプト除去後の状態文を作ります。</summary>
        private static string FormatRemovalStatus(int removedCount)
        {
            return $"欠落スクリプトを{removedCount}件除去しました。自動保存の設定によって保存状態が異なります。"
                + "プレハブ編集画面を確認し、必要に応じて保存するか元に戻してください。";
        }

        /// <summary>欠落スクリプト除去前の確認文を作ります。</summary>
        private static string FormatRemovalDialogMessage(BuildGuardPrefabScanIssue issue)
        {
            return $"「{issue.PrefabPath}」を開き、「{issue.HierarchyPath}」から欠落スクリプトをすべて除去しますか？\n\n"
                + "自動保存が有効な場合は変更が保存され、無効な場合は未保存のままです。"
                + "除去後に保存状態を確認し、必要に応じて保存するか元に戻してください。";
        }

        /// <summary>進捗画面へ表示する検査中のプレハブ名と件数を作ります。</summary>
        private static string FormatProgressText(int index, int total, string path)
        {
            return $"{Path.GetFileNameWithoutExtension(path)}を検査中 ({index + 1}/{total})";
        }

        /// <summary>現在の状態に対応する案内の種類を返します。</summary>
        private MessageType GetStatusMessageType()
        {
            return _scanFailed
                ? MessageType.Error
                : _issues.Count == 0 ? MessageType.Info : MessageType.Warning;
        }

        /// <summary>問題の種類を日本語で返します。</summary>
        private static string FormatKind(BuildGuardIssueKind kind)
        {
            return kind == BuildGuardIssueKind.MissingScript
                ? "欠落スクリプト"
                : "欠落オブジェクト参照";
        }

        /// <summary>問題の詳細を画面表示用の日本語へ整えます。</summary>
        private static string FormatDetails(BuildGuardPrefabScanIssue issue)
        {
            if (issue.Kind != BuildGuardIssueKind.MissingScript)
            {
                return issue.Details ?? string.Empty;
            }

            var details = issue.Details ?? string.Empty;
            var separatorIndex = details.LastIndexOf(':');
            return separatorIndex < 0
                ? "欠落スクリプト"
                : $"欠落スクリプト: {details.Substring(separatorIndex + 1).Trim()}";
        }

        /// <summary>問題1件を共有できる1行の日本語文へ整えます。</summary>
        private static string FormatClipboardText(BuildGuardPrefabScanIssue issue)
        {
            return $"{FormatKind(issue.Kind)} | プレハブ: {issue.PrefabPath} | ゲームオブジェクト: {issue.HierarchyPath} | 詳細: {FormatDetails(issue)}";
        }

        /// <summary>利用者へそのまま案内できる、想定内の選択取得失敗を表します。</summary>
        internal sealed class SelectionCaptureException : InvalidOperationException
        {
            /// <summary>状態欄へ表示する日本語の失敗理由から例外を作成します。</summary>
            internal SelectionCaptureException(string message)
                : base(message)
            {
            }
        }
    }
}
