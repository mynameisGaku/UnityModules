using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;

namespace BuildAssistant.Editor
{
    /// <summary>安全なデスクトップ向けビルドを、上から順に確認して一度だけ実行する画面です。</summary>
    internal sealed class BuildAssistantWindow : EditorWindow
    {
        internal const string WindowTitle = "ビルド実行アシスタント";
        internal const string ProfileHeading = "\u2460 ビルド設定";
        internal const string OutputHeading = "\u2461 出力先";
        internal const string PreviewHeading = "\u2462 計画作成";
        internal const string ConfirmHeading = "\u2463 実行確認";
        internal const string BuildHeading = "\u2464 実行結果と書き出し";
        internal const string OutputHelpText = "Assets、Packages、ProjectSettings、Library、Temp、Logs、objの外にある、ローカルドライブの絶対フォルダーを指定してください。UNC、ネットワーク、割り当てドライブは使えません。既存フォルダー、または既存フォルダー直下の未作成フォルダー一つだけを指定できます。";
        internal const string EditorTargetLabel = "エディターで選択中の対象機種";
        internal const string ProfileHelpText = "ここにはUnityエディターの対象機種を表示します。独自のビルドプロファイルを使う場合は、そのプロファイルと対象機種・種別が一致している必要があります。一致しない場合はUnityのビルドプロファイル画面で切り替え、コンパイル完了後に計画を作り直してください。この画面は設定を切り替えません。";
        internal const string InputFingerprintLabel = "取得した入力照合値";
        internal const int SectionCardCount = 5;
        internal const float MinimumWidth = 620f;
        internal const float MinimumHeight = 480f;
        internal const float SectionCardSpacing = 6f;

        [SerializeField] private string _outputRoot = string.Empty;
        private BuildAssistantPresenter _presenter;
        private Vector2 _scrollPosition;
        private bool _showScenes = true;
        private bool _showDefines;
        private bool _showLargestAssets = true;
        // 同じ描画中に重ねてビルド要求を登録しないための状態です。
        private bool _buildQueued;
        private GUIStyle _wordWrapStyle;

        /// <summary>ビルド実行アシスタントを開くか、既存の画面へ移動します。</summary>
        internal static void Open()
        {
            GetWindow<BuildAssistantWindow>(WindowTitle);
        }

        private void OnEnable()
        {
            minSize = new Vector2(MinimumWidth, MinimumHeight);
            EnsurePresenter();
            _presenter.SetOutputRoot(_outputRoot);
            _presenter.RefreshHistory();
        }

        /// <summary>画面を閉じた場合は、まだ開始していないビルド要求を取り消します。</summary>
        private void OnDisable()
        {
            EditorApplication.delayCall -= ExecuteQueuedBuild;
            _buildQueued = false;
        }

