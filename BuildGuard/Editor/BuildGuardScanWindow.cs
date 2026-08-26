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
    /// Shows actionable findings from enabled build Scenes or captured Scene assets.
    /// </summary>
    internal sealed class BuildGuardScanWindow : EditorWindow
    {
        private const string ToolMenuPath = "Tools/Build Guard/Scan Build Scenes";
        private const string AssetMenuPath = "Assets/Build Guard/Scan Selected Scenes";

        private static readonly GUIContent ScanBuildScenesButtonContent = new GUIContent("Scan Build Scenes");
        private static readonly GUIContent ScanSelectedScenesButtonContent = new GUIContent("Scan Selected Scenes");
        private static readonly GUIContent ClearButtonContent = new GUIContent("Clear");

        private readonly List<BuildGuardScanIssue> _issues = new List<BuildGuardScanIssue>();
        private string[] _selectedScenePaths = Array.Empty<string>();
        private Vector2 _scrollPosition;
        private bool _scanFailed;
        private string _statusText = "Press Scan Build Scenes to inspect the active Build Profile.";

        /// <summary>Gets the current result count for deterministic Editor tests.</summary>
        internal int IssueCount => _issues.Count;

        /// <summary>Gets the current summary for deterministic Editor tests.</summary>
        internal string StatusText => _statusText;

        /// <summary>Gets the captured selected Scene count for deterministic Editor tests.</summary>
        internal int SelectedSceneCount => _selectedScenePaths.Length;

        /// <summary>Opens the manual scan window from the Unity Tools menu.</summary>
        [MenuItem(ToolMenuPath, priority = 2000)]
        private static void ShowFromTools()
        {
            ShowWindow();
        }

        /// <summary>Opens the same window and captures directly selected Scene assets.</summary>
        [MenuItem(AssetMenuPath, false, 2000)]
        private static void ShowFromAssets()
        {
            var window = ShowWindow();
            window.CaptureSelectedScenes();
        }

        /// <summary>Enables the Assets menu only when at least one selected Scene can be captured.</summary>
        [MenuItem(AssetMenuPath, true)]
        private static bool ValidateShowFromAssets()
        {
            return BuildGuardManualScanner.TryGetSelectedScenePaths(out var paths, out _)
                && paths.Count > 0;
        }

        /// <summary>Creates or reuses the Scene scan window.</summary>
        private static BuildGuardScanWindow ShowWindow()
        {
            var window = GetWindow<BuildGuardScanWindow>();
            window.titleContent = new GUIContent("Build Guard");
            window.minSize = new Vector2(720f, 320f);
            window.Show();
            return window;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Build and Selected Scene Reference Scan", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Scans enabled build Scenes or captured Scene assets. Results never modify or save Scene content.",
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
                    if (GUILayout.Button(ClearButtonContent, GUILayout.Width(88f), GUILayout.Height(28f)))
                    {
                        ClearResults();
                    }
                }
            }

            EditorGUILayout.LabelField("Selected Scene Assets", _selectedScenePaths.Length.ToString());
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Current Selection", GUILayout.Height(28f)))
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
                EditorGUILayout.LabelField($"Scene: {issue.ScenePath}", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField($"GameObject: {issue.HierarchyPath}", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField($"Details: {issue.Details}", EditorStyles.wordWrappedLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Open Scene", GUILayout.Width(110f)))
                    {
                        TryOpenIssue(issue, true);
                    }

                    if (GUILayout.Button("Copy", GUILayout.Width(80f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = FormatClipboardText(issue);
                    }

                    if (issue.Kind == BuildGuardIssueKind.MissingScript
                        && GUILayout.Button("Open and Remove", GUILayout.Width(132f)))
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
                    "Build Guard",
                    $"Scanning {Path.GetFileNameWithoutExtension(scenePath)} ({index + 1}/{total})",
                    total == 0 ? 0f : (float)index / total));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>Scans the captured selected Scenes with the existing cancellable progress UI.</summary>
        private void ScanSelectedScenesWithProgress()
        {
            try
            {
                RunSelectedScan((index, total, scenePath) => EditorUtility.DisplayCancelableProgressBar(
                    "Build Guard",
                    $"Scanning {Path.GetFileNameWithoutExtension(scenePath)} ({index + 1}/{total})",
                    total == 0 ? 0f : (float)index / total));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>Runs a manual scan and replaces the window result state.</summary>
        internal void RunScan(Func<int, int, string, bool> shouldCancel = null)
        {
            var scenePaths = BuildGuardManualScanner.GetEnabledBuildScenePaths();
            if (scenePaths.Count == 0)
            {
                _issues.Clear();
                _scanFailed = false;
                _statusText = "No enabled Scenes are configured in the active Build Profile.";
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

        /// <summary>Captures the current direct Scene asset selection and clears obsolete findings.</summary>
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
                ? "Select one or more Scene assets in the Project window. Folders and non-Scene assets are ignored."
                : $"Captured {_selectedScenePaths.Length} Scene asset(s).";
            Repaint();
        }

        /// <summary>Scans the captured Scene snapshot or reports that it became stale.</summary>
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

        /// <summary>Clears findings while leaving project and Scene state unchanged.</summary>
        internal void ClearResults()
        {
            _issues.Clear();
            _scrollPosition = Vector2.zero;
            _scanFailed = false;
            _statusText = "Results cleared. Scan build Scenes or the captured Scene assets again.";
            Repaint();
        }

        /// <summary>Opens an issue Scene and selects the matching GameObject when it still exists.</summary>
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
        /// Opens one Missing Script issue and removes only the missing MonoBehaviour slots from its GameObject.
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
                "Remove Missing Scripts",
                $"Open {issue.ScenePath} and remove all missing script slots from {issue.HierarchyPath}?\n\nThe Scene will remain unsaved so the change can be reviewed or undone.",
                "Open and Remove",
                "Cancel"))
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

            Undo.RegisterFullObjectHierarchyUndo(target, "Remove Missing Scripts");
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
            _statusText = $"Removed {removedCount} missing script slot(s). Review the unsaved Scene, then save or Undo.";
            Repaint();
            GUIUtility.ExitGUI();
        }

        private static string FormatStatus(BuildGuardManualScanResult result)
        {
            if (result.Cancelled)
            {
                return $"Scan cancelled after {result.ScannedSceneCount} Scene(s). {result.Issues.Count} issue(s) retained.";
            }

            return result.Issues.Count == 0
                ? $"Scanned {result.ScannedSceneCount} Scene(s). No missing references found."
                : $"Scanned {result.ScannedSceneCount} Scene(s). Found {result.Issues.Count} issue(s).";
        }

        /// <summary>Formats selected Scene scan status without changing build Scene wording.</summary>
        private static string FormatSelectedStatus(BuildGuardManualScanResult result)
        {
            if (result.Cancelled)
            {
                return $"Selected Scene scan cancelled after {result.ScannedSceneCount} Scene(s). {result.Issues.Count} issue(s) retained.";
            }

            return result.Issues.Count == 0
                ? $"Scanned {result.ScannedSceneCount} selected Scene(s). No missing references found."
                : $"Scanned {result.ScannedSceneCount} selected Scene(s). Found {result.Issues.Count} issue(s).";
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
                ? "Missing Script"
                : "Missing Object Reference";
        }

        private static string FormatClipboardText(BuildGuardScanIssue issue)
        {
            return $"{FormatKind(issue.Kind)} | {issue.ScenePath} | {issue.HierarchyPath} | {issue.Details}";
        }
    }
}
