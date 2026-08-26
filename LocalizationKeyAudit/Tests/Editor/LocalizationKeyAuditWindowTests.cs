using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using AuditEditor = LocalizationKeyAudit.Editor;

namespace LocalizationKeyAudit.Tests
{
    /// <summary>
    /// Window の入力 parser、pure issue filter、4区分集計、表示上限を GUI なしで検証します。
    /// </summary>
    internal sealed class LocalizationKeyAuditWindowTests
    {
        /// <summary>
        /// Tools menu identity と問題一覧の 500 行 cap を v1.0.0 契約として固定します。
        /// </summary>
        [Test]
        public void Constants_MatchMenuAndDisplayContracts()
        {
            Assert.That(AuditEditor.LocalizationKeyAuditMenu.MenuPath, Is.EqualTo("Tools/Localization Key Audit/Open"));
            Assert.That(AuditEditor.LocalizationKeyAuditWindow.MaximumDisplayedIssues, Is.EqualTo(500));
        }

        /// <summary>
        /// required Locale はカンマ、semicolon、改行を区切りに trim し、入力順を保ちます。
        /// </summary>
        [Test]
        public void ParseRequiredLocales_ParsesSupportedSeparatorsAndDropsEmptyTokens()
        {
            var locales = AuditEditor.LocalizationKeyAuditWindow.ParseRequiredLocales(
                " en, ja;\r\nfr\n ; en ");

            Assert.That(locales, Is.EqualTo(new[] { "en", "ja", "fr", "en" }));
            Assert.That(AuditEditor.LocalizationKeyAuditWindow.ParseRequiredLocales(null), Is.Empty);
        }

        /// <summary>
        /// declared Assets/Packages pathは改行だけで分け、非空行の原文と入力順を保ちます。
        /// </summary>
        [Test]
        public void ParseDeclaredAssetPaths_PreservesNonEmptyLinesAndDropsWhitespaceOnlyLines()
        {
            var paths = AuditEditor.LocalizationKeyAuditWindow.ParseDeclaredAssetPaths(
                " Packages/com.zeta/Runtime \r\n \nAssets/Scenes\nPackages/com.alpha/UI ");

            Assert.That(paths, Is.EqualTo(new[]
            {
                " Packages/com.zeta/Runtime ",
                "Assets/Scenes",
                "Packages/com.alpha/UI "
            }));
            Assert.That(AuditEditor.LocalizationKeyAuditWindow.ParseDeclaredAssetPaths(null), Is.Empty);
        }

        /// <summary>declared pathの前後空白をparserで保持し、service validationで設定不正にします。</summary>
        [TestCase("Assets/Foo ")]
        [TestCase(" Assets/Foo")]
        public void Audit_RejectsDeclaredPathWhitespacePreservedByParser(string line)
        {
            var paths = AuditEditor.LocalizationKeyAuditWindow.ParseDeclaredAssetPaths(line);

            Assert.That(paths, Is.EqualTo(new[] { line }));
            var result = AuditEditor.LocalizationKeyAuditService.Audit(
                new[] { "en" },
                "Whitespace path",
                paths);

            Assert.That(result.IsComplete, Is.False);
            Assert.That(result.Coverage.RecognizedReferences, Is.Empty);
            Assert.That(result.Issues, Has.Count.EqualTo(1));
            Assert.That(
                result.Issues[0].Kind,
                Is.EqualTo(AuditEditor.LocalizationKeyAuditIssueKind.InvalidConfiguration));
            StringAssert.Contains("declared asset path", result.Issues[0].Message);
        }

        /// <summary>parserはmixed rootの順序を保ち、serviceはcoverage scan前に設定不正として拒否します。</summary>
        [Test]
        public void Audit_RejectsMixedParsedLogicalRoots()
        {
            var paths = AuditEditor.LocalizationKeyAuditWindow.ParseDeclaredAssetPaths(
                "Packages/com.example/Runtime\nAssets/Scenes");

            Assert.That(paths, Is.EqualTo(new[]
            {
                "Packages/com.example/Runtime",
                "Assets/Scenes"
            }));

            var result = AuditEditor.LocalizationKeyAuditService.Audit(
                new[] { "en" },
                "Mixed roots",
                paths);

            Assert.That(result.IsComplete, Is.False);
            Assert.That(result.Coverage.RecognizedReferences, Is.Empty);
            Assert.That(result.Issues, Has.Count.EqualTo(1));
            Assert.That(
                result.Issues[0].Kind,
                Is.EqualTo(AuditEditor.LocalizationKeyAuditIssueKind.InvalidConfiguration));
            StringAssert.Contains("logical root", result.Issues[0].Message);
        }

