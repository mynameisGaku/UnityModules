// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.SceneManagement;

namespace BuildGuard.Editor
{
    /// <summary>
    /// Formats all build-blocking Scene findings into one deterministic message.
    /// </summary>
    internal static class BuildGuardMessageFormatter
    {
        /// <summary>Formats one or more missing script or object-reference findings.</summary>
        internal static string Format(
            Scene scene,
            IReadOnlyList<MissingScriptFinding> missingScripts,
            IReadOnlyList<MissingObjectReferenceFinding> missingObjectReferences)
        {
            if ((missingScripts == null || missingScripts.Count == 0)
                && (missingObjectReferences == null || missingObjectReferences.Count == 0))
            {
                throw new ArgumentException("At least one build-blocking finding is required.");
            }

            var scripts = CopyAndSortScripts(missingScripts);
            var references = CopyAndSortReferences(missingObjectReferences);
            var builder = new StringBuilder();
            builder.Append("Build Guard found build-blocking issues in a Player build Scene.\n");
            builder.Append("Scene: ");
            builder.Append(FormatSceneIdentifier(scene));
            builder.Append('\n');

            if (scripts.Count > 0)
            {
                long totalScripts = 0;
                foreach (var finding in scripts)
                {
                    totalScripts += finding.MissingScriptCount;
                }

                builder.Append("Missing Scripts: ");
                builder.Append(totalScripts);
                builder.Append('\n');
                foreach (var finding in scripts)
                {
                    builder.Append("- ");
                    builder.Append(finding.HierarchyPath);
                    builder.Append(": ");
                    builder.Append(finding.MissingScriptCount);
                    builder.Append('\n');
                }
            }

            if (references.Count > 0)
            {
                builder.Append("Missing Object References: ");
                builder.Append(references.Count);
                builder.Append('\n');
                foreach (var finding in references)
                {
                    builder.Append("- ");
                    builder.Append(finding.HierarchyPath);
                    builder.Append(" :: ");
                    builder.Append(finding.ComponentTypeName);
                    builder.Append('[');
                    builder.Append(finding.ComponentIndex);
                    builder.Append("].");
                    builder.Append(finding.PropertyPath);
                    builder.Append('\n');
                }
            }

            builder.Append("Repair or remove the listed missing references before building again.");
            return builder.ToString();
        }

        private static List<MissingScriptFinding> CopyAndSortScripts(
            IReadOnlyList<MissingScriptFinding> source)
        {
            var result = new List<MissingScriptFinding>(source?.Count ?? 0);
            if (source != null)
            {
                for (var index = 0; index < source.Count; index++)
                {
                    result.Add(source[index]);
                }
            }

            result.Sort((left, right) => string.CompareOrdinal(left.HierarchyPath, right.HierarchyPath));
            return result;
        }

        private static List<MissingObjectReferenceFinding> CopyAndSortReferences(
            IReadOnlyList<MissingObjectReferenceFinding> source)
        {
            var result = new List<MissingObjectReferenceFinding>(source?.Count ?? 0);
            if (source != null)
            {
                for (var index = 0; index < source.Count; index++)
                {
                    result.Add(source[index]);
                }
            }

            result.Sort((left, right) =>
            {
                var hierarchyOrder = string.CompareOrdinal(left.HierarchyPath, right.HierarchyPath);
                if (hierarchyOrder != 0)
                {
                    return hierarchyOrder;
                }

                var componentOrder = left.ComponentIndex.CompareTo(right.ComponentIndex);
                return componentOrder != 0
                    ? componentOrder
                    : string.CompareOrdinal(left.PropertyPath, right.PropertyPath);
            });
            return result;
        }

        private static string FormatSceneIdentifier(Scene scene)
        {
            if (!string.IsNullOrEmpty(scene.path))
            {
                return MissingScriptSceneScanner.EscapeSingleLineText(scene.path.Replace('\\', '/'));
            }

            return $"<unsaved:{MissingScriptSceneScanner.EscapeSingleLineText(scene.name)}>";
        }
    }
}
