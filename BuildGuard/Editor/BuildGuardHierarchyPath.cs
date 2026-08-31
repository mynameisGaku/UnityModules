// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildGuard.Editor
{
    /// <summary>
    /// 同じシーン状態から常に同じ階層パスを作成し、そのパスから対象を解決します。
    /// </summary>
    internal static class BuildGuardHierarchyPath
    {
        /// <summary>シーンの根元を兄弟順、同順なら名前の文字順へ並べて返します。</summary>
        internal static GameObject[] GetSortedRoots(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            Array.Sort(roots, CompareRootOrder);
            return roots;
        }

        /// <summary>1階層分を、区切り文字を退避した名前と兄弟順へ整形します。</summary>
        internal static string FormatSegment(Transform transform)
        {
            return $"{MissingScriptSceneScanner.EscapePathText(transform.name)}[{transform.GetSiblingIndex()}]";
        }

        /// <summary>指定した変換要素までの、兄弟順を含む完全な階層パスを作成します。</summary>
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

        /// <summary>作成済みの階層パスから、読込済みシーン内のゲームオブジェクトを探します。</summary>
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
