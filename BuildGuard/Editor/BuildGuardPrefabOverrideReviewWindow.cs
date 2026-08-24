// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BuildGuard.Editor
{
    /// <summary>
    /// Shows review-only structural Prefab overrides from enabled build Scenes.
    /// </summary>
    internal sealed class BuildGuardPrefabOverrideReviewWindow : EditorWindow
    {
        internal const string MenuPath = "Tools/Build Guard/Review Prefab Overrides";
        internal const int MaximumDisplayedFindings = 1000;

        private static readonly GUIContent ScanButtonContent = new GUIContent("Refresh / Scan");
        private static readonly GUIContent ClearButtonContent = new GUIContent("Clear");

        private readonly List<BuildGuardPrefabOverrideFinding> _findings =
            new List<BuildGuardPrefabOverrideFinding>();
        private readonly List<BuildGuardPrefabOverrideReviewFailure> _failures =
            new List<BuildGuardPrefabOverrideReviewFailure>();
        private Vector2 _scrollPosition;
        private string _statusText =
            "Press Refresh / Scan to review structural Prefab overrides in enabled build Scenes.";
        private MessageType _statusMessageType = MessageType.Info;

        internal int FindingCount => _findings.Count;

        internal int FailureCount => _failures.Count;

        internal string StatusText => _statusText;

        [MenuItem(MenuPath, priority = 2002)]
        private static void ShowWindow()
        {
            var window = GetWindow<BuildGuardPrefabOverrideReviewWindow>();
            window.titleContent = new GUIContent("Prefab Override Review");
            window.minSize = new Vector2(760f, 360f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Build Scene Prefab Override Review", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Reports added or removed Prefab GameObjects and Components. Property overrides are excluded, and findings never block a Player build.",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(ScanButtonContent, GUILayout.Height(28f)))
                {
                    ScanWithProgress();
                }

                using (new EditorGUI.DisabledScope(_findings.Count == 0 && _failures.Count == 0))
                {
                    if (GUILayout.Button(ClearButtonContent, GUILayout.Width(88f), GUILayout.Height(28f)))
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

        /// <summary>Replaces the window state with one all-or-nothing review snapshot.</summary>
        internal void RunScan(Func<int, int, string, bool> shouldCancel = null)
        {
            var scenePaths = BuildGuardManualScanner.GetEnabledBuildScenePaths();
            _findings.Clear();
            _failures.Clear();
            _scrollPosition = Vector2.zero;

            if (scenePaths.Count == 0)
            {
                _statusText = "No enabled Scenes are configured in the active Build Profile.";
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
                _statusText = $"Review cancelled after {result.ScannedSceneCount} Scene(s). Partial findings were discarded.";
                _statusMessageType = MessageType.Warning;
            }
            else
            {
                _failures.AddRange(result.Failures);
                _statusText = $"Review failed after {result.ScannedSceneCount} Scene(s). Partial findings were discarded.";
                _statusMessageType = MessageType.Error;
            }

            Repaint();
        }

        /// <summary>Clears the current snapshot without changing project or Scene state.</summary>
        internal void ClearResults()
        {
            _findings.Clear();
            _failures.Clear();
            _scrollPosition = Vector2.zero;
            _statusText = "Results cleared. Press Refresh / Scan to create a new snapshot.";
            _statusMessageType = MessageType.Info;
            Repaint();
        }

        /// <summary>Refreshes one result before taking a safe navigation action.</summary>
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

        internal BuildGuardPrefabOverrideFinding GetFinding(int index)
        {
            return _findings[index];
        }

        private void ScanWithProgress()
        {
            try
            {
                RunScan((index, total, scenePath) => EditorUtility.DisplayCancelableProgressBar(
                    "Build Guard Prefab Override Review",
                    $"Scanning {Path.GetFileNameWithoutExtension(scenePath)} ({index + 1}/{total})",
                    total == 0 ? 0f : (float)index / total));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void DrawFinding(int index, BuildGuardPrefabOverrideFinding finding)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    $"{index + 1}. {BuildGuardPrefabOverrideReviewPresentation.FormatKind(finding.Kind)}",
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Scene: {finding.ScenePath}", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField(
                    $"Instance: {finding.InstanceRootHierarchyPath}",
                    EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField(
                    $"Path: {finding.TargetHierarchyPath}",
                    EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField(
                    $"Component: {BuildGuardPrefabOverrideReviewPresentation.FormatComponent(finding)}",
                    EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField(
                    $"Source: {BuildGuardPrefabOverrideReviewPresentation.FormatSource(finding)}",
                    EditorStyles.wordWrappedLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Open & Select", GUILayout.Width(118f)))
                    {
                        LocateFinding(index);
                    }

                    if (GUILayout.Button("Copy", GUILayout.Width(80f)))
                    {
                        EditorGUIUtility.systemCopyBuffer =
                            BuildGuardPrefabOverrideReviewPresentation.FormatClipboardText(finding);
                    }

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private static void DrawFailure(BuildGuardPrefabOverrideReviewFailure failure)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Scene review failed", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Scene: {failure.ScenePath}", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField($"Error: {failure.Error}", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField($"Details: {failure.Message}", EditorStyles.wordWrappedLabel);
            }
        }

        private static string FormatSuccessStatus(BuildGuardPrefabOverrideReviewScanResult result)
        {
            if (result.TotalFindingCount == 0)
            {
                return $"Scanned {result.ScannedSceneCount} Scene(s). No structural Prefab overrides found.";
            }

            return result.WasTruncated
                ? $"Scanned {result.ScannedSceneCount} Scene(s). Found {result.TotalFindingCount} structural Prefab override(s); showing the first {result.Findings.Count}."
                : $"Scanned {result.ScannedSceneCount} Scene(s). Found {result.TotalFindingCount} structural Prefab override(s).";
        }
    }
}