        /// <summary>
        /// frozen defaultを固定し、Clearがrequest/filter入力を残してtransient resultだけを消すことを確認します。
        /// </summary>
        [Test]
        public void DefaultsAndClear_PreserveInputsAndResetTransientState()
        {
            var window = ScriptableObject.CreateInstance<AuditEditor.LocalizationKeyAuditWindow>();
            try
            {
                Assert.That(GetField<string>(window, "declaredAssetPathsText"), Is.EqualTo("Assets"));
                Assert.That(
                    GetField<string>(window, "scopeDescription"),
                    Is.EqualTo("Assets text .unity/.prefab/.asset の GUID + key ID direct references"));

                SetField(window, "requiredLocalesText", "en, ja");
                SetField(window, "declaredAssetPathsText", "Packages/com.example\nAssets");
                SetField(window, "scopeDescription", "Frozen scope");
                SetField(window, "searchText", "package");
                SetField(
                    window,
                    "issueCategory",
                    AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.StaticReferences);
                SetField(window, "result", CreateEmptyResult());
                SetField(
                    window,
                    "issueCategoryCounts",
                    new AuditEditor.LocalizationKeyAuditWindow.IssueCategoryCounts(7, 6, 5, 4));
                GetField<List<int>>(window, "visibleIssueIndices").Add(0);
                SetField(window, "selectedIssueIndex", 0);
                SetField(window, "interactionMessage", "old status");
                SetField(window, "issueScrollPosition", new Vector2(1f, 2f));
                SetField(window, "detailScrollPosition", new Vector2(3f, 4f));
                SetField(window, "windowScrollPosition", new Vector2(5f, 6f));

                Invoke(window, "ClearResult");

                Assert.That(GetField<string>(window, "requiredLocalesText"), Is.EqualTo("en, ja"));
                Assert.That(
                    GetField<string>(window, "declaredAssetPathsText"),
                    Is.EqualTo("Packages/com.example\nAssets"));
                Assert.That(GetField<string>(window, "scopeDescription"), Is.EqualTo("Frozen scope"));
                Assert.That(GetField<string>(window, "searchText"), Is.EqualTo("package"));
                Assert.That(
                    GetField<AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter>(window, "issueCategory"),
                    Is.EqualTo(AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.StaticReferences));
                Assert.That(GetField<AuditEditor.LocalizationKeyAuditResult>(window, "result"), Is.Null);
                AssertCounts(
                    GetField<AuditEditor.LocalizationKeyAuditWindow.IssueCategoryCounts>(
                        window,
                        "issueCategoryCounts"),
                    0,
                    0,
                    0,
                    0);
                Assert.That(GetField<List<int>>(window, "visibleIssueIndices"), Is.Empty);
                Assert.That(GetField<int>(window, "selectedIssueIndex"), Is.EqualTo(-1));
                Assert.That(GetField<string>(window, "interactionMessage"), Is.Empty);
                Assert.That(GetField<Vector2>(window, "issueScrollPosition"), Is.EqualTo(Vector2.zero));
                Assert.That(GetField<Vector2>(window, "detailScrollPosition"), Is.EqualTo(Vector2.zero));
                Assert.That(GetField<Vector2>(window, "windowScrollPosition"), Is.EqualTo(Vector2.zero));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        /// <summary>UI token parserはmax+1 tokenと保持前の長大tokenを即時拒否します。</summary>
        [Test]
        public void ParseInputs_RejectCountsAndTokenLengthAtHardLimits()
        {
            var tooManyLocales = string.Join(",", Enumerable.Repeat(
                "en",
                AuditEditor.LocalizationKeyAuditLimits.MaximumRequiredLocales + 1));
            Assert.That(
                () => AuditEditor.LocalizationKeyAuditWindow.ParseRequiredLocales(tooManyLocales),
                Throws.TypeOf<AuditEditor.LocalizationKeyAuditLimitException>());

            var tooManyPaths = string.Join("\n", Enumerable.Repeat(
                "Assets/A",
                AuditEditor.LocalizationKeyAuditLimits.MaximumDeclaredAssetPaths + 1));
            Assert.That(
                () => AuditEditor.LocalizationKeyAuditWindow.ParseDeclaredAssetPaths(tooManyPaths),
                Throws.TypeOf<AuditEditor.LocalizationKeyAuditLimitException>());

            Assert.That(
                () => AuditEditor.LocalizationKeyAuditWindow.ParseRequiredLocales(
                    new string('x', AuditEditor.LocalizationKeyAuditLimits.MaximumTextCharacters + 1)),
                Throws.TypeOf<AuditEditor.LocalizationKeyAuditLimitException>());
        }

        /// <summary>
        /// 検索は kind、message、全 identity field を trim 済み大小文字無視で照合します。
        /// </summary>
        [TestCase(" danglingstaticreference ")]
        [TestCase("UNIQUE MESSAGE")]
        [TestCase("assets/source.prefab")]
        [TestCase("assets/related.asset")]
        [TestCase("collection display")]
        [TestCase("11111111-2222-3333-4444-555555555555")]
        [TestCase("JA-JP")]
        [TestCase("entry key")]
        [TestCase("123456")]
        public void MatchesFilter_SearchesEveryDisplayedFieldOrdinalIgnoreCase(string search)
        {
            var issue = CreateIssue(AuditEditor.LocalizationKeyAuditIssueKind.DanglingStaticReference);

            Assert.That(
                AuditEditor.LocalizationKeyAuditWindow.MatchesFilter(
                    issue,
                    search,
                    AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.All),
                Is.True);
        }

        /// <summary>
        /// null issue と検索不一致を除外し、null/空白検索は category 一致だけで表示します。
        /// </summary>
        [Test]
        public void MatchesFilter_HandlesNullAndEmptySearchDefensively()
        {
            var issue = CreateIssue(AuditEditor.LocalizationKeyAuditIssueKind.MissingDirectEntry);

            Assert.That(
                AuditEditor.LocalizationKeyAuditWindow.MatchesFilter(
                    null,
                    string.Empty,
                    AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.All),
                Is.False);
            Assert.That(
                AuditEditor.LocalizationKeyAuditWindow.MatchesFilter(
                    issue,
                    "not present",
                    AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.All),
                Is.False);
            Assert.That(
                AuditEditor.LocalizationKeyAuditWindow.MatchesFilter(
                    issue,
                    null,
                    AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.RequiredLocaleCoverage),
                Is.True);
            Assert.That(
                AuditEditor.LocalizationKeyAuditWindow.MatchesFilter(
                    issue,
                    "   ",
                    AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.RequiredLocaleCoverage),
                Is.True);
        }

        /// <summary>検索語はissue loop前に一度だけtrimし、長大入力を拒否します。</summary>
        [Test]
        public void NormalizeSearchText_TrimsOnceAndRejectsOversizeInput()
        {
            Assert.That(
                AuditEditor.LocalizationKeyAuditWindow.NormalizeSearchText("  Entry Key  "),
                Is.EqualTo("Entry Key"));
            Assert.That(
                () => AuditEditor.LocalizationKeyAuditWindow.NormalizeSearchText(
                    new string('x', AuditEditor.LocalizationKeyAuditLimits.MaximumTextCharacters + 1)),
                Throws.TypeOf<AuditEditor.LocalizationKeyAuditLimitException>());
        }

        /// <summary>
        /// 全 IssueKind を terminal、required coverage、static reference、integrity の一つだけへ割り当てます。
        /// </summary>
        [Test]
        public void ClassifyIssueKind_AssignsEveryIssueKindToExactlyOneCategory()
        {
            var expectedCategories = new Dictionary<AuditEditor.LocalizationKeyAuditIssueKind, AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter>
            {
                { AuditEditor.LocalizationKeyAuditIssueKind.ReadOnlyGuaranteeUnavailable, AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.Terminal },
                { AuditEditor.LocalizationKeyAuditIssueKind.InvalidConfiguration, AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.Terminal },
                { AuditEditor.LocalizationKeyAuditIssueKind.LimitExceeded, AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.Terminal },
                { AuditEditor.LocalizationKeyAuditIssueKind.AuditFailed, AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.Terminal },
                { AuditEditor.LocalizationKeyAuditIssueKind.RequiredLocaleNotConfigured, AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.RequiredLocaleCoverage },
                { AuditEditor.LocalizationKeyAuditIssueKind.MissingLocaleTable, AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.RequiredLocaleCoverage },
                { AuditEditor.LocalizationKeyAuditIssueKind.MissingDirectEntry, AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.RequiredLocaleCoverage },
                { AuditEditor.LocalizationKeyAuditIssueKind.EmptyDirectValue, AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.RequiredLocaleCoverage },
                { AuditEditor.LocalizationKeyAuditIssueKind.DanglingStaticReference, AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.StaticReferences },
                { AuditEditor.LocalizationKeyAuditIssueKind.NoStaticReferenceFoundWithinDeclaredScope, AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.StaticReferences },
                { AuditEditor.LocalizationKeyAuditIssueKind.StaticReferenceCoverageIncomplete, AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.StaticReferences },
                { AuditEditor.LocalizationKeyAuditIssueKind.DuplicateCollectionName, AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.Integrity },
                { AuditEditor.LocalizationKeyAuditIssueKind.DuplicateCollectionGuid, AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.Integrity },
                { AuditEditor.LocalizationKeyAuditIssueKind.DuplicateSharedEntryId, AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.Integrity },
                { AuditEditor.LocalizationKeyAuditIssueKind.DuplicateSharedEntryKey, AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.Integrity },
                { AuditEditor.LocalizationKeyAuditIssueKind.DuplicateLocaleTable, AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.Integrity },
                { AuditEditor.LocalizationKeyAuditIssueKind.DuplicateLocalizedEntryId, AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.Integrity },
                { AuditEditor.LocalizationKeyAuditIssueKind.OrphanedLocalizedEntry, AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.Integrity },
                { AuditEditor.LocalizationKeyAuditIssueKind.OrphanedLocaleTable, AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.Integrity },
                { AuditEditor.LocalizationKeyAuditIssueKind.OrphanedSharedTableData, AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.Integrity },
                { AuditEditor.LocalizationKeyAuditIssueKind.DuplicateLocaleIdentifier, AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.Integrity }
            };
            var categories = new[]
            {
                AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.Terminal,
                AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.RequiredLocaleCoverage,
                AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.StaticReferences,
                AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.Integrity
            };
            var kinds = (AuditEditor.LocalizationKeyAuditIssueKind[])Enum.GetValues(
                typeof(AuditEditor.LocalizationKeyAuditIssueKind));

            Assert.That(kinds, Is.EquivalentTo(expectedCategories.Keys));
            foreach (var kind in kinds)
            {
                var issue = CreateIssue(kind);
                Assert.That(
                    AuditEditor.LocalizationKeyAuditWindow.ClassifyIssueKind(kind),
                    Is.EqualTo(expectedCategories[kind]),
                    kind.ToString());
                Assert.That(
                    AuditEditor.LocalizationKeyAuditWindow.MatchesFilter(
                        issue,
                        string.Empty,
                        AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.All),
                    Is.True,
                    kind.ToString());
                foreach (var category in categories)
                {
                    Assert.That(
                        AuditEditor.LocalizationKeyAuditWindow.MatchesFilter(issue, string.Empty, category),
                        Is.EqualTo(category == expectedCategories[kind]),
                        $"{kind} -> {category}");
                }
            }

            AssertCounts(
                AuditEditor.LocalizationKeyAuditWindow.CountIssueCategories(
                    kinds.Select(CreateIssue).ToArray()),
                4,
                4,
                3,
                10);
        }

        /// <summary>集計はkindの種類数ではなく、各区分に発行されたissue頻度を数えます。</summary>
        [Test]
        public void CountIssueCategories_CountsEveryIssueOccurrence()
        {
            var issues = Enumerable.Repeat(
                    CreateIssue(AuditEditor.LocalizationKeyAuditIssueKind.InvalidConfiguration),
                    2)
                .Concat(Enumerable.Repeat(
                    CreateIssue(AuditEditor.LocalizationKeyAuditIssueKind.MissingDirectEntry),
                    3))
                .Concat(Enumerable.Repeat(
                    CreateIssue(AuditEditor.LocalizationKeyAuditIssueKind.DanglingStaticReference),
                    4))
                .Concat(Enumerable.Repeat(
                    CreateIssue(AuditEditor.LocalizationKeyAuditIssueKind.DuplicateLocaleIdentifier),
                    5))
                .ToArray();

            AssertCounts(
                AuditEditor.LocalizationKeyAuditWindow.CountIssueCategories(issues),
                2,
                3,
                4,
                5);
        }

        /// <summary>null一覧・null要素・未知kindは部分集計を返さず、空一覧だけを0件にします。</summary>
        [Test]
        public void CountIssueCategories_NullAndInvalidInputsFailClosed()
        {
            Assert.Throws<ArgumentNullException>(() =>
                AuditEditor.LocalizationKeyAuditWindow.CountIssueCategories(null));
            AssertCounts(
                AuditEditor.LocalizationKeyAuditWindow.CountIssueCategories(
                    Array.Empty<AuditEditor.LocalizationKeyAuditIssue>()),
                0,
                0,
                0,
                0);
            Assert.Throws<ArgumentException>(() =>
                AuditEditor.LocalizationKeyAuditWindow.CountIssueCategories(
                    new AuditEditor.LocalizationKeyAuditIssue[] { null }));

            var invalidKind = (AuditEditor.LocalizationKeyAuditIssueKind)int.MaxValue;
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AuditEditor.LocalizationKeyAuditWindow.ClassifyIssueKind(invalidKind));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AuditEditor.LocalizationKeyAuditWindow.MatchesFilter(
                    CreateIssue(invalidKind),
                    string.Empty,
                    AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.All));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AuditEditor.LocalizationKeyAuditWindow.CountIssueCategories(
                    new[] { CreateIssue(invalidKind) }));
        }

