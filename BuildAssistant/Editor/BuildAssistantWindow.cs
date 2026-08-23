using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;

namespace BuildAssistant.Editor
{
    /// <summary>Provides a strict top-to-bottom workflow for one safe desktop standalone build.</summary>
    internal sealed class BuildAssistantWindow : EditorWindow
    {
        internal const string ProfileHeading = "\u2460 Profile";
        internal const string OutputHeading = "\u2461 Output";
        internal const string PreviewHeading = "\u2462 Preview";
        internal const string ConfirmHeading = "\u2463 Confirm";
        internal const string BuildHeading = "\u2464 Build / Result / Export";
        internal const string OutputHelpText = "Use a local-drive absolute folder outside Assets, Packages, ProjectSettings, Library, Temp, Logs, and obj. UNC, network, and mapped-drive paths are not supported. An existing folder or exactly one missing child is accepted.";
        internal const string EditorTargetLabel = "Editor Active Target";
        internal const string ProfileHelpText = "This is Unity's editor target. For a custom Build Profile, the authoritative profile target appears under Confirm after Preview. Build Assistant does not switch either setting.";
        internal const string InputFingerprintLabel = "Captured Input Fingerprint";
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
        private GUIStyle _wordWrapStyle;

        /// <summary>Opens or focuses the Build Assistant editor window.</summary>
        internal static void Open()
        {
            GetWindow<BuildAssistantWindow>("Build Assistant");
        }

        private void OnEnable()
        {
            minSize = new Vector2(MinimumWidth, MinimumHeight);
            EnsurePresenter();
            _presenter.SetOutputRoot(_outputRoot);
            _presenter.RefreshHistory();
        }

        private void OnGUI()
        {
            EnsurePresenter();
            EnsureStyles();
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollPosition))
            {
                _scrollPosition = scroll.scrollPosition;
                EditorGUILayout.HelpBox("Work from top to bottom. Preview captures the active build inputs; Build stays disabled until that exact plan is confirmed.", MessageType.Info);
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
            DrawValue("Active Profile", activeProfile == null ? "Platform Profile" : activeProfile.name);
            DrawValue(EditorTargetLabel, EditorUserBuildSettings.activeBuildTarget.ToString());
            EditorGUILayout.HelpBox(ProfileHelpText, MessageType.None);
            if (GUILayout.Button("Open Build Profiles", GUILayout.Width(150f)))
            {
                if (!EditorApplication.ExecuteMenuItem("File/Build Profiles..."))
                    EditorApplication.ExecuteMenuItem("File/Build Profiles");
            }
        }

        private void DrawOutput()
        {
            EditorGUI.BeginChangeCheck();
            var nextOutputRoot = EditorGUILayout.TextField("Output Root", _outputRoot);
            if (EditorGUI.EndChangeCheck())
                SetOutputRoot(nextOutputRoot);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Browse", GUILayout.Width(90f)))
                {
                    var selected = EditorUtility.OpenFolderPanel("Select Build Output Root", GetBrowseStartDirectory(), string.Empty);
                    if (!string.IsNullOrEmpty(selected))
                        SetOutputRoot(selected);
                }
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.HelpBox(OutputHelpText, MessageType.None);
        }

        private void DrawPreview()
        {
            if (GUILayout.Button("Preview Build", GUILayout.Height(26f)))
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
                EditorGUILayout.HelpBox("A ready Preview appears here before confirmation.", MessageType.None);
                return;
            }
            if (!plan.IsReady)
            {
                EditorGUILayout.HelpBox("Fix the Preview error above, then create a new plan.", MessageType.Warning);
                return;
            }

            DrawValue("Profile", plan.ProfileName + " (" + plan.ProfileKind + ")");
            DrawValue(InputFingerprintLabel, plan.ProfileDependencyHash);
            DrawValue("Target", plan.Target.ToString());
            DrawValue("Scripting Backend", plan.ScriptingBackend.ToString());
            DrawValue("Build Options", plan.Options.ToString());
            DrawValue("Enabled Scenes", plan.Scenes.Count(scene => scene.Enabled) + " / " + plan.Scenes.Count);
            DrawValue("Output Root", plan.OutputRoot);
            DrawValue("Run Directory", plan.RunDirectory);
            DrawValue("Player Artifact", plan.ArtifactPath);
            if (plan.PreviousComparableSuccess != null)
                DrawValue("Comparison Baseline", plan.PreviousComparableSuccess.RunId);

            _showScenes = EditorGUILayout.Foldout(_showScenes, "Captured Scenes", true);
            if (_showScenes)
            {
                foreach (var scene in plan.Scenes)
                    EditorGUILayout.LabelField((scene.Enabled ? "[Build] " : "[Skip] ") + scene.AssetPath, _wordWrapStyle);
            }

            _showDefines = EditorGUILayout.Foldout(_showDefines, "Effective Scripting Defines", true);
            if (_showDefines)
                EditorGUILayout.LabelField(plan.EffectiveDefines.Count == 0 ? "None" : string.Join(", ", plan.EffectiveDefines), _wordWrapStyle);

