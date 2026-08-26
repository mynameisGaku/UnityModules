using System;
using System.Collections.Generic;

namespace AssemblyDependencyAudit.Editor
{
    /// <summary>
    /// asmref source 全体を target 解決結果と独立した問題一覧へ変換します。
    /// </summary>
    internal static class AssemblyReferenceAnalyzer
    {
        /// <summary>1 回に監査できる asmref 数です。</summary>
        internal const int MaximumAssemblyReferences = 10000;

        /// <summary>
        /// asmdef graph を変更せず、全 asmref の target integrity を監査します。
        /// </summary>
        internal static bool TryAnalyze(
            IReadOnlyList<AssemblyReferenceSource> sources,
            AssemblyDependencyAuditResult assemblyResult,
            out AssemblyDependencyAuditResult result,
            out AssemblyDependencyAuditError error,
            out string errorMessage)
        {
            result = null;
            error = AssemblyDependencyAuditError.None;
            errorMessage = string.Empty;
            if (sources == null || assemblyResult == null)
            {
                error = AssemblyDependencyAuditError.SourceUnavailable;
                errorMessage = "asmref 監査 source を取得できませんでした。";
                return false;
            }

            if (sources.Count > MaximumAssemblyReferences)
            {
                error = AssemblyDependencyAuditError.TooManyAssemblyReferences;
                errorMessage = $"asmref 数が上限 {MaximumAssemblyReferences} 件を超えています。";
                return false;
            }

            var orderedSources = new List<AssemblyReferenceSource>(sources.Count);
            for (var index = 0; index < sources.Count; index++)
            {
                var source = sources[index];
                if (source == null)
                {
                    error = AssemblyDependencyAuditError.SourceUnavailable;
                    errorMessage = "null の asmref source が含まれています。";
                    return false;
                }

                if (source.Json.Length > AssemblyDependencyAnalyzer.MaximumSourceCharacters)
                {
                    error = AssemblyDependencyAuditError.SourceTooLarge;
                    errorMessage = $"{source.AssetPath} が文字数上限 {AssemblyDependencyAnalyzer.MaximumSourceCharacters} を超えています。";
                    return false;
                }

                orderedSources.Add(source);
            }

            orderedSources.Sort(CompareSources);
            var names = BuildAssemblyIndex(assemblyResult.Assemblies, node => node.Name, StringComparer.OrdinalIgnoreCase, false);
            var assemblyGuids = BuildAssemblyIndex(assemblyResult.Assemblies, node => node.Guid, StringComparer.OrdinalIgnoreCase, true);
            var assemblyReferenceGuids = BuildSourceGuidIndex(orderedSources);
            var targets = new List<AssemblyReferenceTarget>(orderedSources.Count);
            var issues = new List<AssemblyDependencyIssue>(assemblyResult.Issues.Count + orderedSources.Count);
            for (var index = 0; index < assemblyResult.Issues.Count; index++)
            {
                issues.Add(assemblyResult.Issues[index]);
            }

            for (var index = 0; index < orderedSources.Count; index++)
            {
                var source = orderedSources[index];
                var status = AssemblyReferenceJsonParser.Parse(source.Json, out var rawReference);
                if (status == AssemblyReferenceJsonParseStatus.InvalidJson)
                {
                    if (!TryAddIssue(issues, new AssemblyDependencyIssue(
                            AssemblyDependencyIssueKind.InvalidAssemblyReferenceJson,
                            source.AssetPath,
                            string.Empty,
                            string.Empty,
                            "asmref JSON の構文、key、または reference の型が不正です。")))
                    {
                        return FailIssueLimit(out result, out error, out errorMessage);
                    }

                    targets.Add(new AssemblyReferenceTarget(
                        source.AssetPath,
                        string.Empty,
                        AssemblyReferenceTargetKind.Unknown,
                        string.Empty));
                    continue;
                }

                if (status == AssemblyReferenceJsonParseStatus.MissingReference)
                {
                    if (!TryAddIssue(issues, new AssemblyDependencyIssue(
                            AssemblyDependencyIssueKind.MissingAssemblyReference,
                            source.AssetPath,
                            string.Empty,
                            rawReference,
                            "asmref に空でない reference property が exactly one 必要です。")))
                    {
                        return FailIssueLimit(out result, out error, out errorMessage);
                    }

                    targets.Add(new AssemblyReferenceTarget(
                        source.AssetPath,
                        rawReference,
                        AssemblyReferenceTargetKind.Unknown,
                        string.Empty));
                    continue;
                }

                var kind = HasGuidPrefix(rawReference)
                    ? AssemblyReferenceTargetKind.Guid
                    : AssemblyReferenceTargetKind.Name;
                var resolution = Resolve(
                    rawReference,
                    kind,
                    names,
                    assemblyGuids,
                    assemblyReferenceGuids,
                    assemblyResult.Assemblies);
                var resolvedTargetPath = resolution.Index >= 0
                    ? assemblyResult.Assemblies[resolution.Index].AssetPath
                    : string.Empty;
                targets.Add(new AssemblyReferenceTarget(source.AssetPath, rawReference, kind, resolvedTargetPath));

                if (resolution.IsAmbiguous)
                {
                    if (!TryAddIssue(issues, new AssemblyDependencyIssue(
                            AssemblyDependencyIssueKind.AmbiguousAssemblyReference,
                            source.AssetPath,
                            string.Empty,
                            rawReference,
                            "asmref の target asmdef を一意に決められません。")))
                    {
                        return FailIssueLimit(out result, out error, out errorMessage);
                    }
                }
                else if (resolution.Index < 0 && !TryAddIssue(issues, new AssemblyDependencyIssue(
                        AssemblyDependencyIssueKind.UnresolvedAssemblyReference,
                        source.AssetPath,
                        resolution.RelatedIndex >= 0
                            ? assemblyResult.Assemblies[resolution.RelatedIndex].AssetPath
                            : string.Empty,
                        rawReference,
                        resolution.RelatedIndex >= 0
                            ? "asmref の target asmdef が有効でないため解決できません。"
                            : "asmref の target asmdef が見つかりません。")))
                {
                    return FailIssueLimit(out result, out error, out errorMessage);
                }
            }

            targets.Sort(CompareTargets);
            issues.Sort(CompareIssues);
            result = new AssemblyDependencyAuditResult(
                assemblyResult.Assemblies,
                issues,
                assemblyResult.Dependencies,
                assemblyResult.Dependents,
                assemblyResult.Cycles,
                targets);
            return true;
        }

