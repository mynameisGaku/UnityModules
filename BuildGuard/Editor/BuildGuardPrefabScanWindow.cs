// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BuildGuard.Editor
{
    /// <summary>
    /// Shows missing references found in selected Prefab assets.
    /// </summary>
    internal sealed class BuildGuardPrefabScanWindow : EditorWindow
    {
        private const string ToolMenuPath = "Tools/Build Guard/Scan Selected Prefabs";
        private const string AssetMenuPath = "Assets/Build Guard/Scan Selected Prefabs";

        private readonly List<BuildGuardPrefabScanIssue> _issues = new List<BuildGuardPrefabScanIssue>();
        private string[] _prefabPaths = Array.Empty<string>();
        private Vector2 _scrollPosition;
        private string _statusText = "Select Prefab assets, then press Scan Selected Prefabs.";

        internal int IssueCount => _issues.Count;

        internal string StatusText => _statusText;

        [MenuItem(ToolMenuPath, priority = 2001)]
        private static void ShowFromTools()
        {
            ShowWindow();
        }

        [MenuItem(AssetMenuPath, false, 2001)]
        private static void ShowFromAssets()
        {
            ShowWindow();
        }

        [MenuItem(AssetMenuPath, true)]
        private static bool ValidateShowFromAssets()
        {
            return GetSelectedPrefabPaths().Count > 0;
        }

        private static void ShowWindow()
        {
            var window = GetWindow<BuildGuardPrefabScanWindow>();
            window.titleContent = new GUIContent("Build Guard Prefabs");
            window.minSize = new Vector2(720f, 320f);
            window.CaptureSelection();
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Selected Prefab Reference Scan", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Loads selected Prefabs temporarily and reports missing scripts and broken object references without saving changes.",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Selected Prefabs", _prefabPaths.Length.ToString());
            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Current Selection", GUILayout.Height(28f)))
                {
                    CaptureSelection();
                }

                using (new EditorGUI.DisabledScope(_prefabPaths.Length == 0))
                {
                    if (GUILayout.Button("Scan Selected Prefabs", GUILayout.Height(28f)))
                    {
                        ScanWithProgress();
                    }
                }

                using (new EditorGUI.DisabledScope(_issues.Count == 0))
                {
                    if (GUILayout.Button("Clear", GUILayout.Width(88f), GUILayout.Height(28f)))
                    {
                        ClearResults();
                    }
                }
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(_statusText, _issues.Count == 0 ? MessageType.Info : MessageType.Warning);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            for (var index = 0; index < _issues.Count; index++)
            {
                DrawIssue(index, _issues[index]);
            }

            EditorGUILayout.EndScrollView();
        }

        internal void CaptureSelection()
        {
            _prefabPaths = GetSelectedPrefabPaths().ToArray();
            _issues.Clear();
            _statusText = _prefabPaths.Length == 0
                ? "Select one or more Prefab assets in the Project window."
                : $"Captured {_prefabPaths.Length} Prefab asset(s).";
            Repaint();
        }

        internal void RunScan(Func<int, int, string, bool> shouldCancel = null)
        {
            if (_prefabPaths.Length == 0)
            {
                _issues.Clear();
                _statusText = "Select one or more Prefab assets in the Project window.";
                Repaint();
                return;
            }

            var result = BuildGuardPrefabScanner.Scan(_prefabPaths, shouldCancel);
            _issues.Clear();
            _issues.AddRange(result.Issues);
            _statusText = result.Cancelled
                ? $"Scan cancelled after {result.ScannedPrefabCount} Prefab(s). {result.Issues.Count} issue(s) retained."
                : result.Issues.Count == 0
                    ? $"Scanned {result.ScannedPrefabCount} Prefab(s). No missing references found."
                    : $"Scanned {result.ScannedPrefabCount} Prefab(s). Found {result.Issues.Count} issue(s).";
            Repaint();
        }

        internal void ClearResults()
        {
            _issues.Clear();
            _scrollPosition = Vector2.zero;
            _statusText = "Results cleared. Press Scan Selected Prefabs to scan again.";
            Repaint();
        }

        internal static IReadOnlyList<string> GetSelectedPrefabPaths()
        {
            var paths = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var item in Selection.objects.Where(item => item != null))
            {
                var path = AssetDatabase.GetAssetPath(item).Replace('\\', '/');
                if (AssetDatabase.IsValidFolder(path) && path.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { path }))
                    {
                        var prefabPath = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                        if (prefabPath.StartsWith("Assets/", StringComparison.Ordinal))
                        {
                            paths.Add(prefabPath);
                        }
                    }
                }
                else if (path.StartsWith("Assets/", StringComparison.Ordinal)
                    && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                    && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                {
                    paths.Add(path);
                }
            }

            return paths.ToArray();
        }

        internal static bool TryOpenIssue(BuildGuardPrefabScanIssue issue, bool confirmSave)
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(issue.PrefabPath);
            if (prefabAsset == null)
            {
                return false;
            }

            if (confirmSave && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return false;
            }

            var stage = PrefabStageUtility.OpenPrefab(issue.PrefabPath);
            if (stage == null)
            {
                return false;
            }

            var target = BuildGuardHierarchyPath.Find(stage.scene, issue.HierarchyPath);
            Selection.activeObject = target != null ? target : prefabAsset;
            EditorGUIUtility.PingObject(Selection.activeObject);
            return true;
        }

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
                "Remove Missing Scripts",
                $"Open {issue.PrefabPath} and remove all missing script slots from {issue.HierarchyPath}?\n\nThe Prefab Stage will remain unsaved so the change can be reviewed or undone.",
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

        private void ScanWithProgress()
        {
            try
            {
                RunScan((index, total, path) => EditorUtility.DisplayCancelableProgressBar(
                    "Build Guard",
                    $"Scanning {Path.GetFileNameWithoutExtension(path)} ({index + 1}/{total})",
                    total == 0 ? 0f : (float)index / total));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void DrawIssue(int index, BuildGuardPrefabScanIssue issue)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"{index + 1}. {FormatKind(issue.Kind)}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Prefab: {issue.PrefabPath}", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField($"GameObject: {issue.HierarchyPath}", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField($"Details: {issue.Details}", EditorStyles.wordWrappedLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Open Prefab", GUILayout.Width(110f)))
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

        private void RemoveMissingScripts(int index, BuildGuardPrefabScanIssue issue)
        {
            if (!TryRemoveMissingScripts(issue, true, true, out var removedCount))
            {
                return;
            }

            _issues.RemoveAt(index);
            _statusText = $"Removed {removedCount} missing script slot(s). Review the unsaved Prefab Stage, then save or Undo.";
            Repaint();
            GUIUtility.ExitGUI();
        }

        private static string FormatKind(BuildGuardIssueKind kind)
        {
            return kind == BuildGuardIssueKind.MissingScript
                ? "Missing Script"
                : "Missing Object Reference";
        }

        private static string FormatClipboardText(BuildGuardPrefabScanIssue issue)
        {
            return $"{FormatKind(issue.Kind)} | {issue.PrefabPath} | {issue.HierarchyPath} | {issue.Details}";
        }
    }
}
