using System;
using System.Collections.Generic;
using UnityEngine;

namespace AssemblyDependencyAudit.Editor
{
    /// <summary>
    /// asmdef source 全体を依存 graph と問題一覧へ変換します。
    /// </summary>
    internal static class AssemblyDependencyAnalyzer
    {
        /// <summary>1 回に監査できる asmdef 数です。</summary>
        internal const int MaximumAssemblyDefinitions = 10000;

        /// <summary>1 件の asmdef で許可する文字数です。</summary>
        internal const int MaximumSourceCharacters = 1048576;

        /// <summary>1 件の asmdef で許可する参照数です。</summary>
        internal const int MaximumReferencesPerAssembly = 4096;

        /// <summary>全 asmdef で許可する参照総数です。</summary>
        internal const int MaximumReferences = 100000;

        /// <summary>1 回の監査で返せる問題数です。</summary>
        internal const int MaximumIssues = 50000;

        /// <summary>
        /// 入力全体を検証してから完全な監査結果だけを返します。
        /// </summary>
        internal static bool TryAnalyze(
            IReadOnlyList<AssemblyDefinitionSource> sources,
            IAssemblyDependencySourceAdapter sourceAdapter,
            out AssemblyDependencyAuditResult result,
            out AssemblyDependencyAuditError error,
            out string errorMessage)
        {
            result = null;
            error = AssemblyDependencyAuditError.None;
            errorMessage = string.Empty;

            if (sources == null || sourceAdapter == null)
            {
                error = AssemblyDependencyAuditError.SourceUnavailable;
                errorMessage = "監査 source を取得できませんでした。";
                return false;
            }

            if (sources.Count > MaximumAssemblyDefinitions)
            {
                error = AssemblyDependencyAuditError.TooManyAssemblyDefinitions;
                errorMessage = $"asmdef 数が上限 {MaximumAssemblyDefinitions} 件を超えています。";
                return false;
            }

            var orderedSources = new List<AssemblyDefinitionSource>(sources.Count);
            for (var index = 0; index < sources.Count; index++)
            {
                if (sources[index] == null)
                {
                    error = AssemblyDependencyAuditError.SourceUnavailable;
                    errorMessage = "null の asmdef source が含まれています。";
                    return false;
                }

                orderedSources.Add(sources[index]);
            }

            orderedSources.Sort(CompareSources);
            var parsedSources = new List<ParsedSource>(orderedSources.Count);
            var issues = new List<AssemblyDependencyIssue>();
            var referenceCount = 0;

            for (var index = 0; index < orderedSources.Count; index++)
            {
                var source = orderedSources[index];
                if (source.Json.Length > MaximumSourceCharacters)
                {
                    error = AssemblyDependencyAuditError.SourceTooLarge;
                    errorMessage = $"{source.AssetPath} が文字数上限 {MaximumSourceCharacters} を超えています。";
                    return false;
                }

                var parsed = ParseSource(source, issues, out var issueLimitReached);
                if (issueLimitReached)
                {
                    error = AssemblyDependencyAuditError.TooManyIssues;
                    errorMessage = $"検出問題数が上限 {MaximumIssues} 件を超えています。";
                    return false;
                }

                if (parsed.References.Count > MaximumReferencesPerAssembly)
                {
                    error = AssemblyDependencyAuditError.TooManyReferencesPerAssembly;
                    errorMessage = $"{source.AssetPath} の参照数が上限 {MaximumReferencesPerAssembly} 件を超えています。";
                    return false;
                }

                referenceCount += parsed.References.Count;
                if (referenceCount > MaximumReferences)
                {
                    error = AssemblyDependencyAuditError.TooManyReferences;
                    errorMessage = $"参照総数が上限 {MaximumReferences} 件を超えています。";
                    return false;
                }

                parsedSources.Add(parsed);
            }

            var names = BuildIndex(parsedSources, parsed => parsed.Name, StringComparer.Ordinal, false);
            var guids = BuildIndex(parsedSources, parsed => parsed.Source.Guid, StringComparer.OrdinalIgnoreCase, false);
            var paths = BuildIndex(parsedSources, parsed => parsed.Source.AssetPath, StringComparer.Ordinal, false);

            if (!AddDuplicateIssues(parsedSources, names, AssemblyDependencyIssueKind.DuplicateName, "同名の assembly が複数あります。", issues) ||
                !AddDuplicateIssues(parsedSources, guids, AssemblyDependencyIssueKind.DuplicateGuid, "同じ GUID の asmdef が複数あります。", issues))
            {
                error = AssemblyDependencyAuditError.TooManyIssues;
                errorMessage = $"検出問題数が上限 {MaximumIssues} 件を超えています。";
                return false;
            }

            var nodes = new List<AssemblyDependencyNode>(parsedSources.Count);
            var dependencies = CreateGraph(parsedSources.Count);

            for (var index = 0; index < parsedSources.Count; index++)
            {
                var parsed = parsedSources[index];
                var resolvedReferences = new List<AssemblyDependencyReference>(parsed.References.Count);
                var hasNameReference = false;
                var hasGuidReference = false;

                for (var referenceIndex = 0; referenceIndex < parsed.References.Count; referenceIndex++)
                {
                    var value = parsed.References[referenceIndex] ?? string.Empty;
                    var kind = GetReferenceKind(value);
                    hasNameReference |= kind == AssemblyDependencyReferenceKind.Name;
                    hasGuidReference |= kind == AssemblyDependencyReferenceKind.Guid;

                    var resolution = ResolveReference(value, kind, names, guids, paths, sourceAdapter);
                    if (resolution.IsAmbiguous)
                    {
                        if (!TryAddIssue(issues, new AssemblyDependencyIssue(
                                AssemblyDependencyIssueKind.AmbiguousReference,
                                parsed.Source.AssetPath,
                                string.Empty,
                                value,
                                "参照先を一意に決められません。")))
                        {
                            return FailIssueLimit(out result, out error, out errorMessage);
                        }
                    }
                    else if (resolution.Index < 0)
                    {
                        if (!TryAddIssue(issues, new AssemblyDependencyIssue(
                                AssemblyDependencyIssueKind.UnresolvedReference,
                                parsed.Source.AssetPath,
                                string.Empty,
                                value,
                                "参照先の asmdef が見つかりません。")))
                        {
                            return FailIssueLimit(out result, out error, out errorMessage);
                        }
                    }
                    else
                    {
                        dependencies[index].Add(resolution.Index);
                        if (resolution.Index == index && !TryAddIssue(issues, new AssemblyDependencyIssue(
                                AssemblyDependencyIssueKind.SelfReference,
                                parsed.Source.AssetPath,
                                parsed.Source.AssetPath,
                                value,
                                "asmdef が自分自身を参照しています。")))
                        {
                            return FailIssueLimit(out result, out error, out errorMessage);
                        }
                    }

                    resolvedReferences.Add(new AssemblyDependencyReference(value, kind, resolution.Index));
                }

                if (hasNameReference && hasGuidReference && !TryAddIssue(issues, new AssemblyDependencyIssue(
                        AssemblyDependencyIssueKind.MixedReferenceKinds,
                        parsed.Source.AssetPath,
                        string.Empty,
                        string.Empty,
                        "assembly 名参照と GUID 参照が混在しています。")))
                {
                    return FailIssueLimit(out result, out error, out errorMessage);
                }

                if (parsed.IncludePlatforms.Count > 0 && parsed.ExcludePlatforms.Count > 0 && !TryAddIssue(issues, new AssemblyDependencyIssue(
                        AssemblyDependencyIssueKind.IncludeAndExcludePlatforms,
                        parsed.Source.AssetPath,
                        string.Empty,
                        string.Empty,
                        "includePlatforms と excludePlatforms が同時指定されています。")))
                {
                    return FailIssueLimit(out result, out error, out errorMessage);
                }

                nodes.Add(new AssemblyDependencyNode(
                    parsed.Name,
                    parsed.Source.AssetPath,
                    parsed.Source.Guid,
                    parsed.IsJsonValid,
                    parsed.IncludePlatforms,
                    parsed.ExcludePlatforms,
                    resolvedReferences));
            }

            SortAndDeduplicateGraph(dependencies);
            var dependents = ReverseGraph(dependencies);

            if (!AddPlayerToEditorIssues(nodes, dependencies, issues))
            {
                return FailIssueLimit(out result, out error, out errorMessage);
            }

            var cycles = FindCycles(dependencies, dependents);
            if (!AddCycleIssues(nodes, cycles, issues))
            {
                return FailIssueLimit(out result, out error, out errorMessage);
            }

            issues.Sort(CompareIssues);
            result = new AssemblyDependencyAuditResult(nodes, issues, ToReadOnlyGraph(dependencies), ToReadOnlyGraph(dependents), ToReadOnlyGraph(cycles));
            return true;
        }

