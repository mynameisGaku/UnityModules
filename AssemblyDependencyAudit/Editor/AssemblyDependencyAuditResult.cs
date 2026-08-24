using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AssemblyDependencyAudit.Editor
{
    /// <summary>
    /// 全 asmdef の依存関係、逆参照、問題、循環 component を保持します。
    /// </summary>
    internal sealed class AssemblyDependencyAuditResult
    {
        /// <summary>
        /// 完全に構築済みの監査結果を不変な形で保持します。
        /// </summary>
        internal AssemblyDependencyAuditResult(
            IReadOnlyList<AssemblyDependencyNode> assemblies,
            IReadOnlyList<AssemblyDependencyIssue> issues,
            IReadOnlyList<IReadOnlyList<int>> dependencies,
            IReadOnlyList<IReadOnlyList<int>> dependents,
            IReadOnlyList<IReadOnlyList<int>> cycles)
        {
            Assemblies = CopyItems(assemblies);
            Issues = CopyItems(issues);
            Dependencies = CopyNested(dependencies);
            Dependents = CopyNested(dependents);
            Cycles = CopyNested(cycles);
        }

        /// <summary>asset path 順に並ぶ全 asmdef です。</summary>
        internal IReadOnlyList<AssemblyDependencyNode> Assemblies { get; }

        /// <summary>決定論的な順序に並ぶ検出問題です。</summary>
        internal IReadOnlyList<AssemblyDependencyIssue> Issues { get; }

        /// <summary>assembly index ごとの参照先 index です。</summary>
        internal IReadOnlyList<IReadOnlyList<int>> Dependencies { get; }

        /// <summary>assembly index ごとの参照元 index です。</summary>
        internal IReadOnlyList<IReadOnlyList<int>> Dependents { get; }

        /// <summary>2 件以上で構成される循環参照 component です。</summary>
        internal IReadOnlyList<IReadOnlyList<int>> Cycles { get; }

        /// <summary>
        /// 一次元一覧を読み取り専用 copy にします。
        /// </summary>
        private static IReadOnlyList<T> CopyItems<T>(IReadOnlyList<T> values)
        {
            var copy = new T[values?.Count ?? 0];
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = values[index];
            }

            return new ReadOnlyCollection<T>(copy);
        }

        /// <summary>
        /// 二次元一覧を各段とも読み取り専用 copy にします。
        /// </summary>
        private static IReadOnlyList<IReadOnlyList<int>> CopyNested(IReadOnlyList<IReadOnlyList<int>> values)
        {
            var outer = new IReadOnlyList<int>[values?.Count ?? 0];
            for (var outerIndex = 0; outerIndex < outer.Length; outerIndex++)
            {
                var source = values[outerIndex];
                var inner = new int[source?.Count ?? 0];
                for (var innerIndex = 0; innerIndex < inner.Length; innerIndex++)
                {
                    inner[innerIndex] = source[innerIndex];
                }

                outer[outerIndex] = new ReadOnlyCollection<int>(inner);
            }

            return new ReadOnlyCollection<IReadOnlyList<int>>(outer);
        }
    }
}
