// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.SceneManagement;

namespace BuildGuard.Editor
{
    /// <summary>
    /// Missing Scriptのbuild失敗理由を決定論的な一行単位のmessageへ整形します。
    /// </summary>
    internal static class MissingScriptMessageFormatter
    {
        /// <summary>
        /// Sceneと検出結果からbuild失敗messageを作成します。
        /// </summary>
        /// <param name="scene">検出元Scene。</param>
        /// <param name="findings">1件以上の検出結果。</param>
        /// <returns>階層path順に並んだbuild失敗message。</returns>
        /// <exception cref="ArgumentException">検出結果が空の場合。</exception>
        internal static string Format(Scene scene, IReadOnlyList<MissingScriptFinding> findings)
        {
            if (findings == null || findings.Count == 0)
            {
                throw new ArgumentException("1件以上の検出結果が必要です。", nameof(findings));
            }

            var sorted = new List<MissingScriptFinding>(findings.Count);
            for (var index = 0; index < findings.Count; index++)
            {
                sorted.Add(findings[index]);
            }

            sorted.Sort((left, right) => string.CompareOrdinal(left.HierarchyPath, right.HierarchyPath));

            long totalCount = 0;
            var builder = new StringBuilder();
            builder.Append("Build GuardがPlayer build対象Scene内のMissing Scriptを検出しました。\n");
            builder.Append("Scene: ");
            builder.Append(FormatSceneIdentifier(scene));
            builder.Append('\n');

            foreach (var finding in sorted)
            {
                totalCount += finding.MissingScriptCount;
                builder.Append("- ");
                builder.Append(finding.HierarchyPath);
                builder.Append(": ");
                builder.Append(finding.MissingScriptCount);
                builder.Append('\n');
            }

            builder.Append("合計: ");
            builder.Append(totalCount);
            builder.Append("\nMissing MonoBehaviourを修復または削除してからbuildを再実行してください。");
            return builder.ToString();
        }

        /// <summary>
        /// 保存済みpathまたは未保存Scene名を一行の識別子へ変換します。
        /// </summary>
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
