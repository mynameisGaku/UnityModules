// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.SceneManagement;

namespace BuildGuard.Editor
{
    /// <summary>
    /// ビルドを停止するシーン内の全問題を、決定論的な1つの診断文へ整形します。
    /// </summary>
    internal static class BuildGuardMessageFormatter
    {
        /// <summary>
        /// 指定シーンの欠落スクリプトと欠落オブジェクト参照を診断文へ整形します。
        /// どちらの問題もない場合はArgumentExceptionを送出します。
        /// </summary>
        internal static string Format(
            Scene scene,
            IReadOnlyList<MissingScriptFinding> missingScripts,
            IReadOnlyList<MissingObjectReferenceFinding> missingObjectReferences)
        {
            if ((missingScripts == null || missingScripts.Count == 0)
                && (missingObjectReferences == null || missingObjectReferences.Count == 0))
            {
                throw new ArgumentException("ビルドを停止する問題が1件以上必要です。");
            }

            // 入力順へ依存しない問題順です。
            var scripts = CopyAndSortScripts(missingScripts);
            var references = CopyAndSortReferences(missingObjectReferences);
            // 改行と末尾を固定した診断文です。
            var builder = new StringBuilder();
            builder.Append("プレイヤービルド対象のシーンで、ビルドを停止する問題が見つかりました。\n");
            builder.Append("シーン: ");
            builder.Append(FormatSceneIdentifier(scene));
            builder.Append('\n');

            if (scripts.Count > 0)
            {
                // オブジェクト単位の欠落数を合計した値です。
                long totalScripts = 0;
                foreach (var finding in scripts)
                {
                    totalScripts += finding.MissingScriptCount;
                }

                builder.Append("欠落スクリプト: ");
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
                builder.Append("欠落オブジェクト参照: ");
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

            builder.Append("再度ビルドする前に、一覧の欠落スクリプトまたはオブジェクト参照を修復するか、該当箇所を削除してください。");
            return builder.ToString();
        }

        /// <summary>欠落スクリプトを複製し、階層パスの昇順へ並べます。</summary>
        private static List<MissingScriptFinding> CopyAndSortScripts(
            IReadOnlyList<MissingScriptFinding> source)
        {
            // 呼び出し元の一覧を変更しない作業用一覧です。
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

        /// <summary>欠落オブジェクト参照を複製し、表示順へ並べます。</summary>
        private static List<MissingObjectReferenceFinding> CopyAndSortReferences(
            IReadOnlyList<MissingObjectReferenceFinding> source)
        {
            // 呼び出し元の一覧を変更しない作業用一覧です。
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
                // 最優先する階層パスの比較結果です。
                var hierarchyOrder = string.CompareOrdinal(left.HierarchyPath, right.HierarchyPath);
                if (hierarchyOrder != 0)
                {
                    return hierarchyOrder;
                }

                // 同じオブジェクト内で優先する部品位置の比較結果です。
                var componentOrder = left.ComponentIndex.CompareTo(right.ComponentIndex);
                return componentOrder != 0
                    ? componentOrder
                    : string.CompareOrdinal(left.PropertyPath, right.PropertyPath);
            });
            return result;
        }

        /// <summary>保存済みパス、または未保存シーン名を1行の識別情報へ整形します。</summary>
        private static string FormatSceneIdentifier(Scene scene)
        {
            if (!string.IsNullOrEmpty(scene.path))
            {
                return MissingScriptSceneScanner.EscapeSingleLineText(scene.path.Replace('\\', '/'));
            }

            return $"<未保存:{MissingScriptSceneScanner.EscapeSingleLineText(scene.name)}>";
        }
    }
}
