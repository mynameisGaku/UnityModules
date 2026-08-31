// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BuildGuard.Editor
{
    /// <summary>
    /// ビルド対象として有効なシーンから、確認専用のプレハブ構造差分を表示します。
    /// </summary>
    internal sealed class BuildGuardPrefabOverrideReviewWindow : EditorWindow
    {
        /// <summary>ツールメニューに登録する経路です。</summary>
        internal const string MenuPath = "Tools/ビルドガード/プレハブ構造差分を確認";

        /// <summary>画面に保持して表示する差分の最大件数です。</summary>
        internal const int MaximumDisplayedFindings = 1000;

        /// <summary>画面のタイトルです。</summary>
        private const string WindowTitleText = "ビルドガード - プレハブ構造差分";

        /// <summary>画面上部の見出しです。</summary>
        private const string HeadingText = "ビルド対象シーンのプレハブ構造差分";

        /// <summary>検査内容とビルドへの影響を説明する案内文です。</summary>
        private const string DescriptionText =
            "追加または削除されたプレハブ内のゲームオブジェクトとコンポーネントを表示します。プロパティ値の変更は対象外で、結果がプレイヤービルドを停止することはありません。";

        /// <summary>検査を開始するボタンの文言です。</summary>
        private const string ScanButtonText = "更新して検査";

        /// <summary>検査結果を消去するボタンの文言です。</summary>
        private const string ClearButtonText = "結果を消去";

        /// <summary>対象を開いて選択するボタンの文言です。</summary>
        private const string LocateButtonText = "開いて選択";

        /// <summary>差分内容を複写するボタンの文言です。</summary>
        private const string CopyButtonText = "内容をコピー";

        /// <summary>画面を開いた直後の案内文です。</summary>
        private const string InitialStatusText =
            "「更新して検査」を押すと、ビルド対象として有効なシーンのプレハブ構造差分を確認できます。";

        /// <summary>進捗画面のタイトルです。</summary>
        private const string ProgressTitleText = "ビルドガード - プレハブ構造差分";

        /// <summary>日本語の案内と操作を欠けずに表示する画面の最小寸法です。</summary>
        private static readonly Vector2 MinimumWindowSize = new Vector2(760f, 360f);

        /// <summary>検査開始ボタンの表示内容です。</summary>
        private static readonly GUIContent ScanButtonContent = new GUIContent(ScanButtonText);

        /// <summary>結果消去ボタンの表示内容です。</summary>
        private static readonly GUIContent ClearButtonContent = new GUIContent(ClearButtonText);

        /// <summary>現在表示している構造差分の一覧です。</summary>
        private readonly List<BuildGuardPrefabOverrideFinding> _findings =
            new List<BuildGuardPrefabOverrideFinding>();

        /// <summary>現在表示しているシーン検査失敗の一覧です。</summary>
        private readonly List<BuildGuardPrefabOverrideReviewFailure> _failures =
            new List<BuildGuardPrefabOverrideReviewFailure>();

        /// <summary>結果一覧の現在のスクロール位置です。</summary>
        private Vector2 _scrollPosition;

        /// <summary>画面の状態欄へ表示する案内文です。</summary>
        private string _statusText = InitialStatusText;

        /// <summary>状態欄へ表示する案内の重要度です。</summary>
        private MessageType _statusMessageType = MessageType.Info;

        /// <summary>現在表示している構造差分の件数を返します。</summary>
        internal int FindingCount => _findings.Count;

        /// <summary>現在表示している検査失敗の件数を返します。</summary>
        internal int FailureCount => _failures.Count;

        /// <summary>現在の状態文を返します。</summary>
        internal string StatusText => _statusText;

        /// <summary>ツールメニューからプレハブ構造差分画面を開きます。</summary>
        [MenuItem(MenuPath, priority = 2002)]
        private static void ShowWindow()
        {
            var window = GetWindow<BuildGuardPrefabOverrideReviewWindow>();
            window.titleContent = new GUIContent(WindowTitleText);
            window.minSize = MinimumWindowSize;
            window.Show();
        }

        /// <summary>プレハブ構造差分画面を描画します。</summary>
        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(HeadingText, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(DescriptionText, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(ScanButtonContent, GUILayout.Height(28f)))
                {
                    ScanWithProgress();
                }

                using (new EditorGUI.DisabledScope(_findings.Count == 0 && _failures.Count == 0))
                {
                    if (GUILayout.Button(ClearButtonContent, GUILayout.Width(100f), GUILayout.Height(28f)))
                    {
                        ClearResults();
                    }
                }
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(_statusText, _statusMessageType);
            EditorGUILayout.Space(4f);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            for (var failureIndex = 0; failureIndex < _failures.Count; failureIndex++)
            {
                DrawFailure(_failures[failureIndex]);
            }

            for (var findingIndex = 0; findingIndex < _findings.Count; findingIndex++)
            {
                DrawFinding(findingIndex, _findings[findingIndex]);
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>画面の状態を、途中結果を含まない1回分の検査結果へ置き換えます。</summary>
        internal void RunScan(Func<int, int, string, bool> shouldCancel = null)
        {
            var scenePaths = BuildGuardManualScanner.GetEnabledBuildScenePaths();
            _findings.Clear();
            _failures.Clear();
            _scrollPosition = Vector2.zero;

            if (scenePaths.Count == 0)
            {
                _statusText = "現在のビルドプロファイルに、有効なビルド対象シーンがありません。";
                _statusMessageType = MessageType.Info;
                Repaint();
                return;
            }

            var result = BuildGuardPrefabOverrideReviewService.Scan(
                scenePaths,
                MaximumDisplayedFindings,
                shouldCancel);
            if (result.Succeeded)
            {
                _findings.AddRange(result.Findings);
                _statusText = FormatSuccessStatus(result);
                _statusMessageType = result.Findings.Count == 0
                    ? MessageType.Info
                    : MessageType.Warning;
            }
            else if (result.Cancelled)
            {
                _statusText = $"シーンを{result.ScannedSceneCount}件検査した時点で中止しました。途中結果は破棄しました。";
                _statusMessageType = MessageType.Warning;
            }
            else
            {
                _failures.AddRange(result.Failures);
                _statusText = $"シーンを{result.ScannedSceneCount}件検査した時点で失敗しました。途中結果は破棄しました。";
                _statusMessageType = MessageType.Error;
            }

            Repaint();
        }

        /// <summary>プロジェクトやシーンを変更せず、現在の検査結果だけを消去します。</summary>
        internal void ClearResults()
        {
            _findings.Clear();
            _failures.Clear();
            _scrollPosition = Vector2.zero;
            _statusText = "結果を消去しました。「更新して検査」を押すと、最新の結果を作成できます。";
            _statusMessageType = MessageType.Info;
            Repaint();
        }

        /// <summary>1件の差分を再検査してから、安全に対象へ移動します。</summary>
        internal BuildGuardPrefabOverrideNavigationOutcome LocateFinding(int index)
        {
            if (index < 0 || index >= _findings.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var outcome = BuildGuardPrefabOverrideReviewService.Locate(
                _findings[index],
                out _statusText);
            _statusMessageType = outcome == BuildGuardPrefabOverrideNavigationOutcome.SelectedSceneObject
                || outcome == BuildGuardPrefabOverrideNavigationOutcome.PingedSceneAsset
                ? MessageType.Info
                : outcome == BuildGuardPrefabOverrideNavigationOutcome.Stale
                    ? MessageType.Warning
                    : MessageType.Error;
            Repaint();
            return outcome;
        }

        /// <summary>指定位置に保持している構造差分を返します。</summary>
        internal BuildGuardPrefabOverrideFinding GetFinding(int index)
        {
            return _findings[index];
        }

        /// <summary>中止可能な進捗表示を使って検査を実行します。</summary>
        private void ScanWithProgress()
        {
            try
            {
                RunScan((index, total, scenePath) => EditorUtility.DisplayCancelableProgressBar(
                    ProgressTitleText,
                    $"{Path.GetFileNameWithoutExtension(scenePath)} を検査中（{index + 1}/{total}）",
                    total == 0 ? 0f : (float)index / total));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>1件の構造差分と、その操作ボタンを描画します。</summary>
        private void DrawFinding(int index, BuildGuardPrefabOverrideFinding finding)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    $"{index + 1}. {BuildGuardPrefabOverrideReviewPresentation.FormatKind(finding.Kind)}",
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"シーン: {finding.ScenePath}", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField(
                    $"プレハブの実体: {finding.InstanceRootHierarchyPath}",
                    EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField(
                    $"対象パス: {finding.TargetHierarchyPath}",
                    EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField(
                    $"コンポーネント: {BuildGuardPrefabOverrideReviewPresentation.FormatComponent(finding)}",
                    EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField(
                    $"参照元: {BuildGuardPrefabOverrideReviewPresentation.FormatSource(finding)}",
                    EditorStyles.wordWrappedLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(LocateButtonText, GUILayout.Width(118f)))
                    {
                        LocateFinding(index);
                    }

                    if (GUILayout.Button(CopyButtonText, GUILayout.Width(100f)))
                    {
                        EditorGUIUtility.systemCopyBuffer =
                            BuildGuardPrefabOverrideReviewPresentation.FormatClipboardText(finding);
                    }

                    GUILayout.FlexibleSpace();
                }
            }
        }

        /// <summary>1件のシーン検査失敗を日本語の項目名で描画します。</summary>
        private static void DrawFailure(BuildGuardPrefabOverrideReviewFailure failure)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("シーンの検査に失敗しました", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"シーン: {failure.ScenePath}", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField(
                    $"原因: {BuildGuardPrefabOverrideReviewPresentation.FormatScanError(failure.Error)}",
                    EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField($"詳細: {failure.Message}", EditorStyles.wordWrappedLabel);
            }
        }

        /// <summary>成功した検査の件数と表示上限を日本語の状態文へ整形します。</summary>
        private static string FormatSuccessStatus(BuildGuardPrefabOverrideReviewScanResult result)
        {
            if (result.TotalFindingCount == 0)
            {
                return $"シーンを{result.ScannedSceneCount}件検査しました。プレハブ構造差分は見つかりませんでした。";
            }

            return result.WasTruncated
                ? $"シーンを{result.ScannedSceneCount}件検査し、{result.TotalFindingCount}件のプレハブ構造差分を見つけました。先頭{result.Findings.Count}件を表示します。"
                : $"シーンを{result.ScannedSceneCount}件検査し、{result.TotalFindingCount}件のプレハブ構造差分を見つけました。";
        }
    }
}