            EditorGUILayout.Space(4f);
            var confirmed = EditorGUILayout.ToggleLeft("I reviewed the profile, scenes, options, and output paths.", _presenter.ConfirmationAccepted);
            if (confirmed != _presenter.ConfirmationAccepted)
                _presenter.SetConfirmation(confirmed);
        }

        private void DrawBuildResultAndExport()
        {
            EditorGUILayout.HelpBox("Build creates one new run directory. Existing files and previous run directories are never overwritten or deleted.", MessageType.None);
            using (new EditorGUI.DisabledScope(!_presenter.CanBuild))
            {
                if (GUILayout.Button("Build Confirmed Plan", GUILayout.Height(30f)))
                {
                    GUI.FocusControl(null);
                    _presenter.Build();
                }
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Result", EditorStyles.miniBoldLabel);
            if (_presenter.Result == null)
            {
                EditorGUILayout.HelpBox("The latest build result appears here.", MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(_presenter.Message, _presenter.Result.BuildSucceeded ? MessageType.Info : MessageType.Error);
                var selectedIsResult = _presenter.Result.Entry != null && StringComparer.Ordinal.Equals(_presenter.SelectedHistoryEntry?.RunId, _presenter.Result.Entry.RunId);
                if (_presenter.Result.Entry != null && !selectedIsResult)
                    DrawHistoryEntry(_presenter.Result.Entry, false);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("History and JSON Export", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawHistorySelector();

                if (GUILayout.Button("Refresh", GUILayout.Width(75f)))
                    _presenter.RefreshHistory();
            }

            if (!string.IsNullOrEmpty(_presenter.History.Message))
                EditorGUILayout.HelpBox(_presenter.History.Message, MessageType.Warning);
            if (_presenter.SelectedHistoryEntry != null)
                DrawHistoryEntry(_presenter.SelectedHistoryEntry, true);

            using (new EditorGUI.DisabledScope(_presenter.ExportEntry == null))
            {
                if (GUILayout.Button("Export Selected Result as New JSON"))
                    ExportSelectedResult();
            }
            if (!string.IsNullOrEmpty(_presenter.ExportMessage))
                EditorGUILayout.HelpBox(_presenter.ExportMessage, _presenter.LastExportError == BuildAssistantError.None ? MessageType.Info : MessageType.Error);
        }

        private void DrawHistoryEntry(BuildAssistantHistoryEntry entry, bool includeLargestAssets)
        {
            DrawValue("Profile", entry.ProfileName + " (" + entry.ProfileKind + ")");
            DrawValue("Target", entry.Target.ToString());
            DrawValue("Scripting Backend", entry.ScriptingBackend.ToString());
            DrawValue("Build Options", entry.Options.ToString());
            DrawValue("Status", entry.Status.ToString());
            DrawValue("Run ID", entry.RunId);
            DrawValue("Completed", entry.CompletedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            DrawValue("Duration", entry.Duration.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s");
            DrawValue("Total Output", BuildAssistantPresenter.FormatBytes(entry.TotalOutputBytes));
            DrawValue("Packed Content", BuildAssistantPresenter.FormatBytes(entry.PackedContentBytes));
            DrawValue("Packed Overhead", BuildAssistantPresenter.FormatBytes(entry.PackedOverheadBytes));
            if (!string.IsNullOrEmpty(entry.PreviousRunId))
            {
                DrawValue("Output Delta", BuildAssistantPresenter.FormatDelta(entry.TotalOutputDeltaBytes));
                DrawValue("Packed Delta", BuildAssistantPresenter.FormatDelta(entry.PackedContentDeltaBytes));
            }
            DrawValue("Errors / Warnings", entry.TotalErrors + " / " + entry.TotalWarnings);
            DrawValue("Artifact", entry.ArtifactPath);
            if (!string.IsNullOrEmpty(entry.Message))
                EditorGUILayout.HelpBox(entry.Message, entry.Status == BuildAssistantHistoryStatus.Succeeded ? MessageType.Info : MessageType.Warning);

            if (!includeLargestAssets || entry.Assets.Count == 0)
                return;
            _showLargestAssets = EditorGUILayout.Foldout(_showLargestAssets, "Largest Packed Assets", true);
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
            var path = EditorUtility.SaveFilePanel("Export Build Assistant JSON", directory, "BuildAssistant-" + entry.RunId, "json");
            if (!string.IsNullOrEmpty(path))
                _presenter.Export(path);
        }

        private void DrawHistorySelector()
        {
            var currentResultNotSaved = _presenter.Result?.Entry != null && !_presenter.History.Entries.Any(entry => StringComparer.Ordinal.Equals(entry.RunId, _presenter.Result.Entry.RunId));
            if (currentResultNotSaved)
            {
                var labels = new[] { "Current result (not saved)" }.Concat(_presenter.History.Entries.Select(BuildAssistantPresenter.FormatHistoryLabel)).ToArray();
                var current = _presenter.SelectedHistoryIndex < 0 ? 0 : _presenter.SelectedHistoryIndex + 1;
                var next = EditorGUILayout.Popup(current, labels);
                if (next != current)
                    _presenter.SetHistoryIndex(next - 1);
                return;
            }

            if (_presenter.History.Entries.Count == 0)
            {
                EditorGUILayout.LabelField("No saved history.");
                return;
            }

            var historyLabels = _presenter.History.Entries.Select(BuildAssistantPresenter.FormatHistoryLabel).ToArray();
            var historyIndex = EditorGUILayout.Popup(Mathf.Max(_presenter.SelectedHistoryIndex, 0), historyLabels);
            if (historyIndex != _presenter.SelectedHistoryIndex)
                _presenter.SetHistoryIndex(historyIndex);
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
