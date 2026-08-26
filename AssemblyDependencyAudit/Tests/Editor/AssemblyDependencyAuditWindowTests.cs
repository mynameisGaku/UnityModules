using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using AuditEditor = AssemblyDependencyAudit.Editor;

namespace AssemblyDependencyAudit.Tests
{
    /// <summary>
    /// Window の表示上限、paging、filter、選択同期を描画操作なしで検証します。
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
        /// search と詳細表示の安全上限を公開契約として固定します。
        /// </summary>
        [Test]
        public void TextLimits_AreFixedToExpectedBoundaries()
        {
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.MaximumSearchCharacters, Is.EqualTo(512));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.MaximumDisplayedTextCharacters, Is.EqualTo(4096));
            Assert.That(
                AuditEditor.AssemblyDependencyAuditWindow.MaximumDeclaredReferenceRowValueCharacters,
                Is.EqualTo(160));
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

        /// <summary>
        /// asmref は path、元 reference、解決先、kind、scope、問題有無を独立に filter できます。
        /// </summary>
        [Test]
        public void MatchesAssemblyReferenceFilters_SearchesAllFieldsAndAppliesScopeAndIssues()
        {
            var target = new AuditEditor.AssemblyReferenceTarget(
                "Assets/Feature/Target.asmref",
                "GUID:ABCDEF0123456789ABCDEF0123456789",
                AuditEditor.AssemblyReferenceTargetKind.Guid,
                "Packages/com.example/Runtime/Resolved.asmdef");

            Assert.That(MatchesAssemblyReference(target, "feature/target", ScopeAll(), IssueAll(), false), Is.True);
            Assert.That(MatchesAssemblyReference(target, "guid:abcdef", ScopeAll(), IssueAll(), false), Is.True);
            Assert.That(MatchesAssemblyReference(target, "resolved.asmdef", ScopeAll(), IssueAll(), false), Is.True);
            Assert.That(MatchesAssemblyReference(target, "gUiD", ScopeAll(), IssueAll(), false), Is.True);
            Assert.That(MatchesAssemblyReference(target, "missing", ScopeAll(), IssueAll(), false), Is.False);
            Assert.That(MatchesAssemblyReference(
                target,
                string.Empty,
                AuditEditor.AssemblyDependencyAuditWindow.ScopeFilter.Assets,
                IssueAll(),
                false), Is.True);
            Assert.That(MatchesAssemblyReference(
                target,
                string.Empty,
                AuditEditor.AssemblyDependencyAuditWindow.ScopeFilter.Packages,
                IssueAll(),
                false), Is.False);
            Assert.That(MatchesAssemblyReference(
                target,
                string.Empty,
                ScopeAll(),
                AuditEditor.AssemblyDependencyAuditWindow.IssueFilter.WithIssues,
                true), Is.True);
            Assert.That(MatchesAssemblyReference(
                target,
                string.Empty,
                ScopeAll(),
                AuditEditor.AssemblyDependencyAuditWindow.IssueFilter.WithIssues,
                false), Is.False);
            Assert.That(MatchesAssemblyReference(
                target,
                string.Empty,
                ScopeAll(),
                AuditEditor.AssemblyDependencyAuditWindow.IssueFilter.WithoutIssues,
                false), Is.True);
            Assert.That(MatchesAssemblyReference(
                target,
                string.Empty,
                ScopeAll(),
                AuditEditor.AssemblyDependencyAuditWindow.IssueFilter.WithoutIssues,
                true), Is.False);
            Assert.That(MatchesAssemblyReference(null, string.Empty, ScopeAll(), IssueAll(), false), Is.False);
        }

        /// <summary>
        /// search は512文字のprefixだけを評価し、surrogate pairを途中で分断しません。
        /// </summary>
        [Test]
        public void AssemblyReferenceSearch_UsesBoundedSurrogateSafePrefix()
        {
            var exactPrefix = new string('x', AuditEditor.AssemblyDependencyAuditWindow.MaximumSearchCharacters);
            var target = new AuditEditor.AssemblyReferenceTarget(
                "Assets/Search.asmref",
                exactPrefix,
                AuditEditor.AssemblyReferenceTargetKind.Name,
                string.Empty);
            var overLimitSearch = exactPrefix + "not-present";
            var surrogateBoundarySearch =
                new string('y', AuditEditor.AssemblyDependencyAuditWindow.MaximumSearchCharacters - 1) +
                "\uD83D\uDE00tail";
            var normalized = InvokeStaticPrivate<string>("NormalizeSearchText", surrogateBoundarySearch);

            Assert.That(MatchesAssemblyReference(target, overLimitSearch, ScopeAll(), IssueAll(), false), Is.True);
            Assert.That(normalized,
                Is.EqualTo(new string('y', AuditEditor.AssemblyDependencyAuditWindow.MaximumSearchCharacters - 1)));
            Assert.That(ContainsUnpairedSurrogate(normalized), Is.False);
            Assert.That(normalized.Length,
                Is.LessThanOrEqualTo(AuditEditor.AssemblyDependencyAuditWindow.MaximumSearchCharacters));
        }

        /// <summary>
        /// asmref sourceだけを持つ finding もそのpathへ一度だけ割り当てます。
        /// </summary>
        [Test]
        public void BuildIssueIndicesByPath_AssignsSourceOnlyAssemblyReferenceIssue()
        {
            const string assetPath = "Assets/Feature/Missing.asmref";
            var issue = CreateIssue(
                AuditEditor.AssemblyDependencyIssueKind.UnresolvedAssemblyReference,
                assetPath,
                string.Empty,
                "Missing");
            var result = CreateResult(
                Array.Empty<AuditEditor.AssemblyDependencyNode>(),
                new[] { issue },
                new[]
                {
                    new AuditEditor.AssemblyReferenceTarget(
                        assetPath,
                        "Missing",
                        AuditEditor.AssemblyReferenceTargetKind.Name,
                        string.Empty)
                });

            var issueIndicesByPath = AuditEditor.AssemblyDependencyAuditWindow.BuildIssueIndicesByPath(result);

            Assert.That(issueIndicesByPath.Keys, Is.EqualTo(new[] { assetPath }));
            Assert.That(issueIndicesByPath[assetPath], Is.EqualTo(new[] { 0 }));
        }

        /// <summary>
        /// 501件目もexact path searchでfiltered一覧の先頭へ到達し、500行capの外へ隠れません。
        /// </summary>
        [Test]
        public void AssemblyReferenceSearch_ReachesExactPathAtIndexFiveHundred()
        {
            var targets = CreateAssemblyReferences(501);
            var expectedPath = targets[500].AssetPath;
            var visibleIndices = targets
                .Select((target, index) => new { target, index })
                .Where(item => MatchesAssemblyReference(item.target, expectedPath, ScopeAll(), IssueAll(), false))
                .Select(item => item.index)
                .ToArray();

            Assert.That(visibleIndices, Is.EqualTo(new[] { 500 }));
            Assert.That(visibleIndices.Single(),
                Is.GreaterThanOrEqualTo(AuditEditor.AssemblyDependencyAuditWindow.MaximumDisplayedRows));
        }

        /// <summary>
        /// 501件を2pageへ分け、範囲外pageを最後の有効pageへ制限します。
        /// </summary>
        [Test]
        public void AssemblyReferencePaging_ExposesSecondPageAndClampsBounds()
        {
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.GetAssemblyReferencePageCount(0), Is.EqualTo(1));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.GetAssemblyReferencePageCount(500), Is.EqualTo(1));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.GetAssemblyReferencePageCount(501), Is.EqualTo(2));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.ClampAssemblyReferencePage(-1, 2), Is.Zero);
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.ClampAssemblyReferencePage(2, 2), Is.EqualTo(1));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.GetAssemblyReferencePageStart(0, 501), Is.Zero);
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.GetAssemblyReferencePageStart(1, 501), Is.EqualTo(500));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.GetAssemblyReferencePageStart(99, 501), Is.EqualTo(500));
        }

        /// <summary>
        /// SCC memberは500件単位で最大10,000件まで全pageへ到達できます。
        /// </summary>
        [Test]
        public void CycleComponentPaging_HandlesZeroExactOverflowDeepAndMaximumCounts()
        {
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.GetCycleComponentPageCount(-1), Is.EqualTo(1));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.GetCycleComponentPageCount(0), Is.EqualTo(1));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.GetCycleComponentPageCount(500), Is.EqualTo(1));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.GetCycleComponentPageCount(501), Is.EqualTo(2));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.GetCycleComponentPageCount(2048), Is.EqualTo(5));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.GetCycleComponentPageCount(10000), Is.EqualTo(20));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.ClampCycleComponentPage(-1, 20), Is.Zero);
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.ClampCycleComponentPage(20, 20), Is.EqualTo(19));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.GetCycleComponentPageStart(0, 0), Is.Zero);
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.GetCycleComponentPageStart(1, 501), Is.EqualTo(500));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.GetCycleComponentPageStart(99, 2048), Is.EqualTo(2000));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.GetCycleComponentPageStart(99, 10000), Is.EqualTo(9500));
        }

        /// <summary>
        /// 選択asmdefが属するcomponentだけを返し、入力index順に依存せずfull pathをOrdinal順へ固定します。
        /// </summary>
        [Test]
        public void TryGetCycleComponentMemberPaths_ReturnsSelectedDisjointComponentInOrdinalPathOrder()
        {
            var assemblies = new[]
            {
                CreateNode("Z", "Assets/Z.asmdef", "guid-z", false),
                CreateNode("PackageZ", "Packages/com.example/Z.asmdef", "guid-pz", false),
                CreateNode("A", "Assets/A.asmdef", "guid-a", false),
                CreateNode("None", "Assets/None.asmdef", "guid-none", false),
                CreateNode("PackageA", "Packages/com.example/A.asmdef", "guid-pa", false)
            };
            var result = CreateCycleResult(
                assemblies,
                new IReadOnlyList<int>[]
                {
                    new[] { 0, 2 },
                    new[] { 1, 4 }
                });

            var firstSucceeded = AuditEditor.AssemblyDependencyAuditWindow.TryGetCycleComponentMemberPaths(
                result,
                0,
                out var firstPaths,
                out var firstError);
            var secondSucceeded = AuditEditor.AssemblyDependencyAuditWindow.TryGetCycleComponentMemberPaths(
                result,
                1,
                out var secondPaths,
                out var secondError);
            var nonMemberSucceeded = AuditEditor.AssemblyDependencyAuditWindow.TryGetCycleComponentMemberPaths(
                result,
                3,
                out var nonMemberPaths,
                out var nonMemberError);

            Assert.That(firstSucceeded, Is.True, firstError);
            Assert.That(firstPaths, Is.EqualTo(new[] { "Assets/A.asmdef", "Assets/Z.asmdef" }));
            Assert.That(firstPaths, Does.Contain(assemblies[0].AssetPath), "selected member自身もexact 1回含めます。");
            Assert.That(secondSucceeded, Is.True, secondError);
            Assert.That(secondPaths, Is.EqualTo(new[]
            {
                "Packages/com.example/A.asmdef",
                "Packages/com.example/Z.asmdef"
            }));
            Assert.That(nonMemberSucceeded, Is.True, nonMemberError);
            Assert.That(nonMemberPaths, Is.Empty);
            Assert.That(firstError, Is.Empty);
            Assert.That(secondError, Is.Empty);
            Assert.That(nonMemberError, Is.Empty);
            Assert.Throws<NotSupportedException>(() => ((IList<string>)firstPaths).Add("Assets/Mutation.asmdef"));
        }

        /// <summary>
        /// asmdef件数とlogical path長はproduction上限exactを受理し、1件または1文字超過をpartialなしで拒否します。
        /// </summary>
        [Test]
        public void TryGetCycleComponentMemberPaths_AcceptsExactLimitsAndRejectsOneOver()
        {
            var maximumAssemblies = CreateCycleNodes(AuditEditor.AssemblyDependencyAnalyzer.MaximumAssemblyDefinitions);
            var exactAssemblyResult = CreateCycleResult(
                maximumAssemblies,
                new IReadOnlyList<int>[] { new[] { 9999, 0 } });

            var exactAssemblySucceeded = AuditEditor.AssemblyDependencyAuditWindow.TryGetCycleComponentMemberPaths(
                exactAssemblyResult,
                9999,
                out var exactAssemblyPaths,
                out var exactAssemblyError);

            Assert.That(exactAssemblySucceeded, Is.True, exactAssemblyError);
            Assert.That(exactAssemblyPaths, Is.EqualTo(new[]
            {
                maximumAssemblies[0].AssetPath,
                maximumAssemblies[9999].AssetPath
            }));

            var tooManyAssemblies = CreateCycleNodes(AuditEditor.AssemblyDependencyAnalyzer.MaximumAssemblyDefinitions + 1);
            AssertCycleComponentRejected(
                CreateCycleResult(tooManyAssemblies, new IReadOnlyList<int>[] { new[] { 0, 1 } }),
                0);

            var exactPath = CreateLogicalAsmdefPath(AuditEditor.AssemblyDependencyAuditWindow.MaximumDisplayedTextCharacters);
            var exactPathResult = CreateCycleResult(
                new[]
                {
                    CreateNode("Exact", exactPath, "guid-exact", false),
                    CreateNode("Peer", "Assets/Z.asmdef", "guid-peer", false)
                },
                new IReadOnlyList<int>[] { new[] { 0, 1 } });
            var exactPathSucceeded = AuditEditor.AssemblyDependencyAuditWindow.TryGetCycleComponentMemberPaths(
                exactPathResult,
                0,
                out var exactPathMembers,
                out var exactPathError);

            Assert.That(exactPathSucceeded, Is.True, exactPathError);
            Assert.That(exactPathMembers, Does.Contain(exactPath));

            var overlongPath = CreateLogicalAsmdefPath(
                AuditEditor.AssemblyDependencyAuditWindow.MaximumDisplayedTextCharacters + 1);
            AssertCycleComponentRejected(
                CreateCycleResult(
                    new[]
                    {
                        CreateNode("Safe", "Assets/Safe.asmdef", "guid-safe", false),
                        CreateNode("Over", overlongPath, "guid-over", false)
                    },
                    new IReadOnlyList<int>[] { new[] { 0, 1 } }),
                0);
        }

        /// <summary>
        /// 壊れたcomponentを一件でも含むresultは、先に確定したmemberを含めて全て破棄します。
        /// </summary>
        [Test]
        public void TryGetCycleComponentMemberPaths_RejectsInvalidResultAtomically()
        {
            var nodes = CreateCycleNodes(4);
            var validResult = CreateCycleResult(nodes, Array.Empty<IReadOnlyList<int>>());
            AssertCycleComponentRejected(null, 0);
            AssertCycleComponentRejected(validResult, -1);
            AssertCycleComponentRejected(validResult, nodes.Length);
            AssertCycleComponentRejected(
                CreateCycleResult(nodes, new IReadOnlyList<int>[] { null }),
                0);
            AssertCycleComponentRejected(
                CreateCycleResult(nodes, new IReadOnlyList<int>[] { Array.Empty<int>() }),
                0);
            AssertCycleComponentRejected(
                CreateCycleResult(nodes, new IReadOnlyList<int>[] { new[] { 0 } }),
                0);
            AssertCycleComponentRejected(
                CreateCycleResult(nodes, new IReadOnlyList<int>[]
                {
                    new[] { 0, 1 },
                    new[] { 2, 3 },
                    new[] { 0, 2 }
                }),
                0);
            AssertCycleComponentRejected(
                CreateCycleResult(CreateCycleNodes(2), new IReadOnlyList<int>[] { new[] { 0, 1, 0 } }),
                0);
            AssertCycleComponentRejected(
                CreateCycleResult(nodes, new IReadOnlyList<int>[] { new[] { -1, 0 } }),
                0);
            AssertCycleComponentRejected(
                CreateCycleResult(nodes, new IReadOnlyList<int>[] { new[] { 0, nodes.Length } }),
                0);
            AssertCycleComponentRejected(
                CreateCycleResult(nodes, new IReadOnlyList<int>[] { new[] { 0, 0 } }),
                0);
            AssertCycleComponentRejected(
                CreateCycleResult(nodes, new IReadOnlyList<int>[]
                {
                    new[] { 0, 1 },
                    new[] { 1, 2 }
                }),
                0);

            var nodesWithNull = new AuditEditor.AssemblyDependencyNode[]
            {
                nodes[0],
                null
            };
            AssertCycleComponentRejected(
                CreateCycleResult(nodesWithNull, new IReadOnlyList<int>[] { new[] { 0, 1 } }),
                0);

            var duplicatePathNodes = new[]
            {
                CreateNode("A", "Assets/Duplicate.asmdef", "guid-a", false),
                CreateNode("B", "Assets/B.asmdef", "guid-b", false),
                CreateNode("C", "Assets/Duplicate.asmdef", "guid-c", false),
                CreateNode("D", "Assets/D.asmdef", "guid-d", false)
            };
            AssertCycleComponentRejected(
                CreateCycleResult(
                    duplicatePathNodes,
                    new IReadOnlyList<int>[]
                    {
                        new[] { 0, 1 },
                        new[] { 2, 3 }
                    }),
                0);

            AssertCycleComponentRejected(
                CreateCycleResult(
                    nodes,
                    new IReadOnlyList<int>[]
                    {
                        new[] { 0, 1 },
                        new[] { 2, nodes.Length }
                    }),
                0);

            var physicalCanary = "C:/Physical/Secret/Canary.asmdef";
            var unsafePaths = new[]
            {
                string.Empty,
                physicalCanary,
                "ProjectSettings/A.asmdef",
                "Assets\\Backslash.asmdef",
                "Assets/Wrong.asset",
                "Assets/Samples~/Ignored.asmdef",
                "Assets/Control\u0001/A.asmdef",
                "Assets/../Escape.asmdef",
                "Assets/Foo:Bar/A.asmdef",
                "Assets/Foo./A.asmdef",
                "Assets/Foo /A.asmdef",
                "Packages/A.asmdef",
                CreateLogicalAsmdefPath(AuditEditor.AssemblyDependencyAuditWindow.MaximumDisplayedTextCharacters + 1)
            };
            for (var pathIndex = 0; pathIndex < unsafePaths.Length; pathIndex++)
            {
                AssertCycleComponentRejected(
                    CreateCycleResult(
                        new[]
                        {
                            CreateNode("Safe", "Assets/Safe.asmdef", "guid-safe", false),
                            CreateNode("Unsafe", unsafePaths[pathIndex], $"guid-unsafe-{pathIndex}", false)
                        },
                        new IReadOnlyList<int>[] { new[] { 0, 1 } }),
                    0,
                    physicalCanary);
            }
        }

        /// <summary>
        /// member rowはglobal番号とlogical pathだけを一行に保ち、長い値とsurrogate pairを安全に制限します。
        /// </summary>
        [Test]
        public void FormatCycleComponentMemberRow_BoundsLogicalPathsAndFailsClosed()
        {
            var exactPath = CreateLogicalAsmdefPath(
                AuditEditor.AssemblyDependencyAuditWindow.MaximumDeclaredReferenceRowValueCharacters);
            var overflowPath = CreateLogicalAsmdefPath(
                AuditEditor.AssemblyDependencyAuditWindow.MaximumDeclaredReferenceRowValueCharacters + 1);
            var surrogateBoundaryPath = "Assets/" + new string('s', 151) + "\uD83D\uDE00.asmdef";
            var exactRow = AuditEditor.AssemblyDependencyAuditWindow.FormatCycleComponentMemberRow(exactPath, 0);
            var overflowRow = AuditEditor.AssemblyDependencyAuditWindow.FormatCycleComponentMemberRow(overflowPath, 500);
            var surrogateRow = AuditEditor.AssemblyDependencyAuditWindow.FormatCycleComponentMemberRow(
                surrogateBoundaryPath,
                1);
            var maximumPathRow = AuditEditor.AssemblyDependencyAuditWindow.FormatCycleComponentMemberRow(
                CreateLogicalAsmdefPath(AuditEditor.AssemblyDependencyAuditWindow.MaximumDisplayedTextCharacters),
                2);
            const string physicalCanary = "C:/Physical/Secret/Canary.asmdef";

            Assert.That(exactRow, Is.EqualTo($"#1 | {exactPath}"));
            Assert.That(overflowRow, Does.StartWith("#501 | "));
            Assert.That(overflowRow.Substring("#501 | ".Length), Has.Length.EqualTo(
                AuditEditor.AssemblyDependencyAuditWindow.MaximumDeclaredReferenceRowValueCharacters));
            Assert.That(overflowRow, Does.EndWith("…"));
            Assert.That(surrogateRow.Split('\n'), Has.Length.EqualTo(1));
            Assert.That(ContainsUnpairedSurrogate(surrogateRow), Is.False);
            Assert.That(surrogateRow.Any(char.IsControl), Is.False);
            Assert.That(maximumPathRow, Does.Not.Contain("(invalid)"));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.FormatCycleComponentMemberRow(
                    "Packages/com.example/A.asmdef",
                    -1),
                Is.EqualTo("#? | Packages/com.example/A.asmdef"));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.FormatCycleComponentMemberRow(
                    "Assets/Control\n/A.asmdef",
                    0),
                Is.EqualTo("#1 | (invalid)"));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.FormatCycleComponentMemberRow(
                    "Assets\\Backslash.asmdef",
                    0),
                Is.EqualTo("#1 | (invalid)"));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.FormatCycleComponentMemberRow(
                    CreateLogicalAsmdefPath(AuditEditor.AssemblyDependencyAuditWindow.MaximumDisplayedTextCharacters + 1),
                    0),
                Is.EqualTo("#1 | (invalid)"));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.FormatCycleComponentMemberRow(
                    physicalCanary,
                    0),
                Is.EqualTo("#1 | (invalid)"));
            Assert.That(
                AuditEditor.AssemblyDependencyAuditWindow.FormatCycleComponentMemberRow(physicalCanary, 0),
                Does.Not.Contain(physicalCanary));
        }

        /// <summary>
        /// 宣言参照は0、500、501、analyzer上限4096件を500件単位のpageへ正確に分割します。
        /// </summary>
        [Test]
        public void DeclaredReferencePaging_HandlesZeroExactOverflowAndAnalyzerMaximum()
        {
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.GetDeclaredReferencePageCount(0), Is.EqualTo(1));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.GetDeclaredReferencePageCount(500), Is.EqualTo(1));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.GetDeclaredReferencePageCount(501), Is.EqualTo(2));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.GetDeclaredReferencePageCount(4096), Is.EqualTo(9));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.ClampDeclaredReferencePage(-1, 9), Is.Zero);
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.ClampDeclaredReferencePage(9, 9), Is.EqualTo(8));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.GetDeclaredReferencePageStart(0, 0), Is.Zero);
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.GetDeclaredReferencePageStart(0, 500), Is.Zero);
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.GetDeclaredReferencePageStart(1, 501), Is.EqualTo(500));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.GetDeclaredReferencePageStart(99, 4096), Is.EqualTo(4000));

            var reference = new AuditEditor.AssemblyDependencyReference(
                "Missing",
                AuditEditor.AssemblyDependencyReferenceKind.Name,
                -1);
            Assert.That(
                AuditEditor.AssemblyDependencyAuditWindow.FormatDeclaredReferenceRow(
                    reference,
                    Array.Empty<AuditEditor.AssemblyDependencyNode>(),
                    499),
                Does.StartWith("#500 |"));
            Assert.That(
                AuditEditor.AssemblyDependencyAuditWindow.FormatDeclaredReferenceRow(
                    reference,
                    Array.Empty<AuditEditor.AssemblyDependencyNode>(),
                    500),
                Does.StartWith("#501 |"));
            Assert.That(
                AuditEditor.AssemblyDependencyAuditWindow.FormatDeclaredReferenceRow(
                    reference,
                    Array.Empty<AuditEditor.AssemblyDependencyNode>(),
                    4095),
                Does.StartWith("#4096 |"));
        }

        /// <summary>
        /// Name、GUID、未一意解決を宣言順の3行rowへ変換し、同じ宣言も出現回数を保持します。
        /// </summary>
        [Test]
        public void FormatDeclaredReferenceRow_PreservesOrderDuplicatesAndResolutionKinds()
        {
            var assemblies = new[]
            {
                CreateNode("Alpha", "Assets/Alpha.asmdef", "guid-alpha", false),
                CreateNode("Beta", "Packages/com.example/Beta.asmdef", "guid-beta", false)
            };
            var references = new[]
            {
                new AuditEditor.AssemblyDependencyReference(
                    "Beta",
                    AuditEditor.AssemblyDependencyReferenceKind.Name,
                    1),
                new AuditEditor.AssemblyDependencyReference(
                    "GUID:guid-alpha",
                    AuditEditor.AssemblyDependencyReferenceKind.Guid,
                    0),
                new AuditEditor.AssemblyDependencyReference(
                    "Missing",
                    AuditEditor.AssemblyDependencyReferenceKind.Name,
                    -1),
                new AuditEditor.AssemblyDependencyReference(
                    "Beta",
                    AuditEditor.AssemblyDependencyReferenceKind.Name,
                    1)
            };

            var rows = references
                .Select((reference, index) => AuditEditor.AssemblyDependencyAuditWindow.FormatDeclaredReferenceRow(
                    reference,
                    assemblies,
                    index))
                .ToArray();

            Assert.That(rows, Is.EqualTo(new[]
            {
                "#1 | Kind: Name | Status: Resolved\n" +
                    "Declared: Beta\n" +
                    "Target: Beta | Packages/com.example/Beta.asmdef",
                "#2 | Kind: GUID | Status: Resolved\n" +
                    "Declared: GUID:guid-alpha\n" +
                    "Target: Alpha | Assets/Alpha.asmdef",
                "#3 | Kind: Name | Status: Not uniquely resolved\n" +
                    "Declared: Missing\n" +
                    "Target: (not uniquely resolved)",
                "#4 | Kind: Name | Status: Resolved\n" +
                    "Declared: Beta\n" +
                    "Target: Beta | Packages/com.example/Beta.asmdef"
            }));
        }

        /// <summary>
        /// null、未知kind、不正index、null targetを例外や偽の解決表示へ変換せず明示します。
        /// </summary>
        [Test]
        public void FormatDeclaredReferenceRow_InvalidInputsFailClosed()
        {
            var target = CreateNode("Target", "Assets/Target.asmdef", "guid-target", false);
            var unknownKind = new AuditEditor.AssemblyDependencyReference(
                "Mystery",
                (AuditEditor.AssemblyDependencyReferenceKind)int.MaxValue,
                0);
            var negativeIndex = new AuditEditor.AssemblyDependencyReference(
                "Negative",
                AuditEditor.AssemblyDependencyReferenceKind.Name,
                -2);
            var outOfRange = new AuditEditor.AssemblyDependencyReference(
                "OutOfRange",
                AuditEditor.AssemblyDependencyReferenceKind.Guid,
                1);
            var nullTarget = new AuditEditor.AssemblyDependencyReference(
                "NullTarget",
                AuditEditor.AssemblyDependencyReferenceKind.Name,
                0);

            Assert.That(
                AuditEditor.AssemblyDependencyAuditWindow.FormatDeclaredReferenceRow(null, new[] { target }, -1),
                Is.EqualTo("#? | Kind: (invalid) | Status: Invalid reference\n" +
                    "Declared: (invalid)\n" +
                    "Target: (invalid)"));
            Assert.That(
                AuditEditor.AssemblyDependencyAuditWindow.FormatDeclaredReferenceRow(unknownKind, new[] { target }, 4),
                Is.EqualTo("#5 | Kind: (invalid) | Status: Invalid kind\n" +
                    "Declared: Mystery\n" +
                    "Target: (invalid)"));
            Assert.That(
                AuditEditor.AssemblyDependencyAuditWindow.FormatDeclaredReferenceRow(negativeIndex, new[] { target }, 0),
                Is.EqualTo("#1 | Kind: Name | Status: Invalid result index\n" +
                    "Declared: Negative\n" +
                    "Target: (invalid)"));
            Assert.That(
                AuditEditor.AssemblyDependencyAuditWindow.FormatDeclaredReferenceRow(outOfRange, new[] { target }, 1),
                Is.EqualTo("#2 | Kind: GUID | Status: Invalid result index\n" +
                    "Declared: OutOfRange\n" +
                    "Target: (invalid)"));
            Assert.That(
                AuditEditor.AssemblyDependencyAuditWindow.FormatDeclaredReferenceRow(outOfRange, null, 1),
                Does.Contain("Status: Invalid result index"));
            Assert.That(
                AuditEditor.AssemblyDependencyAuditWindow.FormatDeclaredReferenceRow(
                    nullTarget,
                    new AuditEditor.AssemblyDependencyNode[] { null },
                    2),
                Is.EqualTo("#3 | Kind: Name | Status: Invalid target node\n" +
                    "Declared: NullTarget\n" +
                    "Target: (invalid)"));
        }

        /// <summary>
        /// 宣言値と解決先は各160 UTF-16 unit以内へ省略し、surrogate pairを分断しません。
        /// </summary>
        [Test]
        public void FormatDeclaredReferenceRow_BoundsValuesAndPreservesSurrogatePairs()
        {
            var maximum = AuditEditor.AssemblyDependencyAuditWindow.MaximumDeclaredReferenceRowValueCharacters;
            var exactValue = new string('e', maximum);
            var exactRow = AuditEditor.AssemblyDependencyAuditWindow.FormatDeclaredReferenceRow(
                new AuditEditor.AssemblyDependencyReference(
                    exactValue,
                    AuditEditor.AssemblyDependencyReferenceKind.Name,
                    -1),
                Array.Empty<AuditEditor.AssemblyDependencyNode>(),
                0);
            var boundaryValue = new string('v', maximum - 1) + "\uD83D\uDE00tail";
            var longTarget = CreateNode(
                new string('t', maximum - 1) + "\uD83D\uDE00tail",
                "Assets/Target.asmdef",
                "guid-target",
                false);
            var limitedRow = AuditEditor.AssemblyDependencyAuditWindow.FormatDeclaredReferenceRow(
                new AuditEditor.AssemblyDependencyReference(
                    boundaryValue,
                    AuditEditor.AssemblyDependencyReferenceKind.Guid,
                    0),
                new[] { longTarget },
                1);
            var exactLines = exactRow.Split('\n');
            var limitedLines = limitedRow.Split('\n');
            var exactDeclared = exactLines[1].Substring("Declared: ".Length);
            var limitedDeclared = limitedLines[1].Substring("Declared: ".Length);
            var limitedTarget = limitedLines[2].Substring("Target: ".Length);

            Assert.That(exactDeclared, Is.EqualTo(exactValue));
            Assert.That(limitedDeclared, Has.Length.EqualTo(maximum));
            Assert.That(limitedDeclared.EndsWith("…", StringComparison.Ordinal), Is.True);
            Assert.That(limitedTarget, Has.Length.EqualTo(maximum));
            Assert.That(limitedTarget.EndsWith("…", StringComparison.Ordinal), Is.True);
            Assert.That(ContainsUnpairedSurrogate(limitedDeclared), Is.False);
            Assert.That(ContainsUnpairedSurrogate(limitedTarget), Is.False);
        }

        /// <summary>
        /// 宣言参照rowは改行などのcontrol文字をspaceへ置換し、surrogate境界でも常にexactly 3行です。
        /// </summary>
        [Test]
        public void FormatDeclaredReferenceRow_SanitizesControlsAndRemainsExactlyThreeLines()
        {
            var maximum = AuditEditor.AssemblyDependencyAuditWindow.MaximumDeclaredReferenceRowValueCharacters;
            var boundaryValue = new string('b', maximum - 6) + "\r\n\t\u0001\uD83D\uDE00tail";
            var sanitizedBoundary = AuditEditor.AssemblyDependencyAuditWindow.LimitDeclaredReferenceRowValue(
                boundaryValue);
            var target = CreateNode(
                "Target\r\n\t\u0001",
                "Assets/Target.asmdef",
                "guid-target",
                false);
            var row = AuditEditor.AssemblyDependencyAuditWindow.FormatDeclaredReferenceRow(
                new AuditEditor.AssemblyDependencyReference(
                    "Name\r\n\t\u0001Value",
                    AuditEditor.AssemblyDependencyReferenceKind.Name,
                    0),
                new[] { target },
                0);
            var lines = row.Split('\n');

            Assert.That(sanitizedBoundary, Is.EqualTo(new string('b', maximum - 6) + "    …"));
            Assert.That(sanitizedBoundary.Length, Is.LessThanOrEqualTo(maximum));
            Assert.That(sanitizedBoundary.Any(char.IsControl), Is.False);
            Assert.That(ContainsUnpairedSurrogate(sanitizedBoundary), Is.False);
            Assert.That(lines, Has.Length.EqualTo(3));
            Assert.That(lines[1], Is.EqualTo("Declared: Name    Value"));
            Assert.That(lines[2], Is.EqualTo("Target: Target     | Assets/Target.asmdef"));
            Assert.That(lines.All(line => !line.Any(char.IsControl)), Is.True);
        }

        /// <summary>
        /// 表示文字列はexact上限を保持し、max+1では末尾ellipsisを付けてsurrogate pairを分断しません。
        /// </summary>
        [Test]
        public void LimitText_PreservesExactBoundaryAndSafelyEllipsizesOverflow()
        {
            var exact = new string('x', AuditEditor.AssemblyDependencyAuditWindow.MaximumDisplayedTextCharacters);
            var overflow = exact + "y";
            var surrogateBoundary = "ab\uD83D\uDE00c";

            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.LimitText(
                    exact,
                    AuditEditor.AssemblyDependencyAuditWindow.MaximumDisplayedTextCharacters),
                Is.EqualTo(exact));
            var limited = AuditEditor.AssemblyDependencyAuditWindow.LimitText(
                overflow,
                AuditEditor.AssemblyDependencyAuditWindow.MaximumDisplayedTextCharacters);
            var surrogateLimited = AuditEditor.AssemblyDependencyAuditWindow.LimitText(surrogateBoundary, 4);

            Assert.That(limited, Has.Length.EqualTo(AuditEditor.AssemblyDependencyAuditWindow.MaximumDisplayedTextCharacters));
            Assert.That(limited.EndsWith("…", StringComparison.Ordinal), Is.True);
            Assert.That(surrogateLimited.EndsWith("…", StringComparison.Ordinal), Is.True);
            Assert.That(surrogateLimited.Length, Is.LessThanOrEqualTo(4));
            Assert.That(ContainsUnpairedSurrogate(surrogateLimited), Is.False);
        }

        /// <summary>
        /// asmref rowは解決状態とsurrogate-safeな短縮leafのexactly 2行だけを返します。
        /// </summary>
        [Test]
        public void FormatAssemblyReferenceRow_UsesTwoLinesAndSurrogateSafeEllipsis()
        {
            var longLeaf = new string('長', 18) + "\uD83D\uDE00末尾.asmref";
            var resolved = new AuditEditor.AssemblyReferenceTarget(
                "Assets/" + longLeaf,
                "GUID:0123456789abcdef0123456789abcdef",
                AuditEditor.AssemblyReferenceTargetKind.Guid,
                "Assets/Target.asmdef");
            var unresolved = new AuditEditor.AssemblyReferenceTarget(
                "Assets/Short.asmref",
                "Missing",
                AuditEditor.AssemblyReferenceTargetKind.Name,
                string.Empty);

            var resolvedLines = AuditEditor.AssemblyDependencyAuditWindow.FormatAssemblyReferenceRow(resolved).Split('\n');
            var unresolvedText = AuditEditor.AssemblyDependencyAuditWindow.FormatAssemblyReferenceRow(unresolved);

            Assert.That(resolvedLines, Has.Length.EqualTo(2));
            Assert.That(resolvedLines[0], Is.EqualTo("Guid: Resolved"));
            Assert.That(resolvedLines[1].EndsWith("…", StringComparison.Ordinal), Is.True);
            Assert.That(resolvedLines[1].Length, Is.LessThanOrEqualTo(20));
            Assert.That(ContainsUnpairedSurrogate(resolvedLines[1]), Is.False);
            Assert.That(unresolvedText, Is.EqualTo("Name: Unresolved\nShort.asmref"));
            Assert.That(AuditEditor.AssemblyDependencyAuditWindow.FormatAssemblyReferenceRow(null), Is.Empty);
        }

        /// <summary>
        /// asmref専用rowだけは折り返さず、共用assembly rowの折り返し契約を変更しません。
        /// </summary>
        [Test]
        public void AssemblyReferenceRowStyle_DisablesWordWrapOnlyForAssemblyReferences()
        {
            var window = ScriptableObject.CreateInstance<AuditEditor.AssemblyDependencyAuditWindow>();
            try
            {
                SetField(window, "_rowStyle", new GUIStyle { wordWrap = true });
                SetField(window, "_wrappedMiniLabelStyle", new GUIStyle());
                InvokeInstance(window, "EnsureStyles");
                var sharedRowStyle = GetField<GUIStyle>(window, "_rowStyle");
                var assemblyReferenceRowStyle = GetField<GUIStyle>(window, "_assemblyReferenceRowStyle");

                Assert.That(sharedRowStyle.wordWrap, Is.True);
                Assert.That(assemblyReferenceRowStyle.wordWrap, Is.False);
                Assert.That(ReferenceEquals(sharedRowStyle, assemblyReferenceRowStyle), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        /// <summary>
        /// tooltipは長いpathを制限し、rowに載せないReferenceとTargetは選択後詳細へ委ねます。
        /// </summary>
        [Test]
        public void FormatAssemblyReferenceTooltip_UsesBoundedPathOnly()
        {
            var longPath = "Assets/" + new string('p', 1200) + ".asmref";
            var target = new AuditEditor.AssemblyReferenceTarget(
                longPath,
                new string('r', 5000),
                AuditEditor.AssemblyReferenceTargetKind.Name,
                new string('t', 5000));

            var tooltip = AuditEditor.AssemblyDependencyAuditWindow.FormatAssemblyReferenceTooltip(target);

            Assert.That(tooltip.StartsWith("Name: Resolved\nPath: ", StringComparison.Ordinal), Is.True);
            Assert.That(tooltip.IndexOf("…\nSelect to view Reference and Target.", StringComparison.Ordinal) >= 0, Is.True);
            Assert.That(tooltip.IndexOf(target.RawReference, StringComparison.Ordinal), Is.EqualTo(-1));
            Assert.That(tooltip.IndexOf(target.ResolvedTargetAssetPath, StringComparison.Ordinal), Is.EqualTo(-1));
            Assert.That(ContainsUnpairedSurrogate(tooltip), Is.False);
        }

        /// <summary>
        /// asmdefが0件でも選択asmrefとそのsource findingをfilter再構築後まで保持します。
        /// </summary>
        [Test]
        public void AssemblyReferenceSelection_PersistsWithoutAssemblyDefinitions()
        {
            const string assetPath = "Assets/Only.asmref";
            var target = new AuditEditor.AssemblyReferenceTarget(
                assetPath,
                "Missing",
                AuditEditor.AssemblyReferenceTargetKind.Name,
                string.Empty);
            var issue = CreateIssue(
                AuditEditor.AssemblyDependencyIssueKind.UnresolvedAssemblyReference,
                assetPath,
                string.Empty,
                "Missing");
            var result = CreateResult(
                Array.Empty<AuditEditor.AssemblyDependencyNode>(),
                new[] { issue },
                new[] { target });
            var window = ScriptableObject.CreateInstance<AuditEditor.AssemblyDependencyAuditWindow>();
            try
            {
                InitializeWindow(window, result);
                InvokeInstance(window, "SelectAssemblyReference", 0);
                InvokeInstance(window, "RebuildVisibleAssemblyIndices", true);

                Assert.That(GetField<int>(window, "_selectedAssemblyIndex"), Is.EqualTo(-1));
                Assert.That(GetField<int>(window, "_selectedAssemblyReferenceIndex"), Is.Zero);
                Assert.That(GetField<int>(window, "_selectedIssueIndex"), Is.Zero);
                Assert.That(ReferenceEquals(InvokeInstance(window, "GetSelectedAssemblyReference"), target), Is.True);
                Assert.That(ReferenceEquals(InvokeInstance(window, "GetSelectedIssue"), issue), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        /// <summary>
        /// 同folder owner問題はasmdef側とasmref側のどちらを選んでも詳細へ到達できます。
        /// </summary>
        [Test]
        public void MultipleAssemblyOwnersIssue_IsReachableFromBothOwnerPaths()
        {
            const string assemblyPath = "Assets/Feature/A.asmdef";
            const string assemblyReferencePath = "Assets/Feature/B.asmref";
            var node = CreateNode("A", assemblyPath, "guid-a", false);
            var target = new AuditEditor.AssemblyReferenceTarget(
                assemblyReferencePath,
                "A",
                AuditEditor.AssemblyReferenceTargetKind.Name,
                assemblyPath);
            var issue = CreateIssue(
                AuditEditor.AssemblyDependencyIssueKind.MultipleAssemblyOwnersInFolder,
                assemblyPath,
                assemblyReferencePath,
                "Assets/Feature");
            var result = CreateResult(new[] { node }, new[] { issue }, new[] { target });
            var window = ScriptableObject.CreateInstance<AuditEditor.AssemblyDependencyAuditWindow>();
            try
            {
                InitializeWindow(window, result);

                InvokeInstance(window, "SelectAssembly", 0);
                Assert.That(ReferenceEquals(InvokeInstance(window, "GetSelectedIssue"), issue), Is.True);

                InvokeInstance(window, "SelectAssemblyReference", 0);
                Assert.That(ReferenceEquals(InvokeInstance(window, "GetSelectedIssue"), issue), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        /// <summary>
        /// 選択asmrefがfilter外になったら旧asmref findingを破棄し、visible asmdefのfindingまたはnoneへ同期します。
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void FilterTransition_ReplacesHiddenAssemblyReferenceIssueSelection(bool assemblyHasIssue)
        {
            const string assemblyPath = "Assets/A.asmdef";
            const string assemblyReferencePath = "Assets/B.asmref";
            var node = CreateNode("A", assemblyPath, "guid-a", false);
            var target = new AuditEditor.AssemblyReferenceTarget(
                assemblyReferencePath,
                "Missing",
                AuditEditor.AssemblyReferenceTargetKind.Name,
                string.Empty);
            var issues = new List<AuditEditor.AssemblyDependencyIssue>
            {
                CreateIssue(
                    AuditEditor.AssemblyDependencyIssueKind.UnresolvedAssemblyReference,
                    assemblyReferencePath,
                    string.Empty,
                    "Missing")
            };
            if (assemblyHasIssue)
            {
                issues.Add(CreateIssue(
                    AuditEditor.AssemblyDependencyIssueKind.UnresolvedReference,
                    assemblyPath,
                    string.Empty,
                    "MissingAssembly"));
            }

            var result = CreateResult(new[] { node }, issues, new[] { target });
            var window = ScriptableObject.CreateInstance<AuditEditor.AssemblyDependencyAuditWindow>();
            try
            {
                InitializeWindow(window, result);
                InvokeInstance(window, "SelectAssemblyReference", 0);
                Assert.That(GetField<int>(window, "_selectedIssueIndex"), Is.Zero);
                SetField(window, "_searchText", assemblyPath);

                InvokeInstance(window, "RebuildVisibleAssemblyIndices", true);

                Assert.That(GetField<int>(window, "_selectedAssemblyReferenceIndex"), Is.EqualTo(-1));
                Assert.That(GetField<int>(window, "_selectedAssemblyIndex"), Is.Zero);
                Assert.That(GetField<int>(window, "_selectedIssueIndex"), Is.EqualTo(assemblyHasIssue ? 1 : -1));
                Assert.That(ReferenceEquals(InvokeInstance(window, "GetSelectedIssue"), issues[0]), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        /// <summary>
        /// 501件のpage2へ移動でき、filter変更ではpage0へ戻して末尾選択を到達可能なまま保持します。
        /// </summary>
        [Test]
        public void AssemblyReferencePage_ClampsAndResetsWhenFiltersChange()
        {
            var targets = CreateAssemblyReferences(501);
            var result = CreateResult(
                Array.Empty<AuditEditor.AssemblyDependencyNode>(),
                Array.Empty<AuditEditor.AssemblyDependencyIssue>(),
                targets);
            var window = ScriptableObject.CreateInstance<AuditEditor.AssemblyDependencyAuditWindow>();
            try
            {
                InitializeWindow(window, result);
                SetField(window, "_assemblyReferencePage", 99);
                InvokeInstance(window, "RebuildVisibleAssemblyIndices", false);
                Assert.That(GetField<int>(window, "_assemblyReferencePage"), Is.EqualTo(1));
                InvokeInstance(window, "SelectAssemblyReference", 500);
                SetField(window, "_searchText", targets[500].AssetPath);

                InvokeInstance(window, "RebuildVisibleAssemblyIndices", true);

                Assert.That(GetField<int>(window, "_assemblyReferencePage"), Is.Zero);
                Assert.That(GetField<int>(window, "_selectedAssemblyReferenceIndex"), Is.EqualTo(500));
                Assert.That(GetField<List<int>>(window, "_visibleAssemblyReferenceIndices"), Is.EqualTo(new[] { 500 }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        /// <summary>
        /// asmdef・asmref・非member・invalid resultの切替とClearはcycle sectionのstale stateを残しません。
        /// </summary>
        [Test]
        public void CycleComponentSelectionTransitions_RebuildHideAndClearStaleState()
        {
            var nodes = new[]
            {
                CreateNode("A", "Assets/A.asmdef", "guid-a", false),
                CreateNode("B", "Assets/B.asmdef", "guid-b", false),
                CreateNode("C", "Assets/C.asmdef", "guid-c", false),
                CreateNode("None", "Assets/None.asmdef", "guid-none", false)
            };
            var target = new AuditEditor.AssemblyReferenceTarget(
                "Assets/Target.asmref",
                "A",
                AuditEditor.AssemblyReferenceTargetKind.Name,
                nodes[0].AssetPath);
            var result = CreateResult(
                nodes,
                Array.Empty<AuditEditor.AssemblyDependencyIssue>(),
                new[] { target },
                new IReadOnlyList<int>[] { new[] { 2, 0, 1 } });
            var window = ScriptableObject.CreateInstance<AuditEditor.AssemblyDependencyAuditWindow>();
            try
            {
                InitializeWindow(window, result);
                InvokeInstance(window, "SelectAssembly", 0);

                Assert.That(GetField<List<string>>(window, "_selectedCycleComponentMemberPaths"), Is.EqualTo(new[]
                {
                    "Assets/A.asmdef",
                    "Assets/B.asmdef",
                    "Assets/C.asmdef"
                }));
                Assert.That(GetField<string>(window, "_cycleComponentErrorMessage"), Is.Empty);

                SetField(window, "_cycleComponentPage", 9);
                SetField(window, "_declaredReferencePage", 8);
                SetField(window, "_cycleComponentErrorMessage", "stale error");
                SetField(window, "_detailsScrollPosition", new Vector2(12f, 34f));
                InvokeInstance(window, "SelectAssembly", 1);

                Assert.That(GetField<int>(window, "_cycleComponentPage"), Is.Zero);
                Assert.That(GetField<int>(window, "_declaredReferencePage"), Is.Zero);
                Assert.That(GetField<string>(window, "_cycleComponentErrorMessage"), Is.Empty);
                Assert.That(GetField<List<string>>(window, "_selectedCycleComponentMemberPaths"), Has.Count.EqualTo(3));
                Assert.That(GetField<Vector2>(window, "_detailsScrollPosition"), Is.EqualTo(Vector2.zero));

                SetField(window, "_cycleComponentPage", 7);
                SetField(window, "_cycleComponentErrorMessage", "stale error");
                SetField(window, "_detailsScrollPosition", new Vector2(56f, 78f));
                InvokeInstance(window, "SelectAssemblyReference", 0);

                Assert.That(GetField<int>(window, "_selectedAssemblyIndex"), Is.EqualTo(1));
                Assert.That(GetField<int>(window, "_selectedAssemblyReferenceIndex"), Is.Zero);
                Assert.That(GetField<int>(window, "_cycleComponentPage"), Is.Zero);
                Assert.That(GetField<List<string>>(window, "_selectedCycleComponentMemberPaths"), Is.Empty);
                Assert.That(GetField<string>(window, "_cycleComponentErrorMessage"), Is.Empty);
                Assert.That(GetField<Vector2>(window, "_detailsScrollPosition"), Is.EqualTo(Vector2.zero));

                SetField(window, "_cycleComponentPage", 5);
                SetField(window, "_detailsScrollPosition", new Vector2(80f, 90f));
                InvokeInstance(window, "SelectAssemblyReference", -1);

                Assert.That(GetField<int>(window, "_selectedAssemblyIndex"), Is.EqualTo(1));
                Assert.That(GetField<int>(window, "_selectedAssemblyReferenceIndex"), Is.EqualTo(-1));
                Assert.That(GetField<int>(window, "_cycleComponentPage"), Is.Zero);
                Assert.That(GetField<List<string>>(window, "_selectedCycleComponentMemberPaths"), Is.EqualTo(new[]
                {
                    "Assets/A.asmdef",
                    "Assets/B.asmdef",
                    "Assets/C.asmdef"
                }));
                Assert.That(GetField<string>(window, "_cycleComponentErrorMessage"), Is.Empty);
                Assert.That(GetField<Vector2>(window, "_detailsScrollPosition"), Is.EqualTo(Vector2.zero));

                InvokeInstance(window, "SelectAssemblyReference", 0);
                Assert.That(GetField<List<string>>(window, "_selectedCycleComponentMemberPaths"), Is.Empty);
                SetField(window, "_cycleComponentPage", 4);
                SetField(window, "_detailsScrollPosition", new Vector2(100f, 110f));
                InvokeInstance(window, "SelectAssemblyReference", result.AssemblyReferences.Count);

                Assert.That(GetField<int>(window, "_selectedAssemblyIndex"), Is.EqualTo(1));
                Assert.That(GetField<int>(window, "_selectedAssemblyReferenceIndex"), Is.EqualTo(-1));
                Assert.That(GetField<int>(window, "_cycleComponentPage"), Is.Zero);
                Assert.That(GetField<List<string>>(window, "_selectedCycleComponentMemberPaths"), Is.EqualTo(new[]
                {
                    "Assets/A.asmdef",
                    "Assets/B.asmdef",
                    "Assets/C.asmdef"
                }));
                Assert.That(GetField<string>(window, "_cycleComponentErrorMessage"), Is.Empty);
                Assert.That(GetField<Vector2>(window, "_detailsScrollPosition"), Is.EqualTo(Vector2.zero));

                InvokeInstance(window, "SelectAssembly", 3);
                Assert.That(GetField<List<string>>(window, "_selectedCycleComponentMemberPaths"), Is.Empty);
                Assert.That(GetField<string>(window, "_cycleComponentErrorMessage"), Is.Empty);

                var invalidResult = CreateResult(
                    new[] { nodes[0], nodes[1] },
                    Array.Empty<AuditEditor.AssemblyDependencyIssue>(),
                    Array.Empty<AuditEditor.AssemblyReferenceTarget>(),
                    new IReadOnlyList<int>[] { new[] { 0, 2 } });
                SetField(window, "_result", invalidResult);
                InvokeInstance(window, "RebuildResultCaches");
                InvokeInstance(window, "SelectAssembly", 0);

                Assert.That(GetField<List<string>>(window, "_selectedCycleComponentMemberPaths"), Is.Empty);
                Assert.That(
                    GetField<string>(window, "_cycleComponentErrorMessage"),
                    Is.EqualTo("Cycle component result が不正なためmemberを表示できません。"));

                SetField(window, "_cycleComponentPage", 6);
                SetField(window, "_declaredReferencePage", 5);
                SetField(window, "_detailsScrollPosition", new Vector2(90f, 123f));
                InvokeInstance(window, "ClearAuditResult");

                Assert.That(GetField<AuditEditor.AssemblyDependencyAuditResult>(window, "_result"), Is.Null);
                Assert.That(GetField<int>(window, "_cycleComponentPage"), Is.Zero);
                Assert.That(GetField<int>(window, "_declaredReferencePage"), Is.Zero);
                Assert.That(GetField<List<string>>(window, "_selectedCycleComponentMemberPaths"), Is.Empty);
                Assert.That(GetField<string>(window, "_cycleComponentErrorMessage"), Is.Empty);
                Assert.That(GetField<Vector2>(window, "_detailsScrollPosition"), Is.EqualTo(Vector2.zero));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        /// <summary>
        /// filterが中央列を一件へ絞ってselectionを置き換えても、SCC member全件をpage 0から再構築します。
        /// </summary>
        [Test]
        public void CycleComponentFilterReplacement_PreservesWholeComponentAndResetsDetailState()
        {
            var nodes = CreateCycleNodes(501);
            var component = Enumerable.Range(0, nodes.Length).Reverse().ToArray();
            var result = CreateResult(
                nodes,
                Array.Empty<AuditEditor.AssemblyDependencyIssue>(),
                Array.Empty<AuditEditor.AssemblyReferenceTarget>(),
                new IReadOnlyList<int>[] { component });
            var window = ScriptableObject.CreateInstance<AuditEditor.AssemblyDependencyAuditWindow>();
            try
            {
                InitializeWindow(window, result);
                InvokeInstance(window, "SelectAssembly", 500);
                SetField(window, "_cycleComponentPage", 1);
                SetField(window, "_declaredReferencePage", 8);
                SetField(window, "_detailsScrollPosition", new Vector2(20f, 40f));
                SetField(window, "_searchText", nodes[0].AssetPath);

                InvokeInstance(window, "RebuildVisibleAssemblyIndices", true);

                Assert.That(GetField<List<int>>(window, "_visibleAssemblyIndices"), Is.EqualTo(new[] { 0 }));
                Assert.That(GetField<int>(window, "_selectedAssemblyIndex"), Is.Zero);
                Assert.That(GetField<int>(window, "_cycleComponentPage"), Is.Zero);
                Assert.That(GetField<int>(window, "_declaredReferencePage"), Is.Zero);
                Assert.That(GetField<Vector2>(window, "_detailsScrollPosition"), Is.EqualTo(Vector2.zero));
                Assert.That(GetField<string>(window, "_cycleComponentErrorMessage"), Is.Empty);
                var memberPaths = GetField<List<string>>(window, "_selectedCycleComponentMemberPaths");
                Assert.That(memberPaths, Has.Count.EqualTo(501));
                Assert.That(memberPaths[0], Is.EqualTo(nodes[0].AssetPath));
                Assert.That(memberPaths[500], Is.EqualTo(nodes[500].AssetPath));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        /// <summary>
        /// real Service/AnalyzerのRefresh成功はcycle cacheを再構築し、typed failureとthrowはstale stateを全破棄します。
        /// </summary>
        [Test]
        public void RefreshAudit_RebuildsCycleCacheAndFailuresClearStaleState()
        {
            var adapter = new FakeAssemblyDependencySourceAdapter
            {
                Sources = new[]
                {
                    AssemblyDependencyTestData.CreateSource(
                        "Assets/Gate/A/A.asmdef",
                        "A",
                        "guid-a",
                        new[] { "B" }),
                    AssemblyDependencyTestData.CreateSource(
                        "Assets/Gate/B/B.asmdef",
                        "B",
                        "guid-b",
                        new[] { "C" }),
                    AssemblyDependencyTestData.CreateSource(
                        "Assets/Gate/C/C.asmdef",
                        "C",
                        "guid-c",
                        new[] { "A" })
                }
            };
            var window = ScriptableObject.CreateInstance<AuditEditor.AssemblyDependencyAuditWindow>();
            try
            {
                SetField(window, "_service", new AuditEditor.AssemblyDependencyAuditService(adapter));
                SetField(window, "_cycleComponentPage", 7);
                GetField<List<string>>(window, "_selectedCycleComponentMemberPaths").Add("Assets/Stale.asmdef");
                SetField(window, "_cycleComponentErrorMessage", "stale error");
                SetField(window, "_detailsScrollPosition", new Vector2(12f, 34f));

                InvokeInstance(window, "RefreshAudit");

                Assert.That(GetField<AuditEditor.AssemblyDependencyAuditResult>(window, "_result"), Is.Not.Null);
                Assert.That(GetField<List<string>>(window, "_selectedCycleComponentMemberPaths"), Is.EqualTo(new[]
                {
                    "Assets/Gate/A/A.asmdef",
                    "Assets/Gate/B/B.asmdef",
                    "Assets/Gate/C/C.asmdef"
                }));
                Assert.That(GetField<int>(window, "_cycleComponentPage"), Is.Zero);
                Assert.That(GetField<string>(window, "_cycleComponentErrorMessage"), Is.Empty);
                Assert.That(GetField<Vector2>(window, "_detailsScrollPosition"), Is.EqualTo(Vector2.zero));

                SetField(window, "_cycleComponentPage", 6);
                SetField(window, "_cycleComponentErrorMessage", "stale error");
                adapter.ReadSucceeds = false;
                adapter.ReadAuditError = AuditEditor.AssemblyDependencyAuditError.SourceUnavailable;
                adapter.ReadError = "fixture typed failure";

                InvokeInstance(window, "RefreshAudit");

                AssertClearedCycleWindowState(window);
                Assert.That(GetField<string>(window, "_auditErrorMessage"), Does.Contain("fixture typed failure"));

                adapter.ReadSucceeds = true;
                adapter.ReadAuditError = AuditEditor.AssemblyDependencyAuditError.None;
                adapter.ReadError = string.Empty;
                SetField(window, "_service", new AuditEditor.AssemblyDependencyAuditService(adapter));
                InvokeInstance(window, "RefreshAudit");
                Assert.That(GetField<List<string>>(window, "_selectedCycleComponentMemberPaths"), Has.Count.EqualTo(3));
                SetField(window, "_cycleComponentPage", 5);
                SetField(window, "_cycleComponentErrorMessage", "stale error");
                SetField(window, "_service", new AuditEditor.AssemblyDependencyAuditService(
                    new ThrowingAssemblyDependencySourceAdapter()));

                InvokeInstance(window, "RefreshAudit");

                AssertClearedCycleWindowState(window);
                Assert.That(GetField<string>(window, "_auditErrorMessage"), Does.Contain("fixture throw"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        /// <summary>
        /// asmdef、asmrefの切替とClearは宣言参照pageとDetails scrollを初期化し、旧選択を残しません。
        /// </summary>
        [Test]
        public void DeclaredReferenceSelectionTransitions_ResetPageScrollAndStaleTargets()
        {
            var references = new[]
            {
                new AuditEditor.AssemblyDependencyReference(
                    "Missing",
                    AuditEditor.AssemblyDependencyReferenceKind.Name,
                    -1)
            };
            var node = CreateNode("Consumer", "Assets/Consumer.asmdef", "guid-consumer", false, references);
            var target = new AuditEditor.AssemblyReferenceTarget(
                "Assets/Consumer.asmref",
                "Consumer",
                AuditEditor.AssemblyReferenceTargetKind.Name,
                node.AssetPath);
            var result = CreateResult(
                new[] { node },
                Array.Empty<AuditEditor.AssemblyDependencyIssue>(),
                new[] { target });
            var window = ScriptableObject.CreateInstance<AuditEditor.AssemblyDependencyAuditWindow>();
            try
            {
                InitializeWindow(window, result);
                SetField(window, "_declaredReferencePage", 7);
                SetField(window, "_cycleComponentPage", 6);
                GetField<List<string>>(window, "_selectedCycleComponentMemberPaths").Add("Assets/Stale.asmdef");
                SetField(window, "_cycleComponentErrorMessage", "stale error");
                SetField(window, "_detailsScrollPosition", new Vector2(12f, 34f));

                InvokeInstance(window, "SelectAssembly", 0);

                Assert.That(GetField<int>(window, "_selectedAssemblyIndex"), Is.Zero);
                Assert.That(GetField<int>(window, "_selectedAssemblyReferenceIndex"), Is.EqualTo(-1));
                Assert.That(InvokeInstance(window, "GetSelectedAssemblyReference"), Is.Null);
                Assert.That(GetField<int>(window, "_declaredReferencePage"), Is.Zero);
                Assert.That(GetField<int>(window, "_cycleComponentPage"), Is.Zero);
                Assert.That(GetField<List<string>>(window, "_selectedCycleComponentMemberPaths"), Is.Empty);
                Assert.That(GetField<string>(window, "_cycleComponentErrorMessage"), Is.Empty);
                Assert.That(GetField<Vector2>(window, "_detailsScrollPosition"), Is.EqualTo(Vector2.zero));

                SetField(window, "_declaredReferencePage", 8);
                SetField(window, "_cycleComponentPage", 7);
                GetField<List<string>>(window, "_selectedCycleComponentMemberPaths").Add("Assets/Stale.asmdef");
                SetField(window, "_cycleComponentErrorMessage", "stale error");
                SetField(window, "_detailsScrollPosition", new Vector2(56f, 78f));
                InvokeInstance(window, "SelectAssemblyReference", 0);

                Assert.That(GetField<int>(window, "_selectedAssemblyReferenceIndex"), Is.Zero);
                Assert.That(ReferenceEquals(InvokeInstance(window, "GetSelectedAssemblyReference"), target), Is.True);
                Assert.That(GetField<int>(window, "_declaredReferencePage"), Is.Zero);
                Assert.That(GetField<int>(window, "_cycleComponentPage"), Is.Zero);
                Assert.That(GetField<List<string>>(window, "_selectedCycleComponentMemberPaths"), Is.Empty);
                Assert.That(GetField<string>(window, "_cycleComponentErrorMessage"), Is.Empty);
                Assert.That(GetField<Vector2>(window, "_detailsScrollPosition"), Is.EqualTo(Vector2.zero));

                SetField(window, "_declaredReferencePage", 8);
                SetField(window, "_cycleComponentPage", 7);
                GetField<List<string>>(window, "_selectedCycleComponentMemberPaths").Add("Assets/Stale.asmdef");
                SetField(window, "_cycleComponentErrorMessage", "stale error");
                SetField(window, "_detailsScrollPosition", new Vector2(90f, 123f));
                InvokeInstance(window, "SelectAssembly", 0);

                Assert.That(GetField<int>(window, "_selectedAssemblyReferenceIndex"), Is.EqualTo(-1));
                Assert.That(GetField<int>(window, "_declaredReferencePage"), Is.Zero);
                Assert.That(GetField<int>(window, "_cycleComponentPage"), Is.Zero);
                Assert.That(GetField<List<string>>(window, "_selectedCycleComponentMemberPaths"), Is.Empty);
                Assert.That(GetField<string>(window, "_cycleComponentErrorMessage"), Is.Empty);
                Assert.That(GetField<Vector2>(window, "_detailsScrollPosition"), Is.EqualTo(Vector2.zero));

                SetField(window, "_declaredReferencePage", 8);
                SetField(window, "_cycleComponentPage", 7);
                GetField<List<string>>(window, "_selectedCycleComponentMemberPaths").Add("Assets/Stale.asmdef");
                SetField(window, "_cycleComponentErrorMessage", "stale error");
                SetField(window, "_detailsScrollPosition", new Vector2(145f, 167f));
                InvokeInstance(window, "ClearAuditResult");

                Assert.That(GetField<AuditEditor.AssemblyDependencyAuditResult>(window, "_result"), Is.Null);
                Assert.That(GetField<int>(window, "_selectedAssemblyIndex"), Is.EqualTo(-1));
                Assert.That(GetField<int>(window, "_selectedAssemblyReferenceIndex"), Is.EqualTo(-1));
                Assert.That(GetField<int>(window, "_declaredReferencePage"), Is.Zero);
                Assert.That(GetField<int>(window, "_cycleComponentPage"), Is.Zero);
                Assert.That(GetField<List<string>>(window, "_selectedCycleComponentMemberPaths"), Is.Empty);
                Assert.That(GetField<string>(window, "_cycleComponentErrorMessage"), Is.Empty);
                Assert.That(GetField<Vector2>(window, "_detailsScrollPosition"), Is.EqualTo(Vector2.zero));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        /// <summary>
        /// 宣言参照rowを追加しても既存Assembly Copyの6行と全文値を変更しません。
        /// </summary>
        [Test]
        public void CopySelectedAssembly_WithDeclaredReferences_PreservesLegacyClipboardExactly()
        {
            var rawReference = new string('r',
                AuditEditor.AssemblyDependencyAuditWindow.MaximumDeclaredReferenceRowValueCharacters + 1);
            var node = CreateNode(
                "Consumer",
                "Assets/Consumer.asmdef",
                "guid-consumer",
                false,
                new[]
                {
                    new AuditEditor.AssemblyDependencyReference(
                        rawReference,
                        AuditEditor.AssemblyDependencyReferenceKind.Name,
                        -1)
                });
            var cyclePeer = CreateNode(
                "CyclePeer",
                "Assets/CyclePeer.asmdef",
                "guid-cycle-peer",
                false);
            var result = CreateResult(
                new[] { node, cyclePeer },
                Array.Empty<AuditEditor.AssemblyDependencyIssue>(),
                Array.Empty<AuditEditor.AssemblyReferenceTarget>(),
                new IReadOnlyList<int>[] { new[] { 1, 0 } });
            var expected = string.Join(Environment.NewLine, new[]
            {
                "Name: Consumer",
                "Path: Assets/Consumer.asmdef",
                "GUID: guid-consumer",
                "Platform: Player Capable",
                "Referenced By: 0",
                "Depends On: 0"
            });
            var previousClipboard = UnityEditor.EditorGUIUtility.systemCopyBuffer;
            var window = ScriptableObject.CreateInstance<AuditEditor.AssemblyDependencyAuditWindow>();
            try
            {
                InitializeWindow(window, result);
                InvokeInstance(window, "SelectAssembly", 0);

                InvokeInstance(window, "CopySelectedAssembly");

                Assert.That(UnityEditor.EditorGUIUtility.systemCopyBuffer, Is.EqualTo(expected));
                Assert.That(UnityEditor.EditorGUIUtility.systemCopyBuffer, Does.Not.Contain(rawReference));
                Assert.That(UnityEditor.EditorGUIUtility.systemCopyBuffer, Does.Not.Contain("Declared Refs"));
                Assert.That(
                    GetField<string>(window, "_interactionMessage"),
                    Is.EqualTo("Assembly details を clipboard へ copy しました。"));
            }
            finally
            {
                UnityEditor.EditorGUIUtility.systemCopyBuffer = previousClipboard;
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        /// <summary>
        /// 画面表示上限を超えるasmref・findingでもCopyはmodel全文を改変せずclipboardへ返します。
        /// </summary>
        [Test]
        public void CopyActions_PreserveFullAssemblyReferenceAndIssueValues()
        {
            var longReference = new string('r', AuditEditor.AssemblyDependencyAuditWindow.MaximumDisplayedTextCharacters + 1);
            var longTarget = new string('t', AuditEditor.AssemblyDependencyAuditWindow.MaximumDisplayedTextCharacters + 1);
            var longMessage = new string('m', AuditEditor.AssemblyDependencyAuditWindow.MaximumDisplayedTextCharacters + 1);
            const string assetPath = "Assets/Long.asmref";
            var target = new AuditEditor.AssemblyReferenceTarget(
                assetPath,
                longReference,
                AuditEditor.AssemblyReferenceTargetKind.Name,
                longTarget);
            var issue = new AuditEditor.AssemblyDependencyIssue(
                AuditEditor.AssemblyDependencyIssueKind.UnresolvedAssemblyReference,
                assetPath,
                string.Empty,
                longReference,
                longMessage);
            var result = CreateResult(
                Array.Empty<AuditEditor.AssemblyDependencyNode>(),
                new[] { issue },
                new[] { target });
            var expectedTargetCopy = string.Join(Environment.NewLine, new[]
            {
                $"Path: {assetPath}",
                $"Reference: {longReference}",
                "Kind: Name",
                $"Target: {longTarget}"
            });
            var expectedIssueCopy = string.Join(Environment.NewLine, new[]
            {
                "Kind: UnresolvedAssemblyReference",
                $"Path: {assetPath}",
                "Related: ",
                $"Reference: {longReference}",
                $"Message: {longMessage}"
            });
            var previousClipboard = UnityEditor.EditorGUIUtility.systemCopyBuffer;
            var window = ScriptableObject.CreateInstance<AuditEditor.AssemblyDependencyAuditWindow>();
            try
            {
                InitializeWindow(window, result);
                InvokeInstance(window, "SelectAssemblyReference", 0);

                InvokeInstance(window, "CopySelectedAssemblyReference");
                Assert.That(UnityEditor.EditorGUIUtility.systemCopyBuffer, Is.EqualTo(expectedTargetCopy));
                InvokeInstance(window, "CopySelectedIssue");
                Assert.That(UnityEditor.EditorGUIUtility.systemCopyBuffer, Is.EqualTo(expectedIssueCopy));
            }
            finally
            {
                UnityEditor.EditorGUIUtility.systemCopyBuffer = previousClipboard;
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        /// <summary>asmref filterを既定の引数名で呼び出します。</summary>
        private static bool MatchesAssemblyReference(
            AuditEditor.AssemblyReferenceTarget target,
            string searchText,
            AuditEditor.AssemblyDependencyAuditWindow.ScopeFilter scope,
            AuditEditor.AssemblyDependencyAuditWindow.IssueFilter issues,
            bool hasIssue)
        {
            return AuditEditor.AssemblyDependencyAuditWindow.MatchesAssemblyReferenceFilters(
                target,
                searchText,
                scope,
                issues,
                hasIssue);
        }

        /// <summary>全scopeを返します。</summary>
        private static AuditEditor.AssemblyDependencyAuditWindow.ScopeFilter ScopeAll()
        {
            return AuditEditor.AssemblyDependencyAuditWindow.ScopeFilter.All;
        }

        /// <summary>全findingを返します。</summary>
        private static AuditEditor.AssemblyDependencyAuditWindow.IssueFilter IssueAll()
        {
            return AuditEditor.AssemblyDependencyAuditWindow.IssueFilter.All;
        }

        /// <summary>Window fixture用のfindingを作ります。</summary>
        private static AuditEditor.AssemblyDependencyIssue CreateIssue(
            AuditEditor.AssemblyDependencyIssueKind kind,
            string assetPath,
            string relatedAssetPath,
            string reference)
        {
            return new AuditEditor.AssemblyDependencyIssue(
                kind,
                assetPath,
                relatedAssetPath,
                reference,
                "fixture issue");
        }

        /// <summary>指定一覧を持つ読み取り専用Window結果を作ります。</summary>
        private static AuditEditor.AssemblyDependencyAuditResult CreateResult(
            IReadOnlyList<AuditEditor.AssemblyDependencyNode> assemblies,
            IReadOnlyList<AuditEditor.AssemblyDependencyIssue> issues,
            IReadOnlyList<AuditEditor.AssemblyReferenceTarget> assemblyReferences,
            IReadOnlyList<IReadOnlyList<int>> cycles = null)
        {
            var graph = new IReadOnlyList<int>[assemblies.Count];
            for (var index = 0; index < graph.Length; index++)
            {
                graph[index] = Array.Empty<int>();
            }

            return new AuditEditor.AssemblyDependencyAuditResult(
                assemblies,
                issues,
                graph,
                graph,
                cycles ?? Array.Empty<IReadOnlyList<int>>(),
                assemblyReferences);
        }

        /// <summary>cycle helperだけを検証する軽量なresultを作ります。</summary>
        private static AuditEditor.AssemblyDependencyAuditResult CreateCycleResult(
            IReadOnlyList<AuditEditor.AssemblyDependencyNode> assemblies,
            IReadOnlyList<IReadOnlyList<int>> cycles)
        {
            return new AuditEditor.AssemblyDependencyAuditResult(
                assemblies,
                Array.Empty<AuditEditor.AssemblyDependencyIssue>(),
                null,
                null,
                cycles,
                Array.Empty<AuditEditor.AssemblyReferenceTarget>());
        }

        /// <summary>安全なlogical pathを持つcycle fixture nodeを指定数作ります。</summary>
        private static AuditEditor.AssemblyDependencyNode[] CreateCycleNodes(int count)
        {
            var nodes = new AuditEditor.AssemblyDependencyNode[count];
            for (var index = 0; index < nodes.Length; index++)
            {
                nodes[index] = CreateNode(
                    $"Cycle{index:D5}",
                    $"Assets/Cycles/Cycle{index:D5}.asmdef",
                    $"guid-cycle-{index:D5}",
                    false);
            }

            return nodes;
        }

        /// <summary>指定UTF-16 code unit数exactのAssets直下asmdef pathを作ります。</summary>
        private static string CreateLogicalAsmdefPath(int length)
        {
            const string prefix = "Assets/";
            const string suffix = ".asmdef";
            if (length < prefix.Length + suffix.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            return prefix + new string('x', length - prefix.Length - suffix.Length) + suffix;
        }

        /// <summary>不正cycle resultがgeneric errorと空memberだけを返すことを共通検証します。</summary>
        private static void AssertCycleComponentRejected(
            AuditEditor.AssemblyDependencyAuditResult result,
            int selectedAssemblyIndex,
            string absentCanary = null)
        {
            var succeeded = AuditEditor.AssemblyDependencyAuditWindow.TryGetCycleComponentMemberPaths(
                result,
                selectedAssemblyIndex,
                out var memberPaths,
                out var errorMessage);

            Assert.That(succeeded, Is.False);
            Assert.That(memberPaths, Is.Empty);
            Assert.That(errorMessage, Is.EqualTo("Cycle component result が不正なためmemberを表示できません。"));
            if (!string.IsNullOrEmpty(absentCanary))
            {
                Assert.That(errorMessage, Does.Not.Contain(absentCanary));
            }
        }

        /// <summary>Refresh failureまたはClear後のcycle stateが完全に空かを検証します。</summary>
        private static void AssertClearedCycleWindowState(AuditEditor.AssemblyDependencyAuditWindow window)
        {
            Assert.That(GetField<AuditEditor.AssemblyDependencyAuditResult>(window, "_result"), Is.Null);
            Assert.That(GetField<int>(window, "_cycleComponentPage"), Is.Zero);
            Assert.That(GetField<List<string>>(window, "_selectedCycleComponentMemberPaths"), Is.Empty);
            Assert.That(GetField<string>(window, "_cycleComponentErrorMessage"), Is.Empty);
            Assert.That(GetField<Vector2>(window, "_detailsScrollPosition"), Is.EqualTo(Vector2.zero));
        }

        /// <summary>Ordinal path順のasmref targetを指定数作ります。</summary>
        private static AuditEditor.AssemblyReferenceTarget[] CreateAssemblyReferences(int count)
        {
            var targets = new AuditEditor.AssemblyReferenceTarget[count];
            for (var index = 0; index < count; index++)
            {
                targets[index] = new AuditEditor.AssemblyReferenceTarget(
                    $"Assets/Refs/Ref{index:D3}.asmref",
                    "Target",
                    AuditEditor.AssemblyReferenceTargetKind.Name,
                    "Assets/Target.asmdef");
            }

            return targets;
        }

        /// <summary>結果とcacheをreflection fixtureへ設定します。</summary>
        private static void InitializeWindow(
            AuditEditor.AssemblyDependencyAuditWindow window,
            AuditEditor.AssemblyDependencyAuditResult result)
        {
            SetField(window, "_result", result);
            InvokeInstance(window, "RebuildResultCaches");
            InvokeInstance(window, "RebuildVisibleAssemblyIndices", false);
        }

        /// <summary>private instance fieldへ値を設定します。</summary>
        private static void SetField<T>(object instance, string fieldName, T value)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(instance, value);
        }

        /// <summary>private instance fieldの値を取得します。</summary>
        private static T GetField<T>(object instance, string fieldName)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(instance);
        }

        /// <summary>private instance methodを引数付きで呼びます。</summary>
        private static object InvokeInstance(object instance, string methodName, params object[] arguments)
        {
            var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(instance, arguments);
        }

        /// <summary>Windowのprivate static methodを呼びます。</summary>
        private static T InvokeStaticPrivate<T>(string methodName, params object[] arguments)
        {
            var method = typeof(AuditEditor.AssemblyDependencyAuditWindow).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return (T)method.Invoke(null, arguments);
        }

        /// <summary>UTF-16文字列に単独surrogateが残っているかを返します。</summary>
        private static bool ContainsUnpairedSurrogate(string value)
        {
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (char.IsHighSurrogate(character))
                {
                    if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                    {
                        return true;
                    }

                    index++;
                }
                else if (char.IsLowSurrogate(character))
                {
                    return true;
                }
            }

            return false;
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

        /// <summary>Window Refreshのexception経路を再現するsource adapterです。</summary>
        private sealed class ThrowingAssemblyDependencySourceAdapter : AuditEditor.IAssemblyDependencySourceAdapter
        {
            /// <summary>source読取時にfixture exceptionを送出します。</summary>
            public bool TryReadAll(
                out IReadOnlyList<AuditEditor.AssemblyDefinitionSource> sources,
                out AuditEditor.AssemblyDependencyAuditError error,
                out string errorMessage)
            {
                throw new InvalidOperationException("fixture throw");
            }

            /// <summary>Refreshは読取で停止するため、このresolverへは到達しません。</summary>
            public bool TryResolveReferencePath(string reference, out string assetPath)
            {
                throw new InvalidOperationException("fixture resolver should not run");
            }
        }

        /// <summary>filter 入力用の最小 node を作ります。</summary>
        private static AuditEditor.AssemblyDependencyNode CreateNode(
            string name,
            string assetPath,
            string guid,
            bool editorOnly,
            IReadOnlyList<AuditEditor.AssemblyDependencyReference> references = null)
        {
            return new AuditEditor.AssemblyDependencyNode(
                name,
                assetPath,
                guid,
                true,
                editorOnly ? new[] { "Editor" } : Array.Empty<string>(),
                Array.Empty<string>(),
                references ?? Array.Empty<AuditEditor.AssemblyDependencyReference>());
        }
    }
}