        /// <summary>表示capを超える501件とmodel上限exact 100000件も省略せず集計します。</summary>
        [Test]
        public void CountIssueCategories_CountsBeyondDisplayCapAndAtModelLimit()
        {
            var issue = CreateIssue(AuditEditor.LocalizationKeyAuditIssueKind.DanglingStaticReference);
            var beyondDisplayCap = Enumerable.Repeat(
                issue,
                AuditEditor.LocalizationKeyAuditWindow.MaximumDisplayedIssues + 1).ToArray();
            var atModelLimit = Enumerable.Repeat(
                issue,
                AuditEditor.LocalizationKeyAuditLimits.MaximumIssues).ToArray();

            Assert.That(beyondDisplayCap, Has.Length.EqualTo(501));
            AssertCounts(
                AuditEditor.LocalizationKeyAuditWindow.CountIssueCategories(beyondDisplayCap),
                0,
                0,
                501,
                0);
            Assert.That(atModelLimit, Has.Length.EqualTo(100000));
            AssertCounts(
                AuditEditor.LocalizationKeyAuditWindow.CountIssueCategories(atModelLimit),
                0,
                0,
                100000,
                0);
        }

        /// <summary>Incomplete結果の内訳はsearch/categoryで表示一覧が変わってもfilter前のままです。</summary>
        [Test]
        public void IncompleteResult_CategoryCountsRemainUnfilteredWhenFiltersChange()
        {
            var issues = new[]
            {
                CreateIssue(AuditEditor.LocalizationKeyAuditIssueKind.AuditFailed),
                CreateIssue(AuditEditor.LocalizationKeyAuditIssueKind.MissingDirectEntry),
                CreateIssue(AuditEditor.LocalizationKeyAuditIssueKind.DanglingStaticReference),
                CreateIssue(AuditEditor.LocalizationKeyAuditIssueKind.DuplicateLocaleIdentifier)
            };
            var result = CreateResult(false, issues, false);
            var counts = AuditEditor.LocalizationKeyAuditWindow.CountIssueCategories(result.Issues);
            var window = ScriptableObject.CreateInstance<AuditEditor.LocalizationKeyAuditWindow>();
            try
            {
                SetField(window, "result", result);
                SetField(window, "issueCategoryCounts", counts);
                SetField(window, "searchText", string.Empty);
                SetField(
                    window,
                    "issueCategory",
                    AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.All);

                Invoke(window, "RebuildVisibleIssues", true);

                Assert.That(result.IsComplete, Is.False);
                Assert.That(result.Coverage.IsComplete, Is.False);
                Assert.That(GetField<List<int>>(window, "visibleIssueIndices"),
                    Is.EqualTo(new[] { 0, 1, 2, 3 }));
                AssertCounts(
                    GetField<AuditEditor.LocalizationKeyAuditWindow.IssueCategoryCounts>(
                        window,
                        "issueCategoryCounts"),
                    1,
                    1,
                    1,
                    1);

                SetField(window, "searchText", "MissingDirectEntry");
                Invoke(window, "RebuildVisibleIssues", true);

                Assert.That(GetField<List<int>>(window, "visibleIssueIndices"), Is.EqualTo(new[] { 1 }));
                AssertCounts(
                    GetField<AuditEditor.LocalizationKeyAuditWindow.IssueCategoryCounts>(
                        window,
                        "issueCategoryCounts"),
                    1,
                    1,
                    1,
                    1);

                SetField(window, "searchText", string.Empty);
                SetField(
                    window,
                    "issueCategory",
                    AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.Integrity);
                Invoke(window, "RebuildVisibleIssues", true);

                Assert.That(GetField<List<int>>(window, "visibleIssueIndices"), Is.EqualTo(new[] { 3 }));
                AssertCounts(
                    GetField<AuditEditor.LocalizationKeyAuditWindow.IssueCategoryCounts>(
                        window,
                        "issueCategoryCounts"),
                    1,
                    1,
                    1,
                    1);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        /// <summary>成功したIncomplete監査は旧cacheを新しいresultの内訳へ置き換えます。</summary>
        [Test]
        public void RunAudit_IncompleteResultReplacesStaleCategoryCounts()
        {
            var window = ScriptableObject.CreateInstance<AuditEditor.LocalizationKeyAuditWindow>();
            try
            {
                SetField(window, "result", CreateEmptyResult());
                SetField(
                    window,
                    "issueCategoryCounts",
                    new AuditEditor.LocalizationKeyAuditWindow.IssueCategoryCounts(9, 8, 7, 6));
                SetField(window, "requiredLocalesText", "en");
                SetField(window, "declaredAssetPathsText", "Packages/com.example/Runtime\nAssets/Scenes");
                SetField(window, "scopeDescription", "Mixed roots");
                SetField(window, "searchText", string.Empty);
                SetField(
                    window,
                    "issueCategory",
                    AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.All);

                Invoke(window, "RunAudit");

                var result = GetField<AuditEditor.LocalizationKeyAuditResult>(window, "result");
                Assert.That(result, Is.Not.Null);
                Assert.That(result.IsComplete, Is.False);
                Assert.That(result.Issues, Has.Count.EqualTo(1));
                Assert.That(
                    result.Issues[0].Kind,
                    Is.EqualTo(AuditEditor.LocalizationKeyAuditIssueKind.InvalidConfiguration));
                AssertCounts(
                    GetField<AuditEditor.LocalizationKeyAuditWindow.IssueCategoryCounts>(
                        window,
                        "issueCategoryCounts"),
                    1,
                    0,
                    0,
                    0);
                Assert.That(GetField<List<int>>(window, "visibleIssueIndices"), Is.EqualTo(new[] { 0 }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        /// <summary>監査開始時の例外は旧resultと4区分cacheを同時に破棄します。</summary>
        [Test]
        public void RunAudit_InputExceptionClearsStaleCategoryCounts()
        {
            var window = ScriptableObject.CreateInstance<AuditEditor.LocalizationKeyAuditWindow>();
            try
            {
                SetField(window, "result", CreateResult(
                    true,
                    new[] { CreateIssue(AuditEditor.LocalizationKeyAuditIssueKind.DuplicateCollectionName) }));
                SetField(
                    window,
                    "issueCategoryCounts",
                    new AuditEditor.LocalizationKeyAuditWindow.IssueCategoryCounts(4, 3, 2, 1));
                GetField<List<int>>(window, "visibleIssueIndices").Add(0);
                SetField(window, "selectedIssueIndex", 0);
                SetField(
                    window,
                    "requiredLocalesText",
                    new string('x', AuditEditor.LocalizationKeyAuditLimits.MaximumTextCharacters + 1));

                Invoke(window, "RunAudit");

                Assert.That(GetField<AuditEditor.LocalizationKeyAuditResult>(window, "result"), Is.Null);
                AssertCounts(
                    GetField<AuditEditor.LocalizationKeyAuditWindow.IssueCategoryCounts>(
                        window,
                        "issueCategoryCounts"),
                    0,
                    0,
                    0,
                    0);
                Assert.That(GetField<List<int>>(window, "visibleIssueIndices"), Is.Empty);
                Assert.That(GetField<int>(window, "selectedIssueIndex"), Is.EqualTo(-1));
                Assert.That(
                    GetField<string>(window, "interactionMessage"),
                    Is.EqualTo("監査を開始できませんでした: LocalizationKeyAuditLimitException"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        /// <summary>全検索 field を埋めた issue を作ります。</summary>
        private static AuditEditor.LocalizationKeyAuditIssue CreateIssue(
            AuditEditor.LocalizationKeyAuditIssueKind kind)
        {
            return new AuditEditor.LocalizationKeyAuditIssue(
                kind,
                "Assets/Source.prefab",
                "Assets/Related.asset",
                "Collection Display",
                Guid.Parse("11111111-2222-3333-4444-555555555555"),
                "ja-JP",
                "Entry Key",
                123456,
                "Unique Message");
        }

        /// <summary>Clear前状態へ入れる最小complete resultを作ります。</summary>
        private static AuditEditor.LocalizationKeyAuditResult CreateEmptyResult()
        {
            return CreateResult(
                true,
                Array.Empty<AuditEditor.LocalizationKeyAuditIssue>());
        }

        /// <summary>Window状態へ入れる最小resultを作ります。</summary>
        private static AuditEditor.LocalizationKeyAuditResult CreateResult(
            bool isComplete,
            IReadOnlyList<AuditEditor.LocalizationKeyAuditIssue> issues,
            bool isCoverageComplete = true)
        {
            return new AuditEditor.LocalizationKeyAuditResult(
                isComplete,
                new AuditEditor.LocalizationKeyAuditCoverage(
                    "Assets",
                    new[] { "Assets" },
                    Array.Empty<AuditEditor.LocalizationKeyAuditStaticReference>(),
                    isCoverageComplete,
                    isCoverageComplete ? string.Empty : "Coverage incomplete"),
                Array.Empty<string>(),
                Array.Empty<AuditEditor.LocalizationKeyAuditCollectionSnapshot>(),
                issues,
                0);
        }

        /// <summary>4区分と合計のexact件数を検証します。</summary>
        private static void AssertCounts(
            AuditEditor.LocalizationKeyAuditWindow.IssueCategoryCounts actual,
            int terminal,
            int requiredLocaleCoverage,
            int staticReferences,
            int integrity)
        {
            Assert.That(actual.Terminal, Is.EqualTo(terminal));
            Assert.That(actual.RequiredLocaleCoverage, Is.EqualTo(requiredLocaleCoverage));
            Assert.That(actual.StaticReferences, Is.EqualTo(staticReferences));
            Assert.That(actual.Integrity, Is.EqualTo(integrity));
            Assert.That(
                actual.Total,
                Is.EqualTo(terminal + requiredLocaleCoverage + staticReferences + integrity));
        }

        /// <summary>必須private fieldを取得します。</summary>
        private static T GetField<T>(AuditEditor.LocalizationKeyAuditWindow window, string fieldName)
        {
            var field = GetFieldInfo(fieldName);
            return (T)field.GetValue(window);
        }

        /// <summary>必須private fieldへ値を設定します。</summary>
        private static void SetField<T>(
            AuditEditor.LocalizationKeyAuditWindow window,
            string fieldName,
            T value)
        {
            GetFieldInfo(fieldName).SetValue(window, value);
        }

        /// <summary>必須private field metadataを取得します。</summary>
        private static FieldInfo GetFieldInfo(string fieldName)
        {
            var field = typeof(AuditEditor.LocalizationKeyAuditWindow).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return field;
        }

        /// <summary>private methodを指定引数で呼び出します。</summary>
        private static void Invoke(
            AuditEditor.LocalizationKeyAuditWindow window,
            string methodName,
            params object[] arguments)
        {
            var method = typeof(AuditEditor.LocalizationKeyAuditWindow).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(window, arguments);
        }
    }
}
