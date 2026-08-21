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
    /// Shows actionable findings from a manual scan of enabled build Scenes.
    /// </summary>
    internal sealed class BuildGuardScanWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Build Guard/Scan Build Scenes";

        private static readonly GUIContent ScanButtonContent = new GUIContent("Scan Build Scenes");
        private static readonly GUIContent ClearButtonContent = new GUIContent("Clear");

        private readonly List<BuildGuardScanIssue> _issues = new List<BuildGuardScanIssue>();
        private Vector2 _scrollPosition;
        private string _statusText = "Press Scan Build Scenes to inspect the active Build Profile.";

        /// <summary>Gets the current result count for deterministic Editor tests.</summary>
        internal int IssueCount => _issues.Count;

        /// <summary>Gets the current summary for deterministic Editor tests.</summary>
        internal string StatusText => _statusText;

        /// <summary>Opens the manual scan window from the Unity Tools menu.</summary>
        [MenuItem(MenuPath, priority = 2000)]
        private static void ShowWindow()
        {
            var window = GetWindow<BuildGuardScanWindow>();
            window.titleContent = new GUIContent("Build Guard");
            window.minSize = new Vector2(720f, 320f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Build Scene Reference Scan", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Scans enabled Scenes without starting a Player build. Results never modify or save Scene content.",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(ScanButtonContent, GUILayout.Height(28f)))
                {
                    ScanWithProgress();
                }

                using (new EditorGUI.DisabledScope(_issues.Count == 0))
                {
                    if (GUILayout.Button(ClearButtonContent, GUILayout.Width(88f), GUILayout.Height(28f)))
                    {
                        ClearResults();
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

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void ScanWithProgress()
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

        /// <summary>Runs a manual scan and replaces the window result state.</summary>
        internal void RunScan(Func<int, int, string, bool> shouldCancel = null)
        {
            var scenePaths = BuildGuardManualScanner.GetEnabledBuildScenePaths();
            if (scenePaths.Count == 0)
            {
                _issues.Clear();
                _statusText = "No enabled Scenes are configured in the active Build Profile.";
                Repaint();
                return;
            }

            var result = BuildGuardManualScanner.Scan(scenePaths, shouldCancel);
            _issues.Clear();
            _issues.AddRange(result.Issues);
            _statusText = FormatStatus(result);
            Repaint();
        }

        /// <summary>Clears findings while leaving project and Scene state unchanged.</summary>
        internal void ClearResults()
        {
            _issues.Clear();
            _scrollPosition = Vector2.zero;
            _statusText = "Results cleared. Press Scan Build Scenes to scan again.";
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

        private MessageType GetStatusMessageType()
        {
            return _issues.Count == 0 ? MessageType.Info : MessageType.Warning;
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
