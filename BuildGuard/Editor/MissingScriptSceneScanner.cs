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
    /// 読み込み済みSceneの全GameObjectを走査してMissing MonoBehaviourを収集します。
    /// </summary>
    internal static class MissingScriptSceneScanner
    {
        /// <summary>
        /// 指定Sceneのactive・inactiveを含む全階層を走査します。
        /// </summary>
        /// <param name="scene">走査対象の読み込み済みScene。</param>
        /// <returns>階層path順に並んだ検出結果。</returns>
        /// <exception cref="ArgumentException">Sceneが無効な場合。</exception>
        /// <exception cref="InvalidOperationException">Sceneが読み込まれていない場合。</exception>
        internal static IReadOnlyList<MissingScriptFinding> Scan(Scene scene)
        {
            if (!scene.IsValid())
            {
                throw new ArgumentException("走査対象Sceneが無効です。", nameof(scene));
            }

            if (!scene.isLoaded)
            {
                throw new InvalidOperationException("走査対象Sceneが読み込まれていません。");
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
        /// root GameObjectをScene内の兄弟index順で比較します。
        /// </summary>
        private static int CompareRootOrder(GameObject left, GameObject right)
        {
            var indexComparison = left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex());
            return indexComparison != 0 ? indexComparison : string.CompareOrdinal(left.name, right.name);
        }

        /// <summary>
        /// Transform以下をinactive状態に関係なく再帰走査します。
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
        /// GameObject名と兄弟indexから一意なpath要素を作成します。
        /// </summary>
        private static string FormatSegment(Transform transform)
        {
            return $"{EscapePathText(transform.name)}[{transform.GetSiblingIndex()}]";
        }

        /// <summary>
        /// path区切りと制御文字を一行で判別できる表現へ変換します。
        /// </summary>
        internal static string EscapePathText(string value)
        {
            return EscapeText(value, true);
        }

        /// <summary>
        /// 制御文字を一行表現へ変換し、通常のslashは維持します。
        /// </summary>
        internal static string EscapeSingleLineText(string value)
        {
            return EscapeText(value, false);
        }

        /// <summary>
        /// path区切りをescapeするか選び、制御文字を一行表現へ変換します。
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
