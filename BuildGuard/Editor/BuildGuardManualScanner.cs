// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace BuildGuard.Editor
{
    /// <summary>
    /// Runs Build Guard rules on enabled build Scenes or captured Scene assets.
    /// </summary>
    internal static class BuildGuardManualScanner
    {
        /// <summary>Maximum number of selected asset candidates accepted by one capture.</summary>
        internal const int MaximumSelectedAssetCandidates = 4096;

        /// <summary>Maximum number of selected Scene assets accepted by one scan.</summary>
        internal const int MaximumSelectedScenes = 256;

        /// <summary>Returns enabled Scene paths from the effective active Build Profile list.</summary>
        internal static IReadOnlyList<string> GetEnabledBuildScenePaths()
        {
            var scenes = EditorBuildSettings.scenes ?? Array.Empty<EditorBuildSettingsScene>();
            var paths = new List<string>(scenes.Length);
            for (var index = 0; index < scenes.Length; index++)
            {
                var scene = scenes[index];
                if (scene != null && scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                {
                    paths.Add(scene.path.Replace('\\', '/'));
                }
            }

            return paths;
        }

        /// <summary>Captures directly selected Scene assets below Assets in ordinal path order.</summary>
        internal static bool TryGetSelectedScenePaths(
            out IReadOnlyList<string> scenePaths,
            out string errorMessage)
        {
            try
            {
                return TryResolveSelectedScenePaths(
                    Selection.assetGUIDs,
                    AssetDatabase.GUIDToAssetPath,
                    path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null,
                    out scenePaths,
                    out errorMessage);
            }
            catch (Exception exception)
            {
                scenePaths = Array.Empty<string>();
                errorMessage = $"Selected Scene capture failed: {exception.Message}";
                return false;
            }
        }

        /// <summary>Resolves selected GUIDs and keeps direct Scene assets below Assets.</summary>
        internal static bool TryResolveSelectedScenePaths(
            IReadOnlyList<string> selectedAssetGuids,
            Func<string, string> guidToAssetPath,
            Func<string, bool> isSceneAsset,
            out IReadOnlyList<string> scenePaths,
            out string errorMessage)
        {
            scenePaths = Array.Empty<string>();
            errorMessage = string.Empty;
            if (selectedAssetGuids == null || guidToAssetPath == null || isSceneAsset == null)
            {
                errorMessage = "Selected Scene capture source is unavailable.";
                return false;
            }

            if (selectedAssetGuids.Count > MaximumSelectedAssetCandidates)
            {
                errorMessage = $"Too many selected assets. Select at most {MaximumSelectedAssetCandidates} assets.";
                return false;
            }

            var paths = new SortedSet<string>(StringComparer.Ordinal);
            try
            {
                for (var index = 0; index < selectedAssetGuids.Count; index++)
                {
                    var guid = selectedAssetGuids[index];
                    if (string.IsNullOrWhiteSpace(guid))
                    {
                        continue;
                    }

                    var path = (guidToAssetPath(guid) ?? string.Empty).Replace('\\', '/').TrimEnd('/');
                    if (!IsSceneAssetPath(path) || !isSceneAsset(path))
                    {
                        continue;
                    }

                    paths.Add(path);
                    if (paths.Count > MaximumSelectedScenes)
                    {
                        errorMessage = $"Too many selected Scenes. Select at most {MaximumSelectedScenes} Scene assets.";
                        return false;
                    }
                }
            }
            catch (Exception exception)
            {
                errorMessage = $"Selected Scene capture failed: {exception.Message}";
                return false;
            }

            scenePaths = new List<string>(paths);
            return true;
        }

        /// <summary>Scans specified Scene paths without changing their saved or loaded state.</summary>
        internal static BuildGuardManualScanResult Scan(
            IReadOnlyList<string> scenePaths,
            Func<int, int, string, bool> shouldCancel = null)
        {
            var issues = new List<BuildGuardScanIssue>();
            var scannedCount = BuildGuardScenePathVisitor.Visit(
                scenePaths,
                shouldCancel,
                scene => AppendIssues(scene, BuildGuardSceneInspector.Inspect(scene), issues),
                out var cancelled);
            return new BuildGuardManualScanResult(issues, scannedCount, cancelled);
        }

        /// <summary>Revalidates captured Scenes and discards partial results when the snapshot is stale.</summary>
        internal static bool TryScanSelectedScenes(
            IReadOnlyList<string> scenePaths,
            Func<int, int, string, bool> shouldCancel,
            out BuildGuardManualScanResult result,
            out string errorMessage)
        {
            result = new BuildGuardManualScanResult(Array.Empty<BuildGuardScanIssue>(), 0, false);
            try
            {
                if (!TryNormalizeCapturedScenePaths(scenePaths, out var normalizedPaths, out errorMessage))
                {
                    return false;
                }

                var scanResult = Scan(normalizedPaths, shouldCancel);
                if (!TryNormalizeCapturedScenePaths(
                        normalizedPaths,
                        out var finalPaths,
                        out _)
                    || !HaveSamePaths(normalizedPaths, finalPaths)
                    || (!scanResult.Cancelled && scanResult.ScannedSceneCount != normalizedPaths.Count))
                {
                    errorMessage = "Selected Scene assets changed. Press Use Current Selection and scan again.";
                    return false;
                }

                result = scanResult;
                return true;
            }
            catch (Exception exception)
            {
                errorMessage = $"Selected Scene scan failed: {exception.Message}";
                return false;
            }
        }

        /// <summary>Checks that every captured path still resolves to a Scene asset.</summary>
        private static bool TryNormalizeCapturedScenePaths(
            IReadOnlyList<string> scenePaths,
            out IReadOnlyList<string> normalizedPaths,
            out string errorMessage)
        {
            normalizedPaths = Array.Empty<string>();
            errorMessage = string.Empty;
            if (scenePaths == null || scenePaths.Count == 0)
            {
                errorMessage = "Select one or more Scene assets in the Project window.";
                return false;
            }

            if (scenePaths.Count > MaximumSelectedScenes)
            {
                errorMessage = $"Too many selected Scenes. Select at most {MaximumSelectedScenes} Scene assets.";
                return false;
            }

            var paths = new SortedSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < scenePaths.Count; index++)
            {
                var path = (scenePaths[index] ?? string.Empty).Replace('\\', '/').TrimEnd('/');
                if (!IsSceneAssetPath(path) || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    errorMessage = "Selected Scene assets changed. Press Use Current Selection and scan again.";
                    return false;
                }

                paths.Add(path);
            }

            normalizedPaths = new List<string>(paths);
            return true;
        }

        /// <summary>Checks whether a path identifies a saved Scene asset below Assets.</summary>
        private static bool IsSceneAssetPath(string path)
        {
            return path.StartsWith("Assets/", StringComparison.Ordinal)
                && path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HaveSamePaths(
            IReadOnlyList<string> expected,
            IReadOnlyList<string> actual)
        {
            if (expected.Count != actual.Count)
            {
                return false;
            }

            for (var index = 0; index < expected.Count; index++)
            {
                if (!string.Equals(expected[index], actual[index], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static void AppendIssues(
            Scene scene,
            BuildGuardSceneInspection inspection,
            ICollection<BuildGuardScanIssue> issues)
        {
            foreach (var finding in inspection.MissingScripts)
            {
                issues.Add(new BuildGuardScanIssue(
                    BuildGuardIssueKind.MissingScript,
                    scene.path.Replace('\\', '/'),
                    finding.HierarchyPath,
                    $"Missing Scripts: {finding.MissingScriptCount}"));
            }

            foreach (var finding in inspection.MissingObjectReferences)
            {
                issues.Add(new BuildGuardScanIssue(
                    BuildGuardIssueKind.MissingObjectReference,
                    scene.path.Replace('\\', '/'),
                    finding.HierarchyPath,
                    $"{finding.ComponentTypeName}[{finding.ComponentIndex}].{finding.PropertyPath}"));
            }
        }
    }
}