        /// <summary>
        /// 1 件の source を JsonUtility で解析します。
        /// </summary>
        private static ParsedSource ParseSource(AssemblyDefinitionSource source, List<AssemblyDependencyIssue> issues, out bool issueLimitReached)
        {
            issueLimitReached = false;
            AssemblyDefinitionJson json = null;
            var trimmed = source.Json.Trim();
            if (trimmed.Length > 1 && trimmed[0] == '{' && trimmed[trimmed.Length - 1] == '}')
            {
                try
                {
                    json = JsonUtility.FromJson<AssemblyDefinitionJson>(source.Json);
                }
                catch (ArgumentException)
                {
                    json = null;
                }
            }

            if (json == null)
            {
                issueLimitReached = !TryAddIssue(issues, new AssemblyDependencyIssue(
                    AssemblyDependencyIssueKind.InvalidJson,
                    source.AssetPath,
                    string.Empty,
                    string.Empty,
                    "asmdef JSON を解析できません。"));
                return new ParsedSource(source, false, string.Empty, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
            }

            var name = json.name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                issueLimitReached = !TryAddIssue(issues, new AssemblyDependencyIssue(
                    AssemblyDependencyIssueKind.MissingName,
                    source.AssetPath,
                    string.Empty,
                    string.Empty,
                    "assembly 名が空です。"));
                name = string.Empty;
            }

            return new ParsedSource(
                source,
                true,
                name,
                CopyArray(json.includePlatforms),
                CopyArray(json.excludePlatforms),
                CopyArray(json.references));
        }

