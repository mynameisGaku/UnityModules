// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace BuildGuard.Editor
{
    /// <summary>
    /// Runs Build Guard rules on the enabled Scenes from the active Build Profile.
    /// </summary>
    internal static class BuildGuardManualScanner
    {
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