        /// <summary>assembly 名または GUID ごとの全一致 index を作ります。</summary>
        private static IReadOnlyDictionary<string, List<int>> BuildAssemblyIndex(
            IReadOnlyList<AssemblyDependencyNode> assemblies,
            Func<AssemblyDependencyNode, string> selector,
            IEqualityComparer<string> comparer,
            bool requireHexGuid)
        {
            var index = new Dictionary<string, List<int>>(comparer);
            for (var assemblyIndex = 0; assemblyIndex < assemblies.Count; assemblyIndex++)
            {
                var key = selector(assemblies[assemblyIndex]) ?? string.Empty;
                if (string.IsNullOrEmpty(key) || (requireHexGuid && !IsHexGuid(key)))
                {
                    continue;
                }

                if (!index.TryGetValue(key, out var matches))
                {
                    matches = new List<int>();
                    index.Add(key, matches);
                }

                matches.Add(assemblyIndex);
            }

            return index;
        }

        /// <summary>同時列挙した asmref 自身の有効な GUID を index 化します。</summary>
        private static IReadOnlyDictionary<string, int> BuildSourceGuidIndex(IReadOnlyList<AssemblyReferenceSource> sources)
        {
            var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
            {
                var guid = sources[sourceIndex].Guid;
                if (!IsHexGuid(guid))
                {
                    continue;
                }

                index.TryGetValue(guid, out var count);
                index[guid] = count + 1;
            }

            return index;
        }

        /// <summary>
        /// target indexを解決します。asmdef GUIDとasmref自身のGUIDが衝突した場合は曖昧とします。
        /// </summary>
        private static Resolution Resolve(
            string rawReference,
            AssemblyReferenceTargetKind kind,
            IReadOnlyDictionary<string, List<int>> names,
            IReadOnlyDictionary<string, List<int>> assemblyGuids,
            IReadOnlyDictionary<string, int> assemblyReferenceGuids,
            IReadOnlyList<AssemblyDependencyNode> assemblies)
        {
            if (kind == AssemblyReferenceTargetKind.Name)
            {
                if (!names.TryGetValue(rawReference, out var matches))
                {
                    return new Resolution(-1, -1, false);
                }

                return matches.Count == 1
                    ? ResolveUniqueCandidate(matches[0], assemblies)
                    : new Resolution(-1, -1, true);
            }

            if (rawReference.Length != 37 || !IsHexGuid(rawReference.Substring(5)))
            {
                return new Resolution(-1, -1, false);
            }

            var guid = rawReference.Substring(5);
            if (!assemblyGuids.TryGetValue(guid, out var guidMatches))
            {
                return new Resolution(-1, -1, false);
            }

            if (guidMatches.Count != 1 || assemblyReferenceGuids.ContainsKey(guid))
            {
                return new Resolution(-1, -1, true);
            }

            return ResolveUniqueCandidate(guidMatches[0], assemblies);
        }

