using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using AuditEditor = LocalizationKeyAudit.Editor;

namespace LocalizationKeyAudit.Tests
{
    /// <summary>
    /// Window の入力 parser、pure issue filter、表示上限を GUI なしで検証します。
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
        /// declared Assets path は改行だけで分け、各 token を trim して入力順を保ちます。
        /// </summary>
        [Test]
        public void ParseDeclaredAssetPaths_ParsesLinesAndDropsEmptyTokens()
        {
            var paths = AuditEditor.LocalizationKeyAuditWindow.ParseDeclaredAssetPaths(
                " Assets/Scenes \r\n\nAssets/Prefabs\n Assets/UI ");

            Assert.That(paths, Is.EqualTo(new[]
            {
                "Assets/Scenes",
                "Assets/Prefabs",
                "Assets/UI"
            }));
            Assert.That(AuditEditor.LocalizationKeyAuditWindow.ParseDeclaredAssetPaths(null), Is.Empty);
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
        public void MatchesFilter_AssignsEveryIssueKindToExactlyOneCategory()
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

            foreach (AuditEditor.LocalizationKeyAuditIssueKind kind in Enum.GetValues(
                         typeof(AuditEditor.LocalizationKeyAuditIssueKind)))
            {
                var issue = CreateIssue(kind);
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
    }
}
