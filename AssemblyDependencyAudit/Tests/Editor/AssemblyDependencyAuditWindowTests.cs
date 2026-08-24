using System;
using System.Collections.Generic;
using NUnit.Framework;
using AuditEditor = AssemblyDependencyAudit.Editor;

namespace AssemblyDependencyAudit.Tests
{
    /// <summary>
    /// Window の表示上限と pure filter 契約を Unity GUI なしで検証します。
    /// </summary>
    internal sealed class AssemblyDependencyAuditWindowTests
    {
        /// <summary>
        /// 一覧は設計上の 500 行上限を維持します。
        /// </summary>
        [Test]
        public void MaximumDisplayedRows_IsFiveHundred()
        {
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.MaximumDisplayedRows, Is.EqualTo(500));
        }

        /// <summary>
        /// 検索は前後空白を除き、名前、path、GUID を大文字小文字無視で照合します。
        /// </summary>
        [TestCase("  gameplay  ")]
        [TestCase("packages/com.example")]
        [TestCase("ABC-123")]
        public void MatchesFilters_SearchesNamePathAndGuidOrdinalIgnoreCase(string searchText)
        {
            var node = CreateNode("Gameplay.Core", "Packages/com.example/Gameplay.Core.asmdef", "abc-123", false);

            var matches = AuditEditor.AssemblyDependencyAuditWindow.MatchesFilters(
                node,
                searchText,
                AuditEditor.AssemblyDependencyAuditWindow.ScopeFilter.All,
                AuditEditor.AssemblyDependencyAuditWindow.PlatformFilter.All,
                AuditEditor.AssemblyDependencyAuditWindow.IssueFilter.All,
                false);

            Assert.That(matches, Is.True);
        }

        /// <summary>
        /// Assets と Packages は path segment の境界を含めて判定します。
        /// </summary>
        [Test]
        public void MatchesFilters_UsesExactScopeBoundaries()
        {
            var assets = CreateNode("AssetsNode", "Assets/Editor/A.asmdef", "guid-a", false);
            var packages = CreateNode("PackageNode", "Packages/com.example/B.asmdef", "guid-b", false);
            var lookalike = CreateNode("Lookalike", "AssetsBackup/C.asmdef", "guid-c", false);

            Assert.That(MatchesScope(assets, AuditEditor.AssemblyDependencyAuditWindow.ScopeFilter.Assets), Is.True);
            Assert.That(MatchesScope(assets, AuditEditor.AssemblyDependencyAuditWindow.ScopeFilter.Packages), Is.False);
            Assert.That(MatchesScope(packages, AuditEditor.AssemblyDependencyAuditWindow.ScopeFilter.Packages), Is.True);
            Assert.That(MatchesScope(packages, AuditEditor.AssemblyDependencyAuditWindow.ScopeFilter.Assets), Is.False);
            Assert.That(MatchesScope(lookalike, AuditEditor.AssemblyDependencyAuditWindow.ScopeFilter.Assets), Is.False);
        }

        /// <summary>
        /// Editor 専用、Player 対応、問題有無の filter を独立に適用します。
        /// </summary>
        [Test]
        public void MatchesFilters_AppliesPlatformAndIssueFilters()
        {
            var editorOnly = CreateNode("EditorOnly", "Assets/EditorOnly.asmdef", "guid-editor", true);
            var playerCapable = CreateNode("Player", "Assets/Player.asmdef", "guid-player", false);

            Assert.That(MatchesPlatform(editorOnly, AuditEditor.AssemblyDependencyAuditWindow.PlatformFilter.EditorOnly), Is.True);
            Assert.That(MatchesPlatform(editorOnly, AuditEditor.AssemblyDependencyAuditWindow.PlatformFilter.PlayerCapable), Is.False);
            Assert.That(MatchesPlatform(playerCapable, AuditEditor.AssemblyDependencyAuditWindow.PlatformFilter.PlayerCapable), Is.True);
            Assert.That(MatchesIssues(playerCapable, AuditEditor.AssemblyDependencyAuditWindow.IssueFilter.WithIssues, true), Is.True);
            Assert.That(MatchesIssues(playerCapable, AuditEditor.AssemblyDependencyAuditWindow.IssueFilter.WithIssues, false), Is.False);
            Assert.That(MatchesIssues(playerCapable, AuditEditor.AssemblyDependencyAuditWindow.IssueFilter.WithoutIssues, false), Is.True);
            Assert.That(MatchesIssues(playerCapable, AuditEditor.AssemblyDependencyAuditWindow.IssueFilter.WithoutIssues, true), Is.False);
        }