        private void OnGUI()
        {
            EnsurePresenter();
            EnsureStyles();
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollPosition))
            {
                _scrollPosition = scroll.scrollPosition;
                EditorGUILayout.HelpBox("上から順に確認してください。計画作成時にビルド入力を取得し、その計画を明示的に確認するまで実行ボタンは有効になりません。", MessageType.Info);
                for (var sectionIndex = 0; sectionIndex < SectionCardCount; sectionIndex++)
                    DrawSection(sectionIndex);
                EditorGUILayout.Space(8f);
            }
        }

        internal static string GetSectionHeading(int sectionIndex)
        {
            switch (sectionIndex)
            {
                case 0:
                    return ProfileHeading;
                case 1:
                    return OutputHeading;
                case 2:
                    return PreviewHeading;
                case 3:
                    return ConfirmHeading;
                case 4:
                    return BuildHeading;
                default:
                    throw new ArgumentOutOfRangeException(nameof(sectionIndex));
            }
        }

        private void DrawSection(int sectionIndex)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawHeading(GetSectionHeading(sectionIndex));
                switch (sectionIndex)
                {
                    case 0:
                        DrawProfile();
                        break;
                    case 1:
                        DrawOutput();
                        break;
                    case 2:
                        DrawPreview();
                        break;
                    case 3:
                        DrawConfirmation();
                        break;
                    case 4:
                        DrawBuildResultAndExport();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(sectionIndex));
                }
            }
            EditorGUILayout.Space(SectionCardSpacing);
        }

        private void DrawProfile()
        {
            var activeProfile = BuildProfile.GetActiveBuildProfile();
            DrawValue("有効なビルドプロファイル", activeProfile == null ? "プラットフォーム設定" : activeProfile.name);
            DrawValue(EditorTargetLabel, BuildAssistantPresenter.FormatTarget(EditorUserBuildSettings.activeBuildTarget));
            EditorGUILayout.HelpBox(ProfileHelpText, MessageType.None);
            if (GUILayout.Button("ビルドプロファイルを開く", GUILayout.Width(190f)))
            {
                if (!EditorApplication.ExecuteMenuItem("File/Build Profiles..."))
                    EditorApplication.ExecuteMenuItem("File/Build Profiles");
            }
        }

        private void DrawOutput()
        {
            EditorGUI.BeginChangeCheck();
            var nextOutputRoot = EditorGUILayout.TextField("出力先ルート", _outputRoot);
            if (EditorGUI.EndChangeCheck())
                SetOutputRoot(nextOutputRoot);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("参照", GUILayout.Width(90f)))
                {
                    var selected = EditorUtility.OpenFolderPanel("ビルド出力先ルートを選択", GetBrowseStartDirectory(), string.Empty);
                    if (!string.IsNullOrEmpty(selected))
                        SetOutputRoot(selected);
                }
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.HelpBox(OutputHelpText, MessageType.None);
        }

        private void DrawPreview()
        {
            if (GUILayout.Button("ビルド計画を作成", GUILayout.Height(26f)))
            {
                GUI.FocusControl(null);
                _presenter.Preview();
            }

            if (_presenter.Plan != null)
                EditorGUILayout.HelpBox(_presenter.Message, _presenter.Plan.IsReady ? MessageType.Info : MessageType.Error);
        }

        private void DrawConfirmation()
        {
            var plan = _presenter.Plan;
            if (plan == null)
            {
                EditorGUILayout.HelpBox("実行確認の前に、準備済みの計画をここへ表示します。", MessageType.None);
                return;
            }
            if (!plan.IsReady)
            {
                EditorGUILayout.HelpBox("上に表示された計画の問題を直し、新しい計画を作成してください。", MessageType.Warning);
                return;
            }

            DrawValue("ビルドプロファイル", plan.ProfileName + "（" + BuildAssistantPresenter.FormatProfileKind(plan.ProfileKind) + "）");
            DrawValue(InputFingerprintLabel, plan.ProfileDependencyHash);
            DrawValue("対象機種", BuildAssistantPresenter.FormatTarget(plan.Target));
            DrawValue("コード生成方式", BuildAssistantPresenter.FormatScriptingBackend(plan.ScriptingBackend));
            DrawValue("ビルド選択肢", BuildAssistantPresenter.FormatBuildOptions(plan.Options));
            DrawValue("有効なシーン", plan.Scenes.Count(scene => scene.Enabled) + " / " + plan.Scenes.Count);
            DrawValue("出力先ルート", plan.OutputRoot);
            DrawValue("今回の実行フォルダー", plan.RunDirectory);
            DrawValue("プレイヤー出力", plan.ArtifactPath);
            if (plan.PreviousComparableSuccess != null)
                DrawValue("比較元", plan.PreviousComparableSuccess.RunId);

            _showScenes = EditorGUILayout.Foldout(_showScenes, "取得したシーン", true);
            if (_showScenes)
            {
                foreach (var scene in plan.Scenes)
                    EditorGUILayout.LabelField((scene.Enabled ? "［対象］" : "［除外］") + scene.AssetPath, _wordWrapStyle);
            }

            _showDefines = EditorGUILayout.Foldout(_showDefines, "有効なコンパイル記号", true);
            if (_showDefines)
                EditorGUILayout.LabelField(plan.EffectiveDefines.Count == 0 ? "なし" : string.Join("、", plan.EffectiveDefines), _wordWrapStyle);

            EditorGUILayout.Space(4f);
            var confirmed = EditorGUILayout.ToggleLeft("ビルド設定、シーン、選択肢、出力先を確認しました。", _presenter.ConfirmationAccepted);
            if (confirmed != _presenter.ConfirmationAccepted)
                _presenter.SetConfirmation(confirmed);
        }

        private void DrawBuildResultAndExport()
        {
            EditorGUILayout.HelpBox("実行ごとに新しいフォルダーを一つ作成します。既存ファイルと過去の実行フォルダーは上書きも削除もしません。", MessageType.None);
            using (new EditorGUI.DisabledScope(!_presenter.CanBuild || _buildQueued))
            {
                if (GUILayout.Button("確認済みの計画を実行", GUILayout.Height(30f)))
                {
                    GUI.FocusControl(null);
                    QueueBuild();
                }
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("実行結果", EditorStyles.miniBoldLabel);
            if (_presenter.Result == null)
            {
                EditorGUILayout.HelpBox("最新のビルド結果をここへ表示します。", MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(_presenter.Message, _presenter.Result.BuildSucceeded ? MessageType.Info : MessageType.Error);
                var selectedIsResult = IsPersistedResultSelected(_presenter.Result, _presenter.SelectedHistoryEntry);
                if (_presenter.Result.Entry != null && !selectedIsResult)
                    DrawHistoryEntry(_presenter.Result.Entry, false);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("履歴とJSON書き出し", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawHistorySelector();

                if (GUILayout.Button("再読込", GUILayout.Width(75f)))
                    _presenter.RefreshHistory();
            }

            var historyNotice = BuildAssistantPresenter.FormatHistoryNotice(_presenter.History);
            if (!string.IsNullOrEmpty(historyNotice))
                EditorGUILayout.HelpBox(historyNotice, MessageType.Warning);
            if (_presenter.SelectedHistoryEntry != null)
                DrawHistoryEntry(_presenter.SelectedHistoryEntry, true);

            using (new EditorGUI.DisabledScope(_presenter.ExportEntry == null))
            {
                if (GUILayout.Button("選択した結果を新しいJSONへ書き出す"))
                    ExportSelectedResult();
            }
            if (!string.IsNullOrEmpty(_presenter.ExportMessage))
                EditorGUILayout.HelpBox(_presenter.ExportMessage, _presenter.LastExportError == BuildAssistantError.None ? MessageType.Info : MessageType.Error);
        }

        /// <summary>現在の画面描画が終わった後に、一度だけビルドを始めるよう登録します。</summary>
        private void QueueBuild()
        {
            if (_buildQueued || !_presenter.CanBuild)
                return;
            _buildQueued = true;
            EditorApplication.delayCall += ExecuteQueuedBuild;
            Repaint();
        }

        /// <summary>画面描画の開始・終了状態を持ち越さず、登録済みのビルドを実行します。</summary>
        private void ExecuteQueuedBuild()
        {
            _buildQueued = false;
            EnsurePresenter();
            _presenter.Build();
            Repaint();
        }

        private void DrawHistoryEntry(BuildAssistantHistoryEntry entry, bool includeLargestAssets)
        {
            DrawValue("ビルドプロファイル", entry.ProfileName + "（" + BuildAssistantPresenter.FormatProfileKind(entry.ProfileKind) + "）");
            DrawValue("対象機種", BuildAssistantPresenter.FormatTarget(entry.Target));
            DrawValue("コード生成方式", BuildAssistantPresenter.FormatScriptingBackend(entry.ScriptingBackend));
            DrawValue("ビルド選択肢", BuildAssistantPresenter.FormatBuildOptions(entry.Options));
            DrawValue("状態", BuildAssistantPresenter.FormatHistoryStatus(entry.Status));
            DrawValue("実行識別子", entry.RunId);
            DrawValue("完了日時", entry.CompletedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            DrawValue("所要時間", entry.Duration.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " 秒");
            DrawValue("出力全体", BuildAssistantPresenter.FormatBytes(entry.TotalOutputBytes));
            DrawValue("格納内容", BuildAssistantPresenter.FormatBytes(entry.PackedContentBytes));
            DrawValue("格納付加分", BuildAssistantPresenter.FormatBytes(entry.PackedOverheadBytes));
            if (!string.IsNullOrEmpty(entry.PreviousRunId))
            {
                DrawValue("出力全体の差", BuildAssistantPresenter.FormatDelta(entry.TotalOutputDeltaBytes));
                DrawValue("格納内容の差", BuildAssistantPresenter.FormatDelta(entry.PackedContentDeltaBytes));
            }
            DrawValue("エラー数 / 警告数", entry.TotalErrors + " / " + entry.TotalWarnings);
            DrawValue("プレイヤー出力", entry.ArtifactPath);
            var historyMessage = BuildAssistantPresenter.FormatHistoryMessage(entry);
            if (!string.IsNullOrEmpty(historyMessage))
                EditorGUILayout.HelpBox(historyMessage, entry.Status == BuildAssistantHistoryStatus.Succeeded ? MessageType.Info : MessageType.Warning);

            if (!includeLargestAssets || entry.Assets.Count == 0)
                return;
            _showLargestAssets = EditorGUILayout.Foldout(_showLargestAssets, "格納容量が大きいアセット", true);
            if (_showLargestAssets)
            {
                foreach (var asset in entry.Assets.Take(5))
                    EditorGUILayout.LabelField(BuildAssistantPresenter.FormatBytes(asset.PackedBytes) + "  " + asset.AssetPath, _wordWrapStyle);
            }
        }

        private void ExportSelectedResult()
        {
            var entry = _presenter.ExportEntry;
            if (entry == null)
                return;
            var directory = Directory.Exists(entry.OutputRoot) ? entry.OutputRoot : GetBrowseStartDirectory();
            var path = EditorUtility.SaveFilePanel("ビルド結果をJSONへ書き出す", directory, "BuildAssistant-" + entry.RunId, "json");
            if (!string.IsNullOrEmpty(path))
                _presenter.Export(path);
        }

        private void DrawHistorySelector()
        {
            var currentResultNotSaved = IsCurrentResultNotSaved(_presenter.Result);
            if (currentResultNotSaved)
            {
                var labels = new[] { "現在の結果（履歴未保存）" }.Concat(_presenter.History.Entries.Select(BuildAssistantPresenter.FormatHistoryLabel)).ToArray();
                var current = _presenter.SelectedHistoryIndex < 0 ? 0 : _presenter.SelectedHistoryIndex + 1;
                var next = EditorGUILayout.Popup(current, labels);
                if (next != current)
                    _presenter.SetHistoryIndex(next - 1);
                return;
            }

            if (_presenter.History.Entries.Count == 0)
            {
                EditorGUILayout.LabelField("保存済み履歴はありません。");
                return;
            }

            var historyLabels = _presenter.History.Entries.Select(BuildAssistantPresenter.FormatHistoryLabel).ToArray();
            var historyIndex = EditorGUILayout.Popup(Mathf.Max(_presenter.SelectedHistoryIndex, 0), historyLabels);
            if (historyIndex != _presenter.SelectedHistoryIndex)
                _presenter.SetHistoryIndex(historyIndex);
        }

        /// <summary>履歴保存に失敗した現在の結果を、保存済み履歴とは別に表示するか判定します。</summary>
        internal static bool IsCurrentResultNotSaved(BuildAssistantBuildResult result) => result?.Entry != null && !result.HistoryPersisted;

        /// <summary>現在の結果と同じ保存済み履歴が選択されているか判定します。</summary>
        internal static bool IsPersistedResultSelected(BuildAssistantBuildResult result, BuildAssistantHistoryEntry selectedEntry)
        {
            return result?.Entry != null && result.HistoryPersisted && StringComparer.Ordinal.Equals(selectedEntry?.RunId, result.Entry.RunId);
        }

        private void SetOutputRoot(string value)
        {
            _outputRoot = value ?? string.Empty;
            _presenter.SetOutputRoot(_outputRoot);
        }

        private string GetBrowseStartDirectory()
        {
            if (Directory.Exists(_outputRoot))
                return _outputRoot;
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            return Directory.Exists(desktop) ? desktop : Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        }

        private void DrawHeading(string heading)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(heading, EditorStyles.boldLabel);
            EditorGUILayout.Space(2f);
        }

        private void DrawValue(string label, string value)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
            EditorGUILayout.LabelField(string.IsNullOrEmpty(value) ? "-" : value, _wordWrapStyle);
        }

        private void EnsurePresenter()
        {
            if (_presenter == null)
                _presenter = new BuildAssistantPresenter();
        }

        private void EnsureStyles()
        {
            if (_wordWrapStyle == null)
                _wordWrapStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };
        }
    }
}