        /// <summary>一意なcandidate自体が有効なasmdefかを検証します。</summary>
        private static Resolution ResolveUniqueCandidate(
            int candidateIndex,
            IReadOnlyList<AssemblyDependencyNode> assemblies)
        {
            var candidate = assemblies[candidateIndex];
            return candidate.IsJsonValid && !string.IsNullOrWhiteSpace(candidate.Name)
                ? new Resolution(candidateIndex, -1, false)
                : new Resolution(-1, candidateIndex, false);
        }

        /// <summary>case-insensitiveなGUID prefixを持つかを返します。</summary>
        private static bool HasGuidPrefix(string reference)
        {
            return reference != null &&
                reference.StartsWith("GUID:", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>32文字すべてが16進数かを返します。</summary>
        private static bool IsHexGuid(string value)
        {
            if (value == null || value.Length != 32)
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if ((character < '0' || character > '9') &&
                    (character < 'a' || character > 'f') &&
                    (character < 'A' || character > 'F'))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>source を path、GUID、JSON の順に並べます。</summary>
        private static int CompareSources(AssemblyReferenceSource left, AssemblyReferenceSource right)
        {
            var comparison = string.Compare(left.AssetPath, right.AssetPath, StringComparison.Ordinal);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = string.Compare(left.Guid, right.Guid, StringComparison.Ordinal);
            return comparison != 0
                ? comparison
                : string.Compare(left.Json, right.Json, StringComparison.Ordinal);
        }

        /// <summary>target を path、元参照、種別、解決先の順に並べます。</summary>
        private static int CompareTargets(AssemblyReferenceTarget left, AssemblyReferenceTarget right)
        {
            var comparison = string.Compare(left.AssetPath, right.AssetPath, StringComparison.Ordinal);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = string.Compare(left.RawReference, right.RawReference, StringComparison.Ordinal);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.Kind.CompareTo(right.Kind);
            return comparison != 0
                ? comparison
                : string.Compare(left.ResolvedTargetAssetPath, right.ResolvedTargetAssetPath, StringComparison.Ordinal);
        }

        /// <summary>問題を既存監査と同じ決定論的な順序へ並べます。</summary>
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

        /// <summary>問題数上限内なら追加します。</summary>
        private static bool TryAddIssue(List<AssemblyDependencyIssue> issues, AssemblyDependencyIssue issue)
        {
            if (issues.Count >= AssemblyDependencyAnalyzer.MaximumIssues)
            {
                return false;
            }

            issues.Add(issue);
            return true;
        }

        /// <summary>問題数上限超過を統一した結果へ変換します。</summary>
        private static bool FailIssueLimit(
            out AssemblyDependencyAuditResult result,
            out AssemblyDependencyAuditError error,
            out string errorMessage)
        {
            result = null;
            error = AssemblyDependencyAuditError.TooManyIssues;
            errorMessage = $"検出問題数が上限 {AssemblyDependencyAnalyzer.MaximumIssues} 件を超えています。";
            return false;
        }

        /// <summary>target index と曖昧性を保持します。</summary>
        private readonly struct Resolution
        {
            /// <summary>解決結果を保持します。</summary>
            internal Resolution(int index, int relatedIndex, bool isAmbiguous)
            {
                Index = index;
                RelatedIndex = relatedIndex;
                IsAmbiguous = isAmbiguous;
            }

            /// <summary>解決した assembly index、未解決では -1 です。</summary>
            internal int Index { get; }

            /// <summary>存在するが有効でないtarget assembly index、該当なしでは -1 です。</summary>
            internal int RelatedIndex { get; }

            /// <summary>候補を一意に決められなかったかを返します。</summary>
            internal bool IsAmbiguous { get; }
        }
    }
}