        /// <summary>
        /// null node と一致しない検索文字列は表示対象にしません。
        /// </summary>
        [Test]
        public void MatchesFilters_RejectsNullNodeAndSearchMiss()
        {
            var node = CreateNode("A", "Assets/A.asmdef", "guid-a", false);

            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.MatchesFilters(
                null,
                string.Empty,
                AuditEditor.AssemblyDependencyAuditWindow.ScopeFilter.All,
                AuditEditor.AssemblyDependencyAuditWindow.PlatformFilter.All,
                AuditEditor.AssemblyDependencyAuditWindow.IssueFilter.All,
                false), Is.False);
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.MatchesFilters(
                node,
                "missing",
                AuditEditor.AssemblyDependencyAuditWindow.ScopeFilter.All,
                AuditEditor.AssemblyDependencyAuditWindow.PlatformFilter.All,
                AuditEditor.AssemblyDependencyAuditWindow.IssueFilter.All,
                false), Is.False);
        }

        /// <summary>
        /// 循環問題を component の代表二件だけでなく全 member へ割り当てます。
        /// </summary>
        [Test]
        public void BuildIssueIndicesByPath_AssignsCycleIssueToEveryComponentMember()
        {
            var assemblies = new[]
            {
                CreateNode("A", "Assets/A.asmdef", "guid-a", false),
                CreateNode("B", "Assets/B.asmdef", "guid-b", false),
                CreateNode("C", "Assets/C.asmdef", "guid-c", false)
            };
            var issues = new[]
            {
                new AuditEditor.AssemblyDependencyIssue(
                    AuditEditor.AssemblyDependencyIssueKind.DependencyCycle,
                    "Assets/A.asmdef",
                    "Assets/B.asmdef",
                    string.Empty,
                    "3 件の assembly で循環参照があります。")
            };
            var emptyGraph = new IReadOnlyList<int>[]
            {
                Array.Empty<int>(),
                Array.Empty<int>(),
                Array.Empty<int>()
            };
            var cycles = new IReadOnlyList<int>[] { new[] { 0, 1, 2 } };
            var result = new AuditEditor.AssemblyDependencyAuditResult(
                assemblies,
                issues,
                emptyGraph,
                emptyGraph,
                cycles);

            var issueIndicesByPath = AuditEditor.AssemblyDependencyAuditWindow.BuildIssueIndicesByPath(result);

            Assert.That(issueIndicesByPath.Keys, Is.EquivalentTo(new[]
            {
                "Assets/A.asmdef",
                "Assets/B.asmdef",
                "Assets/C.asmdef"
            }));
            Assert.That(issueIndicesByPath["Assets/A.asmdef"], Is.EqualTo(new[] { 0 }));
            Assert.That(issueIndicesByPath["Assets/B.asmdef"], Is.EqualTo(new[] { 0 }));
            Assert.That(issueIndicesByPath["Assets/C.asmdef"], Is.EqualTo(new[] { 0 }));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.BuildIssueIndicesByPath(null), Is.Empty);
        }

        /// <summary>scope filter だけを指定して結果を返します。</summary>
        private static bool MatchesScope(
            AuditEditor.AssemblyDependencyNode node,
            AuditEditor.AssemblyDependencyAuditWindow.ScopeFilter scope)
        {
            return AuditEditor.AssemblyDependencyAuditWindow.MatchesFilters(
                node,
                string.Empty,
                scope,
                AuditEditor.AssemblyDependencyAuditWindow.PlatformFilter.All,
                AuditEditor.AssemblyDependencyAuditWindow.IssueFilter.All,
                false);
        }

        /// <summary>platform filter だけを指定して結果を返します。</summary>
        private static bool MatchesPlatform(
            AuditEditor.AssemblyDependencyNode node,
            AuditEditor.AssemblyDependencyAuditWindow.PlatformFilter platform)
        {
            return AuditEditor.AssemblyDependencyAuditWindow.MatchesFilters(
                node,
                string.Empty,
                AuditEditor.AssemblyDependencyAuditWindow.ScopeFilter.All,
                platform,
                AuditEditor.AssemblyDependencyAuditWindow.IssueFilter.All,
                false);
        }

        /// <summary>問題 filter だけを指定して結果を返します。</summary>
        private static bool MatchesIssues(
            AuditEditor.AssemblyDependencyNode node,
            AuditEditor.AssemblyDependencyAuditWindow.IssueFilter issues,
            bool hasIssue)
        {
            return AuditEditor.AssemblyDependencyAuditWindow.MatchesFilters(
                node,
                string.Empty,
                AuditEditor.AssemblyDependencyAuditWindow.ScopeFilter.All,
                AuditEditor.AssemblyDependencyAuditWindow.PlatformFilter.All,
                issues,
                hasIssue);
        }

        /// <summary>filter 入力用の最小 node を作ります。</summary>
        private static AuditEditor.AssemblyDependencyNode CreateNode(
            string name,
            string assetPath,
            string guid,
            bool editorOnly)
        {
            return new AuditEditor.AssemblyDependencyNode(
                name,
                assetPath,
                guid,
                true,
                editorOnly ? new[] { "Editor" } : Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<AuditEditor.AssemblyDependencyReference>());
        }
    }
}
