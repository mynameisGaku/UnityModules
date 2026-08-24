// SPDX-License-Identifier: MIT

namespace BuildGuard.Editor
{
    /// <summary>
    /// Formats structural Prefab override snapshots for deterministic display and copying.
    /// </summary>
    internal static class BuildGuardPrefabOverrideReviewPresentation
    {
        internal static string FormatKind(BuildGuardPrefabOverrideKind kind)
        {
            switch (kind)
            {
                case BuildGuardPrefabOverrideKind.AddedGameObject:
                    return "Added GameObject";
                case BuildGuardPrefabOverrideKind.RemovedGameObject:
                    return "Removed GameObject";
                case BuildGuardPrefabOverrideKind.AddedComponent:
                    return "Added Component";
                case BuildGuardPrefabOverrideKind.RemovedComponent:
                    return "Removed Component";
                default:
                    return kind.ToString();
            }
        }

        internal static string FormatComponent(BuildGuardPrefabOverrideFinding finding)
        {
            return string.IsNullOrEmpty(finding.ComponentTypeName)
                ? "-"
                : $"{finding.ComponentTypeName}[{finding.ComponentIndex}]";
        }

        internal static string FormatSource(BuildGuardPrefabOverrideFinding finding)
        {
            var assetPath = string.IsNullOrEmpty(finding.NearestPrefabAssetPath)
                ? finding.PrefabAssetPath
                : finding.NearestPrefabAssetPath;
            return string.IsNullOrEmpty(finding.SourceObjectPath)
                ? assetPath
                : $"{assetPath} :: {finding.SourceObjectPath}";
        }

        internal static string FormatClipboardText(BuildGuardPrefabOverrideFinding finding)
        {
            return $"{FormatKind(finding.Kind)} | {finding.ScenePath} | {finding.TargetHierarchyPath} | "
                + $"{FormatComponent(finding)} | {FormatSource(finding)}";
        }
    }
}
