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
            IReadOnlyList<AuditEditor.AssemblyReferenceTarget> assemblyReferences)
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
                Array.Empty<IReadOnlyList<int>>(),
                assemblyReferences);
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
