// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildGuard.Editor
{
    /// <summary>
    /// Creates and resolves deterministic Scene hierarchy paths.
    /// </summary>
    internal static class BuildGuardHierarchyPath
    {
        /// <summary>Returns Scene roots sorted by sibling index and ordinal name.</summary>
        internal static GameObject[] GetSortedRoots(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            Array.Sort(roots, CompareRootOrder);
            return roots;
        }

        /// <summary>Formats one Transform as an escaped name followed by its sibling index.</summary>
        internal static string FormatSegment(Transform transform)
        {
            return $"{MissingScriptSceneScanner.EscapePathText(transform.name)}[{transform.GetSiblingIndex()}]";
        }

        /// <summary>Creates the complete deterministic path for a Transform.</summary>
        internal static string Create(Transform transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            var segments = new Stack<string>();
            for (var current = transform; current != null; current = current.parent)
            {
                segments.Push(FormatSegment(current));
            }

            return string.Join("/", segments);
        }

        /// <summary>Finds a loaded Scene GameObject by a previously created path.</summary>
        internal static GameObject Find(Scene scene, string hierarchyPath)
        {
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(hierarchyPath))
            {
                return null;
            }

            foreach (var root in GetSortedRoots(scene))
            {
                var found = FindRecursive(root.transform, FormatSegment(root.transform), hierarchyPath);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static GameObject FindRecursive(Transform current, string currentPath, string targetPath)
        {
            if (string.Equals(currentPath, targetPath, StringComparison.Ordinal))
            {
                return current.gameObject;
            }

            for (var childIndex = 0; childIndex < current.childCount; childIndex++)
            {
                var child = current.GetChild(childIndex);
                var found = FindRecursive(child, $"{currentPath}/{FormatSegment(child)}", targetPath);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static int CompareRootOrder(GameObject left, GameObject right)
        {
            var siblingOrder = left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex());
            return siblingOrder != 0 ? siblingOrder : string.CompareOrdinal(left.name, right.name);
        }
    }
}
