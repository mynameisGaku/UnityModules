using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using AuditEditor = LocalizationKeyAudit.Editor;

namespace LocalizationKeyAudit.Tests
{
    /// <summary>
    /// Window の入力 parser、pure issue filter、4区分集計、表示・clipboard上限を GUI なしで検証します。
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
            Assert.That(
                AuditEditor.LocalizationKeyAuditWindow.MaximumDisplayedIssueClipboardCharacters,
                Is.EqualTo(1_048_576));
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

        /// <summary>current category filterのresult順、同内容duplicate、500件capをheader込みで固定します。</summary>
        [Test]
        public void TryBuildDisplayedIssuesClipboardText_UsesCurrentFilterOrderDuplicatesAndDisplayCap()
        {
            var issues = new List<AuditEditor.LocalizationKeyAuditIssue>();
            var duplicate = CreateMinimalIssue(
                AuditEditor.LocalizationKeyAuditIssueKind.DanglingStaticReference,
                "Duplicate");
            for (var pairIndex = 0; pairIndex <= 500; pairIndex++)
            {
                issues.Add(CreateMinimalIssue(
                    AuditEditor.LocalizationKeyAuditIssueKind.AuditFailed,
                    $"Terminal-{pairIndex:D3}"));
                issues.Add(pairIndex == 1 || pairIndex == 2
                    ? duplicate
                    : CreateMinimalIssue(
                        AuditEditor.LocalizationKeyAuditIssueKind.DanglingStaticReference,
                        $"Static-{pairIndex:D3}"));
            }

            var result = CreateResult(true, issues);
            var window = ScriptableObject.CreateInstance<AuditEditor.LocalizationKeyAuditWindow>();
            try
            {
                SetField(window, "result", result);
                SetField(window, "issueCategoryCounts", AuditEditor.LocalizationKeyAuditWindow.CountIssueCategories(
                    result.Issues));
                SetField(window, "searchText", string.Empty);
                SetField(
                    window,
                    "issueCategory",
                    AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.StaticReferences);
                Invoke(window, "RebuildVisibleIssues", true);

                var visibleIndices = GetField<List<int>>(window, "visibleIssueIndices");
                Assert.That(
                    visibleIndices,
                    Is.EqualTo(Enumerable.Range(0, 501).Select(index => index * 2 + 1).ToArray()));

                var succeeded = AuditEditor.LocalizationKeyAuditWindow.TryBuildDisplayedIssuesClipboardText(
                    result,
                    visibleIndices,
                    out var clipboardText,
                    out var copiedIssueCount);

                var expectedHeader = BuildExpectedClipboardHeader(true, true, 500, 501, 1002);
                Assert.That(succeeded, Is.True);
                Assert.That(copiedIssueCount, Is.EqualTo(500));
                Assert.That(
                    clipboardText,
                    Does.StartWith(
                        expectedHeader +
                        Environment.NewLine +
                        Environment.NewLine +
                        "Kind: DanglingStaticReference" +
                        Environment.NewLine +
                        "Message: Static-000"));
                Assert.That(CountOccurrences(clipboardText, "Message: Duplicate"), Is.EqualTo(2));
                Assert.That(
                    clipboardText.IndexOf("Message: Static-000", StringComparison.Ordinal),
                    Is.LessThan(clipboardText.IndexOf("Message: Duplicate", StringComparison.Ordinal)));
                Assert.That(
                    clipboardText.IndexOf("Message: Duplicate", StringComparison.Ordinal),
                    Is.LessThan(clipboardText.IndexOf("Message: Static-003", StringComparison.Ordinal)));
                StringAssert.Contains("Message: Static-499", clipboardText);
                StringAssert.DoesNotContain("Message: Static-500", clipboardText);
                StringAssert.DoesNotContain("Message: Terminal-", clipboardText);
                Assert.That(clipboardText.Length, Is.LessThanOrEqualTo(
                    AuditEditor.LocalizationKeyAuditWindow.MaximumDisplayedIssueClipboardCharacters));
                Assert.That(
                    visibleIndices,
                    Is.EqualTo(Enumerable.Range(0, 501).Select(index => index * 2 + 1).ToArray()));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        /// <summary>headerとseparatorを含むUTF-16 exact 1Miを許可し、+1をtruncateせず拒否します。</summary>
        [Test]
        public void TryBuildDisplayedIssuesClipboardText_AcceptsExactUtf16LimitAndRejectsOneOverAtomically()
        {
            CreateClipboardBoundaryFixture(
                AuditEditor.LocalizationKeyAuditWindow.MaximumDisplayedIssueClipboardCharacters,
                true,
                out var exactResult,
                out var exactIndices);
            var exactSucceeded = AuditEditor.LocalizationKeyAuditWindow.TryBuildDisplayedIssuesClipboardText(
                exactResult,
                exactIndices,
                out var exactText,
                out var exactCopiedCount);

            Assert.That(exactSucceeded, Is.True);
            Assert.That(
                exactText.Length,
                Is.EqualTo(AuditEditor.LocalizationKeyAuditWindow.MaximumDisplayedIssueClipboardCharacters));
            StringAssert.Contains("\U0001F600", exactText);
            Assert.That(exactCopiedCount, Is.EqualTo(exactIndices.Length));

            CreateClipboardBoundaryFixture(
                AuditEditor.LocalizationKeyAuditWindow.MaximumDisplayedIssueClipboardCharacters + 1,
                false,
                out var overflowResult,
                out var overflowIndices);
            AssertClipboardBuildRejected(overflowResult, overflowIndices);

            var previousClipboard = UnityEditor.EditorGUIUtility.systemCopyBuffer;
            var window = ScriptableObject.CreateInstance<AuditEditor.LocalizationKeyAuditWindow>();
            try
            {
                const string sentinel = "clipboard-overflow-sentinel";
                UnityEditor.EditorGUIUtility.systemCopyBuffer = sentinel;
                SetField(window, "result", overflowResult);
                GetField<List<int>>(window, "visibleIssueIndices").AddRange(overflowIndices);

                Invoke(window, "CopyDisplayedIssues");

                Assert.That(UnityEditor.EditorGUIUtility.systemCopyBuffer, Is.EqualTo(sentinel));
                Assert.That(
                    GetField<string>(window, "interactionMessage"),
                    Is.EqualTo("表示中の問題をclipboardへcopyできませんでした。監査結果とfilterを再確認してください。"));
            }
            finally
            {
                UnityEditor.EditorGUIUtility.systemCopyBuffer = previousClipboard;
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        /// <summary>null、未知kind、不正indexは部分本文・件数を返さずfail closedにします。</summary>
        [Test]
        public void TryBuildDisplayedIssuesClipboardText_RejectsInvalidSnapshotsAtomically()
        {
            var validIssue = CreateMinimalIssue(
                AuditEditor.LocalizationKeyAuditIssueKind.AuditFailed,
                "Valid");
            var validResult = CreateResult(true, new[] { validIssue });
            var twoIssueResult = CreateResult(true, new[] { validIssue, validIssue });

            AssertClipboardBuildRejected(null, new[] { 0 });
            AssertClipboardBuildRejected(validResult, null);
            AssertClipboardBuildRejected(CreateEmptyResult(), new[] { 0 });
            AssertClipboardBuildRejected(validResult, Array.Empty<int>());
            AssertClipboardBuildRejected(
                CreateResult(true, new AuditEditor.LocalizationKeyAuditIssue[] { null }),
                new[] { 0 });
            AssertClipboardBuildRejected(
                CreateResult(true, new[]
                {
                    validIssue,
                    CreateMinimalIssue((AuditEditor.LocalizationKeyAuditIssueKind)int.MaxValue, "Hidden invalid")
                }),
                new[] { 0 });
            AssertClipboardBuildRejected(validResult, new[] { -1 });
            AssertClipboardBuildRejected(validResult, new[] { 1 });
            AssertClipboardBuildRejected(twoIssueResult, new[] { 0, 0 });
            AssertClipboardBuildRejected(twoIssueResult, new[] { 1, 0 });
        }

        /// <summary>result/visible countはmodel上限exactを受け付け、+1を要素参照前に拒否します。</summary>
        [Test]
        public void TryBuildDisplayedIssuesClipboardText_EnforcesResultAndVisibleCountLimits()
        {
            var issue = CreateMinimalIssue(
                AuditEditor.LocalizationKeyAuditIssueKind.AuditFailed,
                "At limit");
            var result = CreateResult(
                true,
                new RepeatedReadOnlyList<AuditEditor.LocalizationKeyAuditIssue>(
                    issue,
                    AuditEditor.LocalizationKeyAuditLimits.MaximumIssues));
            var exactVisibleIndices = new SequentialIntReadOnlyList(
                AuditEditor.LocalizationKeyAuditLimits.MaximumIssues);

            var exactSucceeded = AuditEditor.LocalizationKeyAuditWindow.TryBuildDisplayedIssuesClipboardText(
                result,
                exactVisibleIndices,
                out var exactText,
                out var exactCopiedCount);

            Assert.That(exactSucceeded, Is.True);
            Assert.That(exactCopiedCount, Is.EqualTo(500));
            StringAssert.Contains("Displayed Issues: 500", exactText);
            StringAssert.Contains("Filtered Issues: 100000", exactText);
            StringAssert.Contains("Total Issues: 100000", exactText);

            AssertClipboardBuildRejected(
                result,
                new CountOnlyReadOnlyList<int>(AuditEditor.LocalizationKeyAuditLimits.MaximumIssues + 1));

            SetResultIssuesForTest(
                result,
                new CountOnlyReadOnlyList<AuditEditor.LocalizationKeyAuditIssue>(
                    AuditEditor.LocalizationKeyAuditLimits.MaximumIssues + 1));
            AssertClipboardBuildRejected(result, new[] { 0 });
        }

        /// <summary>Incompleteでも表示sliceだけをcopyし、filter・選択・4区分cacheを変更しません。</summary>
        [Test]
        public void CopyDisplayedIssues_IncompleteResultPreservesFilterSelectionAndCategoryCounts()
        {
            var issues = new[]
            {
                CreateMinimalIssue(AuditEditor.LocalizationKeyAuditIssueKind.AuditFailed, "Terminal"),
                CreateMinimalIssue(
                    AuditEditor.LocalizationKeyAuditIssueKind.DanglingStaticReference,
                    "Visible static")
            };
            var result = CreateResult(false, issues, false);
            var counts = AuditEditor.LocalizationKeyAuditWindow.CountIssueCategories(result.Issues);
            var previousClipboard = UnityEditor.EditorGUIUtility.systemCopyBuffer;
            var window = ScriptableObject.CreateInstance<AuditEditor.LocalizationKeyAuditWindow>();
            try
            {
                SetField(window, "result", result);
                SetField(window, "issueCategoryCounts", counts);
                SetField(window, "searchText", string.Empty);
                SetField(
                    window,
                    "issueCategory",
                    AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.StaticReferences);
                Invoke(window, "RebuildVisibleIssues", true);
                var visibleIndices = GetField<List<int>>(window, "visibleIssueIndices");
                Assert.That(visibleIndices, Is.EqualTo(new[] { 1 }));
                Assert.That(GetField<int>(window, "selectedIssueIndex"), Is.EqualTo(1));
                Assert.That(AuditEditor.LocalizationKeyAuditWindow.TryBuildDisplayedIssuesClipboardText(
                    result,
                    visibleIndices,
                    out var expectedText,
                    out var expectedCount), Is.True);

                UnityEditor.EditorGUIUtility.systemCopyBuffer = "clipboard-success-sentinel";
                Invoke(window, "CopyDisplayedIssues");

                Assert.That(expectedCount, Is.EqualTo(1));
                Assert.That(UnityEditor.EditorGUIUtility.systemCopyBuffer, Is.EqualTo(expectedText));
                Assert.That(
                    expectedText,
                    Does.StartWith(BuildExpectedClipboardHeader(false, false, 1, 1, 2)));
                Assert.That(
                    GetField<string>(window, "interactionMessage"),
                    Is.EqualTo("画面に表示中の問題 1 件をclipboardへcopyしました。"));
                Assert.That(GetField<AuditEditor.LocalizationKeyAuditResult>(window, "result"), Is.SameAs(result));
                Assert.That(visibleIndices, Is.EqualTo(new[] { 1 }));
                Assert.That(GetField<int>(window, "selectedIssueIndex"), Is.EqualTo(1));
                AssertCounts(
                    GetField<AuditEditor.LocalizationKeyAuditWindow.IssueCategoryCounts>(
                        window,
                        "issueCategoryCounts"),
                    1,
                    0,
                    1,
                    0);
            }
            finally
            {
                UnityEditor.EditorGUIUtility.systemCopyBuffer = previousClipboard;
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        /// <summary>invalid cache、Clear、RunAudit例外後はstale findingをclipboardへ再利用しません。</summary>
        [Test]
        public void CopyDisplayedIssues_InvalidClearAndRunExceptionLeaveClipboardUnchanged()
        {
            var previousClipboard = UnityEditor.EditorGUIUtility.systemCopyBuffer;
            var window = ScriptableObject.CreateInstance<AuditEditor.LocalizationKeyAuditWindow>();
            try
            {
                const string sentinel = "clipboard-stale-sentinel";
                var result = CreateResult(true, new[]
                {
                    CreateMinimalIssue(AuditEditor.LocalizationKeyAuditIssueKind.AuditFailed, "Stale"),
                    CreateMinimalIssue(AuditEditor.LocalizationKeyAuditIssueKind.AuditFailed, "Stale second")
                });
                UnityEditor.EditorGUIUtility.systemCopyBuffer = sentinel;
                SetField(window, "result", result);
                GetField<List<int>>(window, "visibleIssueIndices").AddRange(new[] { 0, 0 });

                Invoke(window, "CopyDisplayedIssues");

                Assert.That(UnityEditor.EditorGUIUtility.systemCopyBuffer, Is.EqualTo(sentinel));
                Assert.That(
                    GetField<string>(window, "interactionMessage"),
                    Is.EqualTo("表示中の問題をclipboardへcopyできませんでした。監査結果とfilterを再確認してください。"));

                Invoke(window, "ClearResult");
                Assert.That(GetField<AuditEditor.LocalizationKeyAuditResult>(window, "result"), Is.Null);
                Assert.That(GetField<List<int>>(window, "visibleIssueIndices"), Is.Empty);
                Invoke(window, "CopyDisplayedIssues");
                Assert.That(UnityEditor.EditorGUIUtility.systemCopyBuffer, Is.EqualTo(sentinel));

                SetField(window, "result", result);
                GetField<List<int>>(window, "visibleIssueIndices").Add(0);
                SetField(
                    window,
                    "requiredLocalesText",
                    new string('x', AuditEditor.LocalizationKeyAuditLimits.MaximumTextCharacters + 1));
                Invoke(window, "RunAudit");
                Assert.That(GetField<AuditEditor.LocalizationKeyAuditResult>(window, "result"), Is.Null);
                Assert.That(GetField<List<int>>(window, "visibleIssueIndices"), Is.Empty);

                Invoke(window, "CopyDisplayedIssues");

                Assert.That(UnityEditor.EditorGUIUtility.systemCopyBuffer, Is.EqualTo(sentinel));
                Assert.That(
                    GetField<string>(window, "interactionMessage"),
                    Is.EqualTo("表示中の問題をclipboardへcopyできませんでした。監査結果とfilterを再確認してください。"));
            }
            finally
            {
                UnityEditor.EditorGUIUtility.systemCopyBuffer = previousClipboard;
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        /// <summary>一括copy追加後も既存single Copy Details本文をbyte-for-byte維持します。</summary>
        [Test]
        public void BuildIssueDetails_RemainsExactAfterDisplayedCopyAddition()
        {
            var issue = CreateIssue(AuditEditor.LocalizationKeyAuditIssueKind.DanglingStaticReference);
            var details = InvokeStatic<string>("BuildIssueDetails", issue);

            Assert.That(details, Is.EqualTo(string.Join(Environment.NewLine, new[]
            {
                "Kind: DanglingStaticReference",
                "Message: Unique Message",
                "Asset: Assets/Source.prefab",
                "Related: Assets/Related.asset",
                "Collection: Collection Display",
                "Collection GUID: 11111111-2222-3333-4444-555555555555",
                "Locale: ja-JP",
                "Entry Key: Entry Key",
                "Entry ID: 123456"
            })));
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

        /// <summary>clipboard本文の長さと順序を制御しやすい最小issueを作ります。</summary>
        private static AuditEditor.LocalizationKeyAuditIssue CreateMinimalIssue(
            AuditEditor.LocalizationKeyAuditIssueKind kind,
            string message)
        {
            return new AuditEditor.LocalizationKeyAuditIssue(
                kind,
                string.Empty,
                string.Empty,
                string.Empty,
                Guid.Empty,
                string.Empty,
                string.Empty,
                0,
                message);
        }

        /// <summary>clipboard headerの6行をproduction契約どおり組み立てます。</summary>
        private static string BuildExpectedClipboardHeader(
            bool isComplete,
            bool isCoverageComplete,
            int displayedCount,
            int filteredCount,
            int totalCount)
        {
            return string.Join(Environment.NewLine, new[]
            {
                "Localization Key Audit - Displayed Issues",
                $"Result: {(isComplete ? "Complete" : "Incomplete")}",
                $"Static Coverage: {(isCoverageComplete ? "Complete" : "Incomplete")}",
                $"Displayed Issues: {displayedCount}",
                $"Filtered Issues: {filteredCount}",
                $"Total Issues: {totalCount}"
            });
        }

        /// <summary>33 issueへmessage長を分配し、指定UTF-16長exactの候補snapshotを作ります。</summary>
        private static void CreateClipboardBoundaryFixture(
            int targetLength,
            bool includeSurrogatePair,
            out AuditEditor.LocalizationKeyAuditResult result,
            out int[] visibleIndices)
        {
            const int issueCount = 33;
            var messages = Enumerable.Repeat("x", issueCount).ToArray();
            visibleIndices = Enumerable.Range(0, issueCount).ToArray();
            var seedResult = CreateResult(
                true,
                messages.Select(message => CreateMinimalIssue(
                    AuditEditor.LocalizationKeyAuditIssueKind.AuditFailed,
                    message)).ToArray());
            Assert.That(AuditEditor.LocalizationKeyAuditWindow.TryBuildDisplayedIssuesClipboardText(
                seedResult,
                visibleIndices,
                out var seedText,
                out var seedCopiedCount), Is.True);
            Assert.That(seedCopiedCount, Is.EqualTo(issueCount));

            var remainingCharacters = targetLength - seedText.Length;
            Assert.That(remainingCharacters, Is.GreaterThanOrEqualTo(0));
            for (var index = 0; index < messages.Length && remainingCharacters > 0; index++)
            {
                var addedCharacters = Math.Min(
                    remainingCharacters,
                    AuditEditor.LocalizationKeyAuditLimits.MaximumTextCharacters - messages[index].Length);
                messages[index] = new string('x', messages[index].Length + addedCharacters);
                remainingCharacters -= addedCharacters;
            }

            Assert.That(remainingCharacters, Is.Zero, "fixture capacity");
            if (includeSurrogatePair)
            {
                Assert.That(messages[0].Length, Is.GreaterThanOrEqualTo(2));
                messages[0] = messages[0].Substring(0, messages[0].Length - 2) + "\U0001F600";
                Assert.That("\U0001F600".Length, Is.EqualTo(2));
            }

            var finalLength = seedText.Length + messages.Sum(message => message.Length - 1);
            Assert.That(finalLength, Is.EqualTo(targetLength));
            Assert.That(
                messages.All(message => message.Length <= AuditEditor.LocalizationKeyAuditLimits.MaximumTextCharacters),
                Is.True);
            result = CreateResult(
                true,
                messages.Select(message => CreateMinimalIssue(
                    AuditEditor.LocalizationKeyAuditIssueKind.AuditFailed,
                    message)).ToArray());
        }

        /// <summary>失敗時out本文と件数が必ずdefaultへ戻ることを共通検証します。</summary>
        private static void AssertClipboardBuildRejected(
            AuditEditor.LocalizationKeyAuditResult result,
            IReadOnlyList<int> visibleIndices)
        {
            var succeeded = AuditEditor.LocalizationKeyAuditWindow.TryBuildDisplayedIssuesClipboardText(
                result,
                visibleIndices,
                out var clipboardText,
                out var copiedIssueCount);

            Assert.That(succeeded, Is.False);
            Assert.That(clipboardText, Is.Empty);
            Assert.That(copiedIssueCount, Is.Zero);
        }

        /// <summary>重複した本文断片の出現頻度をordinalで数えます。</summary>
        private static int CountOccurrences(string source, string value)
        {
            Assert.That(value, Is.Not.Empty);
            var count = 0;
            var start = 0;
            while (start <= source.Length - value.Length)
            {
                var index = source.IndexOf(value, start, StringComparison.Ordinal);
                if (index < 0)
                {
                    break;
                }

                count++;
                start = index + value.Length;
            }

            return count;
        }

        /// <summary>巨大arrayを二重生成せずresult issue countの+1 guardだけを注入します。</summary>
        private static void SetResultIssuesForTest(
            AuditEditor.LocalizationKeyAuditResult result,
            IReadOnlyList<AuditEditor.LocalizationKeyAuditIssue> issues)
        {
            var field = typeof(AuditEditor.LocalizationKeyAuditResult).GetField(
                "<Issues>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "LocalizationKeyAuditResult.Issues backing field");
            field.SetValue(result, issues);
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

        /// <summary>必須private static methodを呼び、既存formatの戻り値を取得します。</summary>
        private static T InvokeStatic<T>(string methodName, params object[] arguments)
        {
            var method = typeof(AuditEditor.LocalizationKeyAuditWindow).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return (T)method.Invoke(null, arguments);
        }

        /// <summary>同じ要素を指定countだけ返し、model limit fixtureのsource allocationを抑えます。</summary>
        private sealed class RepeatedReadOnlyList<T> : IReadOnlyList<T>
        {
            private readonly T value;

            internal RepeatedReadOnlyList(T value, int count)
            {
                this.value = value;
                Count = count;
            }

            public int Count { get; }

            public T this[int index]
            {
                get
                {
                    if (index < 0 || index >= Count)
                    {
                        throw new ArgumentOutOfRangeException(nameof(index));
                    }

                    return value;
                }
            }

            public IEnumerator<T> GetEnumerator()
            {
                for (var index = 0; index < Count; index++)
                {
                    yield return value;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        /// <summary>0..Count-1をallocationなしで返し、visible count exact limitを検証します。</summary>
        private sealed class SequentialIntReadOnlyList : IReadOnlyList<int>
        {
            internal SequentialIntReadOnlyList(int count)
            {
                Count = count;
            }

            public int Count { get; }

            public int this[int index]
            {
                get
                {
                    if (index < 0 || index >= Count)
                    {
                        throw new ArgumentOutOfRangeException(nameof(index));
                    }

                    return index;
                }
            }

            public IEnumerator<int> GetEnumerator()
            {
                for (var index = 0; index < Count; index++)
                {
                    yield return index;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        /// <summary>Count guard後にindexerを触らないことを検証する境界listです。</summary>
        private sealed class CountOnlyReadOnlyList<T> : IReadOnlyList<T>
        {
            internal CountOnlyReadOnlyList(int count)
            {
                Count = count;
            }

            public int Count { get; }

            public T this[int index] => throw new InvalidOperationException("Count guardより後へ進みました。");

            public IEnumerator<T> GetEnumerator()
            {
                throw new InvalidOperationException("Count guardより後へ進みました。");
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
