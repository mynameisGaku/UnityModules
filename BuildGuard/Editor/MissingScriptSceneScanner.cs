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
    /// 読み込み済みシーンの全ゲームオブジェクトから、欠落したMonoBehaviourの枠を検出します。
    /// </summary>
    internal static class MissingScriptSceneScanner
    {
        /// <summary>
        /// 有効・無効を問わず、階層順が毎回同じになるようゲームオブジェクトを検査します。
        /// </summary>
        /// <param name="scene">検査する読み込み済みシーンです。</param>
        /// <returns>階層パス順に並べた検出結果です。</returns>
        /// <exception cref="ArgumentException">シーンが無効です。</exception>
        /// <exception cref="InvalidOperationException">シーンが読み込まれていません。</exception>
        internal static IReadOnlyList<MissingScriptFinding> Scan(Scene scene)
        {
            if (!scene.IsValid())
            {
                throw new ArgumentException("検査するシーンが無効です。", nameof(scene));
            }

            if (!scene.isLoaded)
            {
                throw new InvalidOperationException("検査するシーンが読み込まれていません。");
            }

            var findings = new List<MissingScriptFinding>();
            foreach (var root in BuildGuardHierarchyPath.GetSortedRoots(scene))
            {
                ScanTransform(root.transform, BuildGuardHierarchyPath.FormatSegment(root.transform), findings);
            }

            findings.Sort((left, right) => string.CompareOrdinal(left.HierarchyPath, right.HierarchyPath));
            return findings;
        }

        /// <summary>
        /// 有効状態に関係なく、Transformの子階層を再帰的に検査します。
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
                ScanTransform(child, $"{hierarchyPath}/{BuildGuardHierarchyPath.FormatSegment(child)}", findings);
            }
        }

        /// <summary>
        /// 1行で表示できるよう、パス区切りと制御文字を置き換えます。
        /// </summary>
        internal static string EscapePathText(string value)
        {
            return EscapeText(value, true);
        }

        /// <summary>
        /// 通常のスラッシュを保ちながら、制御文字を置き換えます。
        /// </summary>
        internal static string EscapeSingleLineText(string value)
        {
            return EscapeText(value, false);
        }

        /// <summary>
        /// 指定に応じてスラッシュを含め、制御文字を置き換えます。
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
