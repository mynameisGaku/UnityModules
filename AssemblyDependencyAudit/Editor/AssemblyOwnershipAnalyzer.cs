using System;
using System.Collections.Generic;

namespace AssemblyDependencyAudit.Editor
{
    /// <summary>
    /// 同じfolderに複数あるasmdef・asmref owner候補を検出します。
    /// </summary>
    internal static class AssemblyOwnershipAnalyzer
    {
        /// <summary>
        /// graphとtarget解決を変えず、path-levelのowner配置問題を追加します。
        /// </summary>
        internal static bool TryAnalyze(
            AssemblyDependencyAuditResult sourceResult,
            out AssemblyDependencyAuditResult result,
            out AssemblyDependencyAuditError error,
            out string errorMessage)
        {
            result = null;
            error = AssemblyDependencyAuditError.None;
            errorMessage = string.Empty;
            if (sourceResult == null)
            {
                error = AssemblyDependencyAuditError.SourceUnavailable;
                errorMessage = "assembly owner監査sourceを取得できませんでした。";
                return false;
            }

            if (sourceResult.Issues.Count > AssemblyDependencyAnalyzer.MaximumIssues)
            {
                return FailIssueLimit(out result, out error, out errorMessage);
            }

            var ownersByFolder = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            for (var index = 0; index < sourceResult.Assemblies.Count; index++)
            {
                var assembly = sourceResult.Assemblies[index];
                if (assembly == null || !TryAddOwner(ownersByFolder, assembly.AssetPath))
                {
                    return FailSource(assembly?.AssetPath, out result, out error, out errorMessage);
                }
            }

            for (var index = 0; index < sourceResult.AssemblyReferences.Count; index++)
            {
                var assemblyReference = sourceResult.AssemblyReferences[index];
                if (assemblyReference == null || !TryAddOwner(ownersByFolder, assemblyReference.AssetPath))
                {
                    return FailSource(assemblyReference?.AssetPath, out result, out error, out errorMessage);
                }
            }

            var issues = new List<AssemblyDependencyIssue>();
            for (var index = 0; index < sourceResult.Issues.Count; index++)
            {
                var issue = sourceResult.Issues[index];
                if (issue == null)
                {
                    return FailSource(string.Empty, out result, out error, out errorMessage);
                }

                issues.Add(issue);
            }

            foreach (var pair in ownersByFolder)
            {
                if (pair.Value.Count < 2)
                {
                    continue;
                }

                var owners = new List<string>(pair.Value);
                for (var ownerIndex = 0; ownerIndex < owners.Count; ownerIndex++)
                {
                    if (!TryAddIssue(issues, new AssemblyDependencyIssue(
                            AssemblyDependencyIssueKind.MultipleAssemblyOwnersInFolder,
                            owners[ownerIndex],
                            ownerIndex == 0 ? owners[1] : owners[0],
                            pair.Key,
                            $"同じfolderに{owners.Count}件のasmdefまたはasmrefがあります。JSONの有効性に関係なく、scriptの所属を指定するassetは1件にしてください。")))
                    {
                        return FailIssueLimit(out result, out error, out errorMessage);
                    }
                }
            }

            issues.Sort(CompareIssues);
            result = new AssemblyDependencyAuditResult(
                sourceResult.Assemblies,
                issues,
                sourceResult.Dependencies,
                sourceResult.Dependents,
                sourceResult.Cycles,
                sourceResult.AssemblyReferences);
            return true;
        }

        /// <summary>owner pathをexact parent folderごとに重複なく追加します。</summary>
        private static bool TryAddOwner(
            SortedDictionary<string, SortedSet<string>> ownersByFolder,
            string assetPath)
        {
            var normalizedPath = AssemblyDefinitionSourcePathUtility.NormalizeAssetPath(assetPath);
            var separatorIndex = normalizedPath.LastIndexOf('/');
            if (separatorIndex <= 0 || separatorIndex >= normalizedPath.Length - 1)
            {
                return false;
            }

            var folder = normalizedPath.Substring(0, separatorIndex);
            if (!ownersByFolder.TryGetValue(folder, out var owners))
            {
                owners = new SortedSet<string>(StringComparer.Ordinal);
                ownersByFolder.Add(folder, owners);
            }

            owners.Add(normalizedPath);
            return true;
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

        /// <summary>source path不正を部分結果なしの失敗へ変換します。</summary>
        private static bool FailSource(
            string assetPath,
            out AssemblyDependencyAuditResult result,
            out AssemblyDependencyAuditError error,
            out string errorMessage)
        {
            result = null;
            error = AssemblyDependencyAuditError.SourceUnavailable;
            errorMessage = string.IsNullOrEmpty(assetPath)
                ? "assembly owner監査sourceに不正な項目があります。"
                : $"{assetPath} の親folderを特定できませんでした。";
            return false;
        }

        /// <summary>問題数上限超過を部分結果なしの失敗へ変換します。</summary>
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
    }
}
