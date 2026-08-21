// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildGuard.Editor
{
    /// <summary>
    /// Scans every loaded Scene GameObject for missing MonoBehaviour slots.
    /// </summary>
    internal static class MissingScriptSceneScanner
    {
        /// <summary>
        /// Scans active and inactive GameObjects in deterministic hierarchy order.
        /// </summary>
        /// <param name="scene">The loaded Scene to scan.</param>
        /// <returns>Findings sorted by hierarchy path.</returns>
        /// <exception cref="ArgumentException">The Scene is invalid.</exception>
        /// <exception cref="InvalidOperationException">The Scene is not loaded.</exception>
        internal static IReadOnlyList<MissingScriptFinding> Scan(Scene scene)
        {
            if (!scene.IsValid())
            {
                throw new ArgumentException("The Scene to scan is invalid.", nameof(scene));
            }

            if (!scene.isLoaded)
            {
                throw new InvalidOperationException("The Scene to scan is not loaded.");
            }

            var findings = new List<MissingScriptFinding>();
            var roots = scene.GetRootGameObjects();
            Array.Sort(roots, CompareRootOrder);

            foreach (var root in roots)
            {
                ScanTransform(root.transform, FormatSegment(root.transform), findings);
            }

            findings.Sort((left, right) => string.CompareOrdinal(left.HierarchyPath, right.HierarchyPath));
            return findings;
        }

        /// <summary>
        /// Compares root GameObjects by sibling index and then by ordinal name.
        /// </summary>
        private static int CompareRootOrder(GameObject left, GameObject right)
        {
            var indexComparison = left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex());
            return indexComparison != 0 ? indexComparison : string.CompareOrdinal(left.name, right.name);
        }

        /// <summary>
        /// Recursively scans a Transform regardless of active state.
        /// </summary>
        private static void ScanTransform(Transform current, string hierarchyPath, ICollection<MissingScriptFinding> findings)
        {
            var missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(current.gameObject);
            if (missingCount > 0)
            {
                findings.Add(new MissingScriptFinding(hierarchyPath, missingCount));
            }

            for (var childIndex = 0; childIndex < current.childCount; childIndex++)
            {
                var child = current.GetChild(childIndex);
                ScanTransform(child, $"{hierarchyPath}/{FormatSegment(child)}", findings);
            }
        }

        /// <summary>
        /// Creates one deterministic hierarchy segment.
        /// </summary>
        private static string FormatSegment(Transform transform)
        {
            return $"{EscapePathText(transform.name)}[{transform.GetSiblingIndex()}]";
        }

        /// <summary>
        /// Escapes path separators and control characters for one-line output.
        /// </summary>
        internal static string EscapePathText(string value)
        {
            return EscapeText(value, true);
        }

        /// <summary>
        /// Escapes control characters while preserving ordinary slashes.
        /// </summary>
        internal static string EscapeSingleLineText(string value)
        {
            return EscapeText(value, false);
        }

        /// <summary>
        /// Escapes control characters with optional slash escaping.
        /// </summary>
        private static string EscapeText(string value, bool escapeSlash)
        {
            var builder = new StringBuilder(value?.Length ?? 0);
            foreach (var character in value ?? string.Empty)
            {
                switch (character)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '/':
                        builder.Append(escapeSlash ? "\\/" : "/");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        builder.Append(character);
                        break;
                }
            }

            return builder.ToString();
        }
    }
}
