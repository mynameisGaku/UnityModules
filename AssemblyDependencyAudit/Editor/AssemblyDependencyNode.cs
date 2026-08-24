using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AssemblyDependencyAudit.Editor
{
    /// <summary>
    /// 監査対象 assembly の宣言と解決済み参照を保持します。
    /// </summary>
    internal sealed class AssemblyDependencyNode
    {
        /// <summary>
        /// 解析済みの assembly 情報を不変な一覧として保持します。
        /// </summary>
        internal AssemblyDependencyNode(
            string name,
            string assetPath,
            string guid,
            bool isJsonValid,
            IReadOnlyList<string> includePlatforms,
            IReadOnlyList<string> excludePlatforms,
            IReadOnlyList<AssemblyDependencyReference> references)
        {
            Name = name ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            Guid = guid ?? string.Empty;
            IsJsonValid = isJsonValid;
            IncludePlatforms = CopyStrings(includePlatforms);
            ExcludePlatforms = CopyStrings(excludePlatforms);
            References = CopyReferences(references);
        }

        /// <summary>assembly 名です。無効な JSON または空名の場合は空です。</summary>
        internal string Name { get; }

        /// <summary>Unity project から見た asmdef のパスです。</summary>
        internal string AssetPath { get; }

        /// <summary>asmdef asset の GUID です。</summary>
        internal string Guid { get; }

        /// <summary>JSON 全体を解析できたかを示します。</summary>
        internal bool IsJsonValid { get; }

        /// <summary>includePlatforms の不変な一覧です。</summary>
        internal IReadOnlyList<string> IncludePlatforms { get; }

        /// <summary>excludePlatforms の不変な一覧です。</summary>
        internal IReadOnlyList<string> ExcludePlatforms { get; }

        /// <summary>元表記と解決結果を保持する参照一覧です。</summary>
        internal IReadOnlyList<AssemblyDependencyReference> References { get; }

        /// <summary>Editor だけを対象にする assembly かを示します。</summary>
        internal bool IsEditorOnly => IncludePlatforms.Count == 1 && string.Equals(IncludePlatforms[0], "Editor", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// null を含まない文字列の読み取り専用 copy を作ります。
        /// </summary>
        private static IReadOnlyList<string> CopyStrings(IReadOnlyList<string> values)
        {
            var copy = new string[values?.Count ?? 0];
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = values[index] ?? string.Empty;
            }

            return new ReadOnlyCollection<string>(copy);
        }

        /// <summary>
        /// 参照の読み取り専用 copy を作ります。
        /// </summary>
        private static IReadOnlyList<AssemblyDependencyReference> CopyReferences(IReadOnlyList<AssemblyDependencyReference> values)
        {
            var copy = new AssemblyDependencyReference[values?.Count ?? 0];
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = values[index];
            }

            return new ReadOnlyCollection<AssemblyDependencyReference>(copy);
        }
    }
}
