// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BuildGuard.Editor
{
    /// <summary>
    /// Inspects selected Prefab assets without saving or changing them.
    /// </summary>
    internal static class BuildGuardPrefabScanner
    {
        /// <summary>Normalizes and validates persistent Prefab asset paths below Assets.</summary>
        internal static IReadOnlyList<string> NormalizePrefabPaths(IReadOnlyList<string> prefabPaths)
        {
            if (prefabPaths == null)
            {
                throw new ArgumentNullException(nameof(prefabPaths));
            }

            var normalized = new SortedSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < prefabPaths.Count; index++)
            {
                var path = (prefabPaths[index] ?? string.Empty).Replace('\\', '/').TrimEnd('/');
                if (!path.StartsWith("Assets/", StringComparison.Ordinal)
                    || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                    || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    throw new ArgumentException($"The path is not a Prefab asset below Assets: {path}", nameof(prefabPaths));
                }

                normalized.Add(path);
            }

            return new List<string>(normalized);
        }

        /// <summary>Scans Prefabs in ordinal path order and restores every loaded Prefab content.</summary>
        internal static BuildGuardPrefabScanResult Scan(
            IReadOnlyList<string> prefabPaths,
            Func<int, int, string, bool> shouldCancel = null)
        {
            var paths = NormalizePrefabPaths(prefabPaths);
            var issues = new List<BuildGuardPrefabScanIssue>();
            var scannedCount = 0;
            var cancelled = false;
            for (var index = 0; index < paths.Count; index++)
            {
                var path = paths[index];
                if (shouldCancel != null && shouldCancel(index, paths.Count, path))
                {
                    cancelled = true;
                    break;
                }

                GameObject contentsRoot = null;
                try
                {
                    contentsRoot = PrefabUtility.LoadPrefabContents(path);
                    AppendIssues(path, BuildGuardSceneInspector.Inspect(contentsRoot.scene), issues);
                    scannedCount++;
                }
                finally
                {
                    if (contentsRoot != null)
                    {
                        PrefabUtility.UnloadPrefabContents(contentsRoot);
                    }
                }
            }

            return new BuildGuardPrefabScanResult(issues, scannedCount, cancelled);
        }

        private static void AppendIssues(
            string prefabPath,
            BuildGuardSceneInspection inspection,
            ICollection<BuildGuardPrefabScanIssue> issues)
        {
            foreach (var finding in inspection.MissingScripts)
            {
                issues.Add(new BuildGuardPrefabScanIssue(
                    BuildGuardIssueKind.MissingScript,
                    prefabPath,
                    finding.HierarchyPath,
                    $"Missing Scripts: {finding.MissingScriptCount}"));
            }

            foreach (var finding in inspection.MissingObjectReferences)
            {
                issues.Add(new BuildGuardPrefabScanIssue(
                    BuildGuardIssueKind.MissingObjectReference,
                    prefabPath,
                    finding.HierarchyPath,
                    $"{finding.ComponentTypeName}[{finding.ComponentIndex}].{finding.PropertyPath}"));
            }
        }
    }
}
