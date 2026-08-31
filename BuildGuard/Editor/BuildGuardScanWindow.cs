// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildGuard.Editor
{
    /// <summary>
    /// 有効なビルド対象シーンまたは記録済みの選択シーンから、修復可能な問題を表示します。
    /// </summary>
    internal sealed class BuildGuardScanWindow : EditorWindow
    {
        private const string ToolMenuPath = "Tools/ビルドガード/ビルド対象シーンを検査";
        private const string AssetMenuPath = "Assets/ビルドガード/選択シーンを検査";

        private static readonly GUIContent ScanBuildScenesButtonContent = new GUIContent("ビルド対象シーンを検査");
        private static readonly GUIContent ScanSelectedScenesButtonContent = new GUIContent("選択シーンを検査");
        private static readonly GUIContent ClearButtonContent = new GUIContent("結果を消去");

        private readonly List<BuildGuardScanIssue> _issues = new List<BuildGuardScanIssue>();
        private string[] _selectedScenePaths = Array.Empty<string>();
        private Vector2 _scrollPosition;
        private bool _scanFailed;
        private string _statusText = "「ビルド対象シーンを検査」を押して、現在のビルドプロファイルを確認してください。";

        /// <summary>再現可能な編集モード試験のため、現在の問題件数を返します。</summary>
        internal int IssueCount => _issues.Count;

        /// <summary>再現可能な編集モード試験のため、現在の状態文を返します。</summary>
        internal string StatusText => _statusText;

        /// <summary>再現可能な編集モード試験のため、記録済みの選択シーン数を返します。</summary>
        internal int SelectedSceneCount => _selectedScenePaths.Length;

        /// <summary>Unityのツールメニューから手動検査画面を開きます。</summary>
        [MenuItem(ToolMenuPath, priority = 2000)]
        private static void ShowFromTools()
        {
            ShowWindow();
        }

        /// <summary>同じ画面を開き、直接選択されているシーンアセットを記録します。</summary>
        [MenuItem(AssetMenuPath, false, 2000)]
        private static void ShowFromAssets()
        {
            var window = ShowWindow();
            window.CaptureSelectedScenes();
        }

        /// <summary>選択中のシーンを1件以上記録できる場合だけ、アセットメニューを有効にします。</summary>
        [MenuItem(AssetMenuPath, true)]
        private static bool ValidateShowFromAssets()
        {
            return BuildGuardManualScanner.TryGetSelectedScenePaths(out var paths, out _)
                && paths.Count > 0;
        }

        /// <summary>シーン検査画面を作成するか、既存の画面を再利用します。</summary>
        private static BuildGuardScanWindow ShowWindow()
        {
            var window = GetWindow<BuildGuardScanWindow>();
            window.titleContent = new GUIContent("ビルドガード");
            window.minSize = new Vector2(720f, 320f);
            window.Show();
            return window;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("ビルド対象・選択シーンの参照検査", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "有効なビルド対象シーン、または記録済みの選択シーンを検査します。検査だけではシーンを変更したり保存したりしません。",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(ScanBuildScenesButtonContent, GUILayout.Height(28f)))
                {
                    ScanBuildScenesWithProgress();
                }

                using (new EditorGUI.DisabledScope(_issues.Count == 0))
                {
                    if (GUILayout.Button(ClearButtonContent, GUILayout.Width(104f), GUILayout.Height(28f)))
                    {
                        ClearResults();
                    }
                }
            }

            EditorGUILayout.LabelField("記録済みの選択シーン", $"{_selectedScenePaths.Length}件");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("現在の選択を使用", GUILayout.Height(28f)))
                {
                    CaptureSelectedScenes();
                }

                using (new EditorGUI.DisabledScope(_selectedScenePaths.Length == 0))
                {
                    if (GUILayout.Button(ScanSelectedScenesButtonContent, GUILayout.Height(28f)))
                    {
                        ScanSelectedScenesWithProgress();
                    }
                }
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(_statusText, GetStatusMessageType());
            EditorGUILayout.Space(4f);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            for (var index = 0; index < _issues.Count; index++)
            {
                DrawIssue(index, _issues[index]);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawIssue(int index, BuildGuardScanIssue issue)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"{index + 1}. {FormatKind(issue.Kind)}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"シーン: {issue.ScenePath}", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField($"ゲームオブジェクト: {issue.HierarchyPath}", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField($"詳細: {issue.Details}", EditorStyles.wordWrappedLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("シーンを開く", GUILayout.Width(120f)))
                    {
                        TryOpenIssue(issue, true);
                    }

                    if (GUILayout.Button("内容をコピー", GUILayout.Width(104f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = FormatClipboardText(issue);
                    }

                    if (issue.Kind == BuildGuardIssueKind.MissingScript
                        && GUILayout.Button("開いて除去", GUILayout.Width(120f)))
                    {
                        RemoveMissingScripts(index, issue);
                    }

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void ScanBuildScenesWithProgress()
        {
            try
            {
                RunScan((index, total, scenePath) => EditorUtility.DisplayCancelableProgressBar(
                    "ビルドガード",
                    $"{Path.GetFileNameWithoutExtension(scenePath)}を検査中 ({index + 1}/{total})",
                    total == 0 ? 0f : (float)index / total));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>中止可能な進捗表示を使って、記録済みの選択シーンを検査します。</summary>
        private void ScanSelectedScenesWithProgress()
        {
            try
            {
                RunSelectedScan((index, total, scenePath) => EditorUtility.DisplayCancelableProgressBar(
                    "ビルドガード",
                    $"{Path.GetFileNameWithoutExtension(scenePath)}を検査中 ({index + 1}/{total})",
                    total == 0 ? 0f : (float)index / total));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>手動検査を実行し、画面に保持している結果を置き換えます。</summary>
        internal void RunScan(Func<int, int, string, bool> shouldCancel = null)
        {
            var scenePaths = BuildGuardManualScanner.GetEnabledBuildScenePaths();
            if (scenePaths.Count == 0)
            {
                _issues.Clear();
                _scanFailed = false;
                _statusText = "現在のビルドプロファイルに、有効なシーンが設定されていません。";
                Repaint();
                return;
            }

            var result = BuildGuardManualScanner.Scan(scenePaths, shouldCancel);
            _issues.Clear();
            _issues.AddRange(result.Issues);
            _scanFailed = false;
            _statusText = FormatStatus(result);
            Repaint();
        }

        /// <summary>現在直接選択されているシーンアセットを記録し、古い問題一覧を消去します。</summary>
        internal void CaptureSelectedScenes()
        {
            _issues.Clear();
            _scrollPosition = Vector2.zero;
            if (!BuildGuardManualScanner.TryGetSelectedScenePaths(out var paths, out var errorMessage))
            {
                _selectedScenePaths = Array.Empty<string>();
                _scanFailed = true;
                _statusText = errorMessage;
                Repaint();
                return;
            }

            _selectedScenePaths = new string[paths.Count];
            for (var index = 0; index < paths.Count; index++)
            {
                _selectedScenePaths[index] = paths[index];
            }

            _scanFailed = false;
            _statusText = _selectedScenePaths.Length == 0
                ? "プロジェクトウィンドウでシーンアセットを1件以上選択してください。フォルダーとシーン以外のアセットは無視されます。"
                : $"選択中のシーンアセットを{_selectedScenePaths.Length}件記録しました。";
            Repaint();
        }

        /// <summary>記録済みのシーン一覧を検査するか、その一覧が古くなったことを報告します。</summary>
        internal void RunSelectedScan(Func<int, int, string, bool> shouldCancel = null)
        {
            _issues.Clear();
            if (!BuildGuardManualScanner.TryScanSelectedScenes(
                    _selectedScenePaths,
                    shouldCancel,
                    out var result,
                    out var errorMessage))
            {
                _scanFailed = true;
                _statusText = errorMessage;
                Repaint();
                return;
            }

            _issues.AddRange(result.Issues);
            _scanFailed = false;
            _statusText = FormatSelectedStatus(result);
            Repaint();
        }

        /// <summary>プロジェクトとシーンの状態を変えずに、問題一覧だけを消去します。</summary>
        internal void ClearResults()
        {
            _issues.Clear();
            _scrollPosition = Vector2.zero;
            _scanFailed = false;
            _statusText = "結果を消去しました。ビルド対象シーン、または記録済みの選択シーンを再度検査してください。";
            Repaint();
        }

        /// <summary>問題のあるシーンを開き、対象が残っていれば該当ゲームオブジェクトを選択します。</summary>
        internal static bool TryOpenIssue(BuildGuardScanIssue issue, bool confirmSave)
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(issue.ScenePath);
            if (sceneAsset == null)
            {
                return false;
            }

            if (confirmSave && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return false;
            }

            var scene = SceneManager.GetSceneByPath(issue.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(issue.ScenePath, OpenSceneMode.Single);
            }
            else
            {
                SceneManager.SetActiveScene(scene);
            }

            var target = BuildGuardHierarchyPath.Find(scene, issue.HierarchyPath);
            if (target != null)
            {
                Selection.activeGameObject = target;
                EditorGUIUtility.PingObject(target);
                return true;
            }

            Selection.activeObject = sceneAsset;
            EditorGUIUtility.PingObject(sceneAsset);
            return true;
        }

        /// <summary>
        /// 欠落スクリプトがあるシーンを開き、対象ゲームオブジェクトから欠落分だけを除去します。
        /// </summary>
        internal static bool TryRemoveMissingScripts(
            BuildGuardScanIssue issue,
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
                "欠落スクリプトを除去",
                $"「{issue.ScenePath}」を開き、「{issue.HierarchyPath}」から欠落スクリプトをすべて除去しますか？\n\nシーンは保存されません。変更内容を確認し、必要に応じて元に戻せます。",
                "開いて除去",
                "キャンセル"))
            {
                return false;
            }

            if (!TryOpenIssue(issue, confirmSave))
            {
                return false;
            }

            var target = Selection.activeGameObject;
            if (target == null || GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(target) == 0)
            {
                return false;
            }

            Undo.RegisterFullObjectHierarchyUndo(target, "欠落スクリプトを除去");
            removedCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target);
            if (removedCount == 0)
            {
                return false;
            }

            EditorSceneManager.MarkSceneDirty(target.scene);
            EditorGUIUtility.PingObject(target);
            return true;
        }

        private void RemoveMissingScripts(int index, BuildGuardScanIssue issue)
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

        /// <summary>欠落スクリプトの除去後に表示する状態文を作ります。</summary>
        private static string FormatRemovalStatus(int removedCount)
        {
            return $"欠落スクリプトを{removedCount}件除去しました。未保存のシーンを確認し、保存するか元に戻してください。";
        }

        private static string FormatStatus(BuildGuardManualScanResult result)
        {
            if (result.Cancelled)
            {
                return $"シーンを{result.ScannedSceneCount}件検査した時点で中止しました。{result.Issues.Count}件の問題を保持しています。";
            }

            return result.Issues.Count == 0
                ? $"シーンを{result.ScannedSceneCount}件検査しました。欠落参照は見つかりませんでした。"
                : $"シーンを{result.ScannedSceneCount}件検査し、{result.Issues.Count}件の問題を見つけました。";
        }

        /// <summary>ビルド対象シーンと区別できるよう、選択シーン向けの状態文を作ります。</summary>
        private static string FormatSelectedStatus(BuildGuardManualScanResult result)
        {
            if (result.Cancelled)
            {
                return $"選択シーンを{result.ScannedSceneCount}件検査した時点で中止しました。{result.Issues.Count}件の問題を保持しています。";
            }

            return result.Issues.Count == 0
                ? $"選択シーンを{result.ScannedSceneCount}件検査しました。欠落参照は見つかりませんでした。"
                : $"選択シーンを{result.ScannedSceneCount}件検査し、{result.Issues.Count}件の問題を見つけました。";
        }

        private MessageType GetStatusMessageType()
        {
            return _scanFailed
                ? MessageType.Error
                : _issues.Count == 0 ? MessageType.Info : MessageType.Warning;
        }

        private static string FormatKind(BuildGuardIssueKind kind)
        {
            return kind == BuildGuardIssueKind.MissingScript
                ? "欠落スクリプト"
                : "欠落オブジェクト参照";
        }

        private static string FormatClipboardText(BuildGuardScanIssue issue)
        {
            return $"{FormatKind(issue.Kind)} | シーン: {issue.ScenePath} | ゲームオブジェクト: {issue.HierarchyPath} | 詳細: {issue.Details}";
        }
    }
}