        /// <summary>
        /// null を空文字へ正規化した配列を作ります。
        /// </summary>
        private static IReadOnlyList<string> CopyArray(string[] values)
        {
            if (values == null || values.Length == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new string[values.Length];
            for (var index = 0; index < values.Length; index++)
            {
                copy[index] = values[index] ?? string.Empty;
            }

            return copy;
        }

        /// <summary>
        /// 指定項目から重複を保持できる index を構築します。
        /// </summary>
        private static Dictionary<string, List<int>> BuildIndex(
            IReadOnlyList<ParsedSource> sources,
            Func<ParsedSource, string> selector,
            StringComparer comparer,
            bool includeEmpty)
        {
            var index = new Dictionary<string, List<int>>(comparer);
            for (var sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
            {
                var key = selector(sources[sourceIndex]) ?? string.Empty;
                if (!includeEmpty && string.IsNullOrEmpty(key))
                {
                    continue;
                }

                if (!index.TryGetValue(key, out var matches))
                {
                    matches = new List<int>();
                    index.Add(key, matches);
                }

                matches.Add(sourceIndex);
            }

            return index;
        }

        /// <summary>
        /// 重複 key に属する各 source へ問題を追加します。
        /// </summary>
        private static bool AddDuplicateIssues(
            IReadOnlyList<ParsedSource> sources,
            IReadOnlyDictionary<string, List<int>> index,
            AssemblyDependencyIssueKind kind,
            string message,
            List<AssemblyDependencyIssue> issues)
        {
            foreach (var pair in index)
            {
                if (pair.Value.Count < 2)
                {
                    continue;
                }

                for (var matchIndex = 0; matchIndex < pair.Value.Count; matchIndex++)
                {
                    var source = sources[pair.Value[matchIndex]].Source;
                    var relatedIndex = matchIndex == 0 ? 1 : 0;
                    var related = sources[pair.Value[relatedIndex]].Source;
                    if (!TryAddIssue(issues, new AssemblyDependencyIssue(kind, source.AssetPath, related.AssetPath, pair.Key, message)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 参照表記を name または GUID として分類します。
        /// </summary>
        private static AssemblyDependencyReferenceKind GetReferenceKind(string reference)
        {
            return reference.StartsWith("GUID:", StringComparison.OrdinalIgnoreCase)
                ? AssemblyDependencyReferenceKind.Guid
                : AssemblyDependencyReferenceKind.Name;
        }

        /// <summary>
        /// index と Unity compiler の path 解決を使って参照先を決めます。
        /// </summary>
        private static ReferenceResolution ResolveReference(
            string reference,
            AssemblyDependencyReferenceKind kind,
            IReadOnlyDictionary<string, List<int>> names,
            IReadOnlyDictionary<string, List<int>> guids,
            IReadOnlyDictionary<string, List<int>> paths,
            IAssemblyDependencySourceAdapter sourceAdapter)
        {
            var key = kind == AssemblyDependencyReferenceKind.Guid && reference.Length >= 5
                ? reference.Substring(5)
                : reference;
            var index = kind == AssemblyDependencyReferenceKind.Guid ? guids : names;
            if (index.TryGetValue(key, out var matches))
            {
                return matches.Count == 1
                    ? new ReferenceResolution(matches[0], false)
                    : new ReferenceResolution(-1, true);
            }

            if (sourceAdapter.TryResolveReferencePath(reference, out var resolvedPath))
            {
                resolvedPath = NormalizePath(resolvedPath);
                if (paths.TryGetValue(resolvedPath, out matches))
                {
                    return matches.Count == 1
                        ? new ReferenceResolution(matches[0], false)
                        : new ReferenceResolution(-1, true);
                }
            }

            return new ReferenceResolution(-1, false);
        }

        /// <summary>
        /// Player 対応 assembly から Editor 専用 assembly への各 edge を報告します。
        /// </summary>
        private static bool AddPlayerToEditorIssues(
            IReadOnlyList<AssemblyDependencyNode> nodes,
            IReadOnlyList<List<int>> dependencies,
            List<AssemblyDependencyIssue> issues)
        {
            for (var sourceIndex = 0; sourceIndex < nodes.Count; sourceIndex++)
            {
                if (nodes[sourceIndex].IsEditorOnly)
                {
                    continue;
                }

                for (var edgeIndex = 0; edgeIndex < dependencies[sourceIndex].Count; edgeIndex++)
                {
                    var targetIndex = dependencies[sourceIndex][edgeIndex];
                    if (!nodes[targetIndex].IsEditorOnly)
                    {
                        continue;
                    }

                    if (!TryAddIssue(issues, new AssemblyDependencyIssue(
                            AssemblyDependencyIssueKind.PlayerAssemblyReferencesEditorOnly,
                            nodes[sourceIndex].AssetPath,
                            nodes[targetIndex].AssetPath,
                            nodes[targetIndex].Name,
                            "Player 用 assembly が Editor 専用 assembly を参照しています。")))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Kosaraju 法を再帰なしで実行し、2 件以上の循環 component を返します。
        /// </summary>
        private static List<List<int>> FindCycles(IReadOnlyList<List<int>> graph, IReadOnlyList<List<int>> reverseGraph)
        {
            var visited = new bool[graph.Count];
            var finishOrder = new List<int>(graph.Count);
            for (var index = 0; index < graph.Count; index++)
            {
                if (!visited[index])
                {
                    AppendFinishOrder(index, graph, visited, finishOrder);
                }
            }

            Array.Clear(visited, 0, visited.Length);
            var cycles = new List<List<int>>();
            var stack = new Stack<int>();
            for (var orderIndex = finishOrder.Count - 1; orderIndex >= 0; orderIndex--)
            {
                var start = finishOrder[orderIndex];
                if (visited[start])
                {
                    continue;
                }

                var component = new List<int>();
                visited[start] = true;
                stack.Push(start);
                while (stack.Count > 0)
                {
                    var current = stack.Pop();
                    component.Add(current);
                    var edges = reverseGraph[current];
                    for (var edgeIndex = edges.Count - 1; edgeIndex >= 0; edgeIndex--)
                    {
                        var next = edges[edgeIndex];
                        if (!visited[next])
                        {
                            visited[next] = true;
                            stack.Push(next);
                        }
                    }
                }

                if (component.Count > 1)
                {
                    component.Sort();
                    cycles.Add(component);
                }
            }

            cycles.Sort((left, right) => left[0].CompareTo(right[0]));
            return cycles;
        }

        /// <summary>
        /// 再帰を使わず深さ優先探索の終了順を追加します。
        /// </summary>
        private static void AppendFinishOrder(int start, IReadOnlyList<List<int>> graph, bool[] visited, List<int> finishOrder)
        {
            var stack = new List<TraversalFrame>();
            visited[start] = true;
            stack.Add(new TraversalFrame(start, 0));
            while (stack.Count > 0)
            {
                var frameIndex = stack.Count - 1;
                var frame = stack[frameIndex];
                var edges = graph[frame.Node];
                if (frame.NextEdge < edges.Count)
                {
                    var next = edges[frame.NextEdge];
                    stack[frameIndex] = new TraversalFrame(frame.Node, frame.NextEdge + 1);
                    if (!visited[next])
                    {
                        visited[next] = true;
                        stack.Add(new TraversalFrame(next, 0));
                    }
                }
                else
                {
                    finishOrder.Add(frame.Node);
                    stack.RemoveAt(frameIndex);
                }
            }
        }

        /// <summary>
        /// 各循環 component を 1 件の問題として追加します。
        /// </summary>
        private static bool AddCycleIssues(
            IReadOnlyList<AssemblyDependencyNode> nodes,
            IReadOnlyList<List<int>> cycles,
            List<AssemblyDependencyIssue> issues)
        {
            for (var cycleIndex = 0; cycleIndex < cycles.Count; cycleIndex++)
            {
                var cycle = cycles[cycleIndex];
                if (!TryAddIssue(issues, new AssemblyDependencyIssue(
                        AssemblyDependencyIssueKind.DependencyCycle,
                        nodes[cycle[0]].AssetPath,
                        nodes[cycle[1]].AssetPath,
                        string.Empty,
                        $"{cycle.Count} 件の assembly で循環参照があります。")))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 指定数の空 adjacency list を作ります。
        /// </summary>
        private static List<List<int>> CreateGraph(int count)
        {
            var graph = new List<List<int>>(count);
            for (var index = 0; index < count; index++)
            {
                graph.Add(new List<int>());
            }

            return graph;
        }

        /// <summary>
        /// adjacency list を昇順に並べて重複 edge を除きます。
        /// </summary>
        private static void SortAndDeduplicateGraph(IReadOnlyList<List<int>> graph)
        {
            for (var nodeIndex = 0; nodeIndex < graph.Count; nodeIndex++)
            {
                var edges = graph[nodeIndex];
                edges.Sort();
                for (var edgeIndex = edges.Count - 1; edgeIndex > 0; edgeIndex--)
                {
                    if (edges[edgeIndex] == edges[edgeIndex - 1])
                    {
                        edges.RemoveAt(edgeIndex);
                    }
                }
            }
        }

        /// <summary>
        /// forward graph から逆参照 graph を作ります。
        /// </summary>
        private static List<List<int>> ReverseGraph(IReadOnlyList<List<int>> graph)
        {
            var reverse = CreateGraph(graph.Count);
            for (var sourceIndex = 0; sourceIndex < graph.Count; sourceIndex++)
            {
                for (var edgeIndex = 0; edgeIndex < graph[sourceIndex].Count; edgeIndex++)
                {
                    reverse[graph[sourceIndex][edgeIndex]].Add(sourceIndex);
                }
            }

            SortAndDeduplicateGraph(reverse);
            return reverse;
        }

        /// <summary>
        /// 内部 graph を結果 DTO 用の interface 一覧へ変換します。
        /// </summary>
        private static IReadOnlyList<IReadOnlyList<int>> ToReadOnlyGraph(IReadOnlyList<List<int>> graph)
        {
            var result = new IReadOnlyList<int>[graph.Count];
            for (var index = 0; index < graph.Count; index++)
            {
                result[index] = graph[index];
            }

            return result;
        }

        /// <summary>
        /// 問題上限を超えない場合だけ 1 件追加します。
        /// </summary>
        private static bool TryAddIssue(List<AssemblyDependencyIssue> issues, AssemblyDependencyIssue issue)
        {
            if (issues.Count >= MaximumIssues)
            {
                return false;
            }

            issues.Add(issue);
            return true;
        }

        /// <summary>
        /// 問題上限超過として結果を破棄します。
        /// </summary>
        private static bool FailIssueLimit(
            out AssemblyDependencyAuditResult result,
            out AssemblyDependencyAuditError error,
            out string errorMessage)
        {
            result = null;
            error = AssemblyDependencyAuditError.TooManyIssues;
            errorMessage = $"検出問題数が上限 {MaximumIssues} 件を超えています。";
            return false;
        }

        /// <summary>
        /// source を asset path、GUID の順に決定論的に並べます。
        /// </summary>
        private static int CompareSources(AssemblyDefinitionSource left, AssemblyDefinitionSource right)
        {
            var pathComparison = string.Compare(left.AssetPath, right.AssetPath, StringComparison.Ordinal);
            return pathComparison != 0
                ? pathComparison
                : string.Compare(left.Guid, right.Guid, StringComparison.Ordinal);
        }

        /// <summary>
        /// 問題を path、種類、関連 path、参照表記の順に並べます。
        /// </summary>
        private static int CompareIssues(AssemblyDependencyIssue left, AssemblyDependencyIssue right)
        {
            var comparison = string.Compare(left.AssetPath, right.AssetPath, StringComparison.Ordinal);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.Kind.CompareTo(right.Kind);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = string.Compare(left.RelatedAssetPath, right.RelatedAssetPath, StringComparison.Ordinal);
            return comparison != 0
                ? comparison
                : string.Compare(left.Reference, right.Reference, StringComparison.Ordinal);
        }

        /// <summary>
        /// path の区切り文字を Unity 形式へ統一します。
        /// </summary>
        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }

        /// <summary>
        /// JsonUtility で解析した 1 source の作業用情報です。
        /// </summary>
        private sealed class ParsedSource
        {
            /// <summary>解析結果を保持します。</summary>
            internal ParsedSource(
                AssemblyDefinitionSource source,
                bool isJsonValid,
                string name,
                IReadOnlyList<string> includePlatforms,
                IReadOnlyList<string> excludePlatforms,
                IReadOnlyList<string> references)
            {
                Source = source;
                IsJsonValid = isJsonValid;
                Name = name;
                IncludePlatforms = includePlatforms;
                ExcludePlatforms = excludePlatforms;
                References = references;
            }

            /// <summary>元 source です。</summary>
            internal AssemblyDefinitionSource Source { get; }

            /// <summary>JSON の解析に成功したかを示します。</summary>
            internal bool IsJsonValid { get; }

            /// <summary>assembly 名です。</summary>
            internal string Name { get; }

            /// <summary>includePlatforms です。</summary>
            internal IReadOnlyList<string> IncludePlatforms { get; }

            /// <summary>excludePlatforms です。</summary>
            internal IReadOnlyList<string> ExcludePlatforms { get; }

            /// <summary>元の参照表記です。</summary>
            internal IReadOnlyList<string> References { get; }
        }

        /// <summary>
        /// 参照先 index と曖昧性を保持します。
        /// </summary>
        private readonly struct ReferenceResolution
        {
            /// <summary>解決結果を保持します。</summary>
            internal ReferenceResolution(int index, bool isAmbiguous)
            {
                Index = index;
                IsAmbiguous = isAmbiguous;
            }

            /// <summary>一意に解決できた index、または -1 です。</summary>
            internal int Index { get; }

            /// <summary>複数候補があったかを示します。</summary>
            internal bool IsAmbiguous { get; }
        }

        /// <summary>
        /// 反復深さ優先探索の node と次 edge を保持します。
        /// </summary>
        private readonly struct TraversalFrame
        {
            /// <summary>探索位置を保持します。</summary>
            internal TraversalFrame(int node, int nextEdge)
            {
                Node = node;
                NextEdge = nextEdge;
            }

            /// <summary>探索中の node index です。</summary>
            internal int Node { get; }

            /// <summary>次に調べる edge index です。</summary>
            internal int NextEdge { get; }
        }
    }
}
