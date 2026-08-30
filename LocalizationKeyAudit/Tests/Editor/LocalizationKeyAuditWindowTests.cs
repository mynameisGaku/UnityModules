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
    /// 画面の入力解析、問題の絞り込み、4区分集計、ページ表示、クリップボード上限を画面描画なしで検証します。
    /// </summary>
    internal sealed class LocalizationKeyAuditWindowTests
    {
        /// <summary>
        /// ツールメニュー、画面タイトル、既定の範囲説明、問題一覧の1ページ500行上限を固定します。
        /// </summary>
        [Test]
        public void Constants_MatchMenuAndDisplayContracts()
        {
            Assert.That(AuditEditor.LocalizationKeyAuditMenu.MenuPath, Is.EqualTo("Tools/ローカライズキー監査/開く"));
            Assert.That(AuditEditor.LocalizationKeyAuditWindow.WindowTitle, Is.EqualTo("ローカライズキー監査"));
            Assert.That(
                AuditEditor.LocalizationKeyAuditWindow.DefaultScopeDescription,
                Is.EqualTo("Assets内のテキスト形式の.unity、.prefab、.assetにあるGUIDと項目識別子の直接参照"));
            Assert.That(AuditEditor.LocalizationKeyAuditWindow.MaximumDisplayedIssues, Is.EqualTo(500));
            Assert.That(
                AuditEditor.LocalizationKeyAuditWindow.MaximumDisplayedIssueClipboardCharacters,
                Is.EqualTo(1_048_576));
        }

        /// <summary>問題件数の境界からページ数、有効範囲、開始位置を整数演算だけで決定します。</summary>
        [Test]
        public void IssuePageHelpers_HandleEmptyBoundaryAndAllTwoHundredPages()
        {
            Assert.That(AuditEditor.LocalizationKeyAuditWindow.GetIssuePageCount(-1), Is.EqualTo(1));
            Assert.That(AuditEditor.LocalizationKeyAuditWindow.GetIssuePageCount(0), Is.EqualTo(1));
            Assert.That(AuditEditor.LocalizationKeyAuditWindow.GetIssuePageCount(500), Is.EqualTo(1));
            Assert.That(AuditEditor.LocalizationKeyAuditWindow.GetIssuePageCount(501), Is.EqualTo(2));
            Assert.That(AuditEditor.LocalizationKeyAuditWindow.GetIssuePageCount(100000), Is.EqualTo(200));

            Assert.That(AuditEditor.LocalizationKeyAuditWindow.ClampIssuePage(-1, 2), Is.Zero);
            Assert.That(AuditEditor.LocalizationKeyAuditWindow.ClampIssuePage(0, 2), Is.Zero);
            Assert.That(AuditEditor.LocalizationKeyAuditWindow.ClampIssuePage(1, 2), Is.EqualTo(1));
            Assert.That(AuditEditor.LocalizationKeyAuditWindow.ClampIssuePage(2, 2), Is.EqualTo(1));
            Assert.That(AuditEditor.LocalizationKeyAuditWindow.ClampIssuePage(7, 0), Is.Zero);

            Assert.That(AuditEditor.LocalizationKeyAuditWindow.GetIssuePageStart(-1, -1), Is.Zero);
            Assert.That(AuditEditor.LocalizationKeyAuditWindow.GetIssuePageStart(7, 0), Is.Zero);
            Assert.That(AuditEditor.LocalizationKeyAuditWindow.GetIssuePageStart(-1, 501), Is.Zero);
            Assert.That(AuditEditor.LocalizationKeyAuditWindow.GetIssuePageStart(0, 501), Is.Zero);
            Assert.That(AuditEditor.LocalizationKeyAuditWindow.GetIssuePageStart(1, 501), Is.EqualTo(500));
            Assert.That(AuditEditor.LocalizationKeyAuditWindow.GetIssuePageStart(2, 501), Is.EqualTo(500));
            Assert.That(
                Enumerable.Range(0, 200).Select(page =>
                    AuditEditor.LocalizationKeyAuditWindow.GetIssuePageStart(page, 100000)),
                Is.EqualTo(Enumerable.Range(0, 200).Select(page => page * 500)));
        }

        /// <summary>日本語タイトルを復元し、未変更の旧既定値だけを移行して独自の範囲説明は保持します。</summary>
        [Test]
        public void OnEnable_RestoresJapaneseTitleAndMigratesOnlyExactLegacyScopeDescription()
        {
            const string legacyDefault =
                "Assets text .unity/.prefab/.asset の GUID + key ID direct references";
            var legacyWindow = ScriptableObject.CreateInstance<AuditEditor.LocalizationKeyAuditWindow>();
            try
            {
                legacyWindow.titleContent = new GUIContent("Localization Key Audit");
                SetField(legacyWindow, "scopeDescription", legacyDefault);

                Invoke(legacyWindow, "OnEnable");

                Assert.That(legacyWindow.titleContent, Is.Not.Null);
                Assert.That(
                    legacyWindow.titleContent.text,
                    Is.EqualTo(AuditEditor.LocalizationKeyAuditWindow.WindowTitle));
                Assert.That(
                    GetField<string>(legacyWindow, "scopeDescription"),
                    Is.EqualTo(AuditEditor.LocalizationKeyAuditWindow.DefaultScopeDescription));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(legacyWindow);
            }

            var customDescriptions = new[]
            {
                legacyDefault + " ",
                "利用者独自の範囲説明",
                string.Empty,
                null
            };
            foreach (var customDescription in customDescriptions)
            {
                var customWindow = ScriptableObject.CreateInstance<AuditEditor.LocalizationKeyAuditWindow>();
                try
                {
                    customWindow.titleContent = new GUIContent("Localization Key Audit");
                    SetField(customWindow, "scopeDescription", customDescription);

                    Invoke(customWindow, "OnEnable");

                    Assert.That(customWindow.titleContent, Is.Not.Null);
                    Assert.That(
                        customWindow.titleContent.text,
                        Is.EqualTo(AuditEditor.LocalizationKeyAuditWindow.WindowTitle));
                    Assert.That(
                        GetField<string>(customWindow, "scopeDescription"),
                        Is.EqualTo(customDescription));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(customWindow);
                }
            }
        }

        /// <summary>
        /// 必須ロケールはカンマ、セミコロン、改行を区切りに前後空白を除き、入力順を保ちます。
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
        /// 対象とする Assets または Packages のパスは改行だけで分け、非空行の原文と入力順を保ちます。
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

        /// <summary>対象パスの前後空白を入力解析で保持し、監査処理の検証で設定不正にします。</summary>
        [TestCase("Assets/Foo ")]
        [TestCase(" Assets/Foo")]
        public void Audit_RejectsDeclaredPathWhitespacePreservedByParser(string line)
        {
            var paths = AuditEditor.LocalizationKeyAuditWindow.ParseDeclaredAssetPaths(line);

            Assert.That(paths, Is.EqualTo(new[] { line }));
            var result = AuditEditor.LocalizationKeyAuditService.Audit(
                new[] { "en" },
                "前後空白を含むパス",
                paths);

            Assert.That(result.IsComplete, Is.False);
            Assert.That(result.Coverage.RecognizedReferences, Is.Empty);
            Assert.That(result.Issues, Has.Count.EqualTo(1));
            Assert.That(
                result.Issues[0].Kind,
                Is.EqualTo(AuditEditor.LocalizationKeyAuditIssueKind.InvalidConfiguration));
            StringAssert.Contains("宣言済みアセットパス", result.Issues[0].Message);
        }

        /// <summary>入力解析は異なる基点の順序を保ち、監査処理は網羅走査前に設定不正として拒否します。</summary>
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
                "異なる論理ルート",
                paths);

            Assert.That(result.IsComplete, Is.False);
            Assert.That(result.Coverage.RecognizedReferences, Is.Empty);
            Assert.That(result.Issues, Has.Count.EqualTo(1));
            Assert.That(
                result.Issues[0].Kind,
                Is.EqualTo(AuditEditor.LocalizationKeyAuditIssueKind.InvalidConfiguration));
            StringAssert.Contains("論理ルート", result.Issues[0].Message);
        }

        /// <summary>
        /// 日本語の既定値を固定し、消去操作が監査条件と絞り込みを残して一時結果だけを消すことを確認します。
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
                    Is.EqualTo(AuditEditor.LocalizationKeyAuditWindow.DefaultScopeDescription));

                SetField(window, "requiredLocalesText", "en, ja");
                SetField(window, "declaredAssetPathsText", "Packages/com.example\nAssets");
                SetField(window, "scopeDescription", "保持する走査範囲");
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
                SetField(window, "issuePage", 1);
                SetField(window, "selectedIssueIndex", 0);
                SetField(window, "interactionMessage", "以前の状態");
                SetField(window, "issueScrollPosition", new Vector2(1f, 2f));
                SetField(window, "detailScrollPosition", new Vector2(3f, 4f));
                SetField(window, "windowScrollPosition", new Vector2(5f, 6f));

                Invoke(window, "ClearResult");

                Assert.That(GetField<string>(window, "requiredLocalesText"), Is.EqualTo("en, ja"));
                Assert.That(
                    GetField<string>(window, "declaredAssetPathsText"),
                    Is.EqualTo("Packages/com.example\nAssets"));
                Assert.That(GetField<string>(window, "scopeDescription"), Is.EqualTo("保持する走査範囲"));
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
                Assert.That(GetField<int>(window, "issuePage"), Is.Zero);
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

        /// <summary>画面入力の解析は件数上限超過と、保持前の長大な入力要素を即時拒否します。</summary>
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
        /// 検索は日本語の問題種別名、従来の列挙識別子、説明、全識別項目を前後空白除去済みで照合します。
        /// </summary>
        [TestCase(" danglingstaticreference ")]
        [TestCase(" 解決不能な静的参照 ")]
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
        /// 空の問題と検索不一致を除外し、空または空白だけの検索は区分一致だけで表示します。
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
                    "存在しない語句",
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

        /// <summary>検索語は問題走査前に一度だけ前後空白を除き、長大入力を拒否します。</summary>
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

        /// <summary>表示区分5種の日本語名と選択欄の並びを固定し、未知の区分を拒否します。</summary>
        [Test]
        public void GetIssueCategoryDisplayName_CoversEveryJapaneseLabelAndRejectsUnknownValue()
        {
            var categories = new[]
            {
                AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.All,
                AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.Terminal,
                AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.RequiredLocaleCoverage,
                AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.StaticReferences,
                AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.Integrity
            };
            var expectedDisplayNames = new[]
            {
                "すべて",
                "監査停止",
                "必須ロケール網羅",
                "静的参照",
                "整合性"
            };

            Assert.That(
                (AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter[])Enum.GetValues(
                    typeof(AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter)),
                Is.EqualTo(categories));
            Assert.That(
                categories.Select(AuditEditor.LocalizationKeyAuditWindow.GetIssueCategoryDisplayName),
                Is.EqualTo(expectedDisplayNames));
            Assert.That(
                GetStaticField<string[]>("IssueCategoryDisplayNames"),
                Is.EqualTo(expectedDisplayNames));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AuditEditor.LocalizationKeyAuditWindow.GetIssueCategoryDisplayName(
                    (AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter)int.MaxValue));
        }

        /// <summary>問題種別21種の日本語名を固定し、日本語名と従来の列挙識別子の両方で検索できます。</summary>
        [Test]
        public void GetIssueKindDisplayName_CoversEveryJapaneseLabelAndBothSearchForms()
        {
            var expectedDisplayNames = new Dictionary<AuditEditor.LocalizationKeyAuditIssueKind, string>
            {
                { AuditEditor.LocalizationKeyAuditIssueKind.ReadOnlyGuaranteeUnavailable, "読み取り専用保証不可" },
                { AuditEditor.LocalizationKeyAuditIssueKind.InvalidConfiguration, "設定不備" },
                { AuditEditor.LocalizationKeyAuditIssueKind.LimitExceeded, "上限超過" },
                { AuditEditor.LocalizationKeyAuditIssueKind.AuditFailed, "監査失敗" },
                { AuditEditor.LocalizationKeyAuditIssueKind.RequiredLocaleNotConfigured, "必須ロケール未登録" },
                { AuditEditor.LocalizationKeyAuditIssueKind.MissingLocaleTable, "ロケールテーブル不足" },
                { AuditEditor.LocalizationKeyAuditIssueKind.MissingDirectEntry, "直接項目不足" },
                { AuditEditor.LocalizationKeyAuditIssueKind.EmptyDirectValue, "直接値が空" },
                { AuditEditor.LocalizationKeyAuditIssueKind.DanglingStaticReference, "解決不能な静的参照" },
                { AuditEditor.LocalizationKeyAuditIssueKind.NoStaticReferenceFoundWithinDeclaredScope, "宣言範囲内の静的参照なし" },
                { AuditEditor.LocalizationKeyAuditIssueKind.StaticReferenceCoverageIncomplete, "静的参照網羅が未完了" },
                { AuditEditor.LocalizationKeyAuditIssueKind.DuplicateCollectionName, "コレクション名重複" },
                { AuditEditor.LocalizationKeyAuditIssueKind.DuplicateCollectionGuid, "コレクション識別子（GUID）重複" },
                { AuditEditor.LocalizationKeyAuditIssueKind.DuplicateSharedEntryId, "共有項目識別子重複" },
                { AuditEditor.LocalizationKeyAuditIssueKind.DuplicateSharedEntryKey, "共有項目キー重複" },
                { AuditEditor.LocalizationKeyAuditIssueKind.DuplicateLocaleTable, "ロケールテーブル重複" },
                { AuditEditor.LocalizationKeyAuditIssueKind.DuplicateLocalizedEntryId, "翻訳項目識別子重複" },
                { AuditEditor.LocalizationKeyAuditIssueKind.OrphanedLocalizedEntry, "所属先なし翻訳項目" },
                { AuditEditor.LocalizationKeyAuditIssueKind.OrphanedLocaleTable, "所属先なしロケールテーブル" },
                { AuditEditor.LocalizationKeyAuditIssueKind.OrphanedSharedTableData, "所属先なし共有テーブルデータ" },
                { AuditEditor.LocalizationKeyAuditIssueKind.DuplicateLocaleIdentifier, "ロケール識別子重複" }
            };
            var kinds = (AuditEditor.LocalizationKeyAuditIssueKind[])Enum.GetValues(
                typeof(AuditEditor.LocalizationKeyAuditIssueKind));

            Assert.That(kinds, Is.EquivalentTo(expectedDisplayNames.Keys));
            foreach (var kind in kinds)
            {
                var issue = CreateIssue(kind);
                Assert.That(
                    AuditEditor.LocalizationKeyAuditWindow.GetIssueKindDisplayName(kind),
                    Is.EqualTo(expectedDisplayNames[kind]),
                    kind.ToString());
                Assert.That(
                    AuditEditor.LocalizationKeyAuditWindow.MatchesFilter(
                        issue,
                        expectedDisplayNames[kind],
                        AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.All),
                    Is.True,
                    $"日本語表示名: {kind}");
                Assert.That(
                    AuditEditor.LocalizationKeyAuditWindow.MatchesFilter(
                        issue,
                        kind.ToString(),
                        AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.All),
                    Is.True,
                    $"列挙識別子: {kind}");
            }

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AuditEditor.LocalizationKeyAuditWindow.GetIssueKindDisplayName(
                    (AuditEditor.LocalizationKeyAuditIssueKind)int.MaxValue));
        }

        /// <summary>日本語の詳細項目でも、生成後のUTF-16長と事前計算値が問題種別21種すべてで一致します。</summary>
        [Test]
        public void IssueDetailsLength_MatchesGeneratedJapaneseDetailsForEveryIssueKind()
        {
            var kinds = (AuditEditor.LocalizationKeyAuditIssueKind[])Enum.GetValues(
                typeof(AuditEditor.LocalizationKeyAuditIssueKind));
            foreach (var kind in kinds)
            {
                var issue = CreateIssue(kind);
                var details = InvokeStatic<string>("BuildIssueDetails", issue);
                var lengthArguments = new object[] { issue, 0 };
                var succeeded = InvokeStatic<bool>("TryGetIssueDetailsLength", lengthArguments);

                Assert.That(succeeded, Is.True, kind.ToString());
                Assert.That((int)lengthArguments[1], Is.EqualTo(details.Length), kind.ToString());
                Assert.That(
                    details,
                    Does.StartWith(
                        $"種別: {AuditEditor.LocalizationKeyAuditWindow.GetIssueKindDisplayName(kind)}" +
                        Environment.NewLine +
                        "説明: Unique Message"),
                    kind.ToString());
            }

            var invalidIssue = CreateIssue((AuditEditor.LocalizationKeyAuditIssueKind)int.MaxValue);
            var invalidArguments = new object[] { invalidIssue, 123 };
            Assert.That(
                InvokeStatic<bool>("TryGetIssueDetailsLength", invalidArguments),
                Is.False);
            Assert.That((int)invalidArguments[1], Is.Zero);
        }

        /// <summary>最後の詳細値に残る各種空白を除いた後も、実際のUTF-16長と事前計算値が一致します。</summary>
        [TestCase("   ", "説明:")]
        [TestCase("説明本体\r\n", "説明: 説明本体")]
        [TestCase("説明本体\u3000", "説明: 説明本体")]
        public void IssueDetailsLength_MatchesWhenLastValueHasTrailingWhitespace(
            string message,
            string expectedLastLine)
        {
            var issue = CreateMinimalIssue(
                AuditEditor.LocalizationKeyAuditIssueKind.AuditFailed,
                message);
            var details = InvokeStatic<string>("BuildIssueDetails", issue);
            var lengthArguments = new object[] { issue, 0 };
            var succeeded = InvokeStatic<bool>("TryGetIssueDetailsLength", lengthArguments);

            Assert.That(succeeded, Is.True);
            Assert.That((int)lengthArguments[1], Is.EqualTo(details.Length));
            Assert.That(details, Is.EqualTo(string.Join(Environment.NewLine, new[]
            {
                "種別: 監査失敗",
                expectedLastLine
            })));
        }

        /// <summary>
        /// 全問題種別を監査停止、必須ロケール網羅、静的参照、整合性の一つだけへ割り当てます。
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

        /// <summary>集計は問題種別の種類数ではなく、各区分に発行された問題の件数を数えます。</summary>
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

        /// <summary>空の一覧参照、空の要素、未知の種別は部分集計を返さず、空一覧だけを0件にします。</summary>
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

        /// <summary>表示上限を超える501件と処理上限ちょうどの100,000件も省略せず集計します。</summary>
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

        /// <summary>501件のページ変更は2ページ目先頭を選び、入力と区分件数を保ったまま表示状態だけを初期化します。</summary>
        [Test]
        public void SetIssuePage_SelectsCurrentPageStartAndCopyUsesOnlyThatPage()
        {
            var issues = Enumerable.Range(0, 501).Select(index => CreateMinimalIssue(
                AuditEditor.LocalizationKeyAuditIssueKind.AuditFailed,
                $"Issue-{index:D3}")).ToArray();
            var result = CreateResult(true, issues);
            var counts = AuditEditor.LocalizationKeyAuditWindow.CountIssueCategories(result.Issues);
            var previousClipboard = UnityEditor.EditorGUIUtility.systemCopyBuffer;
            var window = ScriptableObject.CreateInstance<AuditEditor.LocalizationKeyAuditWindow>();
            try
            {
                SetField(window, "result", result);
                SetField(window, "issueCategoryCounts", counts);
                SetField(window, "requiredLocalesText", "en, ja");
                SetField(window, "declaredAssetPathsText", "Assets/Localization");
                SetField(window, "scopeDescription", "ページ表示の走査範囲");
                SetField(window, "searchText", string.Empty);
                SetField(
                    window,
                    "issueCategory",
                    AuditEditor.LocalizationKeyAuditWindow.IssueCategoryFilter.All);
                Invoke(window, "RebuildVisibleIssues", true);

                Assert.That(GetField<List<int>>(window, "visibleIssueIndices"),
                    Is.EqualTo(Enumerable.Range(0, 501).ToArray()));
                Assert.That(GetField<int>(window, "issuePage"), Is.Zero);
                Assert.That(GetField<int>(window, "selectedIssueIndex"), Is.Zero);

                SetField(window, "issueScrollPosition", new Vector2(1f, 2f));
                SetField(window, "detailScrollPosition", new Vector2(3f, 4f));
                SetField(window, "interactionMessage", "以前のメッセージ");
                Invoke(window, "SetIssuePage", 1);

                Assert.That(GetField<int>(window, "issuePage"), Is.EqualTo(1));
                Assert.That(GetField<int>(window, "selectedIssueIndex"), Is.EqualTo(500));
                Assert.That(GetField<Vector2>(window, "issueScrollPosition"), Is.EqualTo(Vector2.zero));
                Assert.That(GetField<Vector2>(window, "detailScrollPosition"), Is.EqualTo(Vector2.zero));
                Assert.That(GetField<string>(window, "interactionMessage"), Is.Empty);
                Assert.That(GetField<string>(window, "requiredLocalesText"), Is.EqualTo("en, ja"));
                Assert.That(GetField<string>(window, "declaredAssetPathsText"), Is.EqualTo("Assets/Localization"));
                Assert.That(GetField<string>(window, "scopeDescription"), Is.EqualTo("ページ表示の走査範囲"));
                AssertCounts(
                    GetField<AuditEditor.LocalizationKeyAuditWindow.IssueCategoryCounts>(
                        window,
                        "issueCategoryCounts"),
                    501,
                    0,
                    0,
                    0);

                UnityEditor.EditorGUIUtility.systemCopyBuffer = "paged-copy-sentinel";
                Invoke(window, "CopyDisplayedIssues");

                Assert.That(
                    UnityEditor.EditorGUIUtility.systemCopyBuffer,
                    Does.StartWith(BuildExpectedClipboardHeader(true, true, 2, 2, 501, 501, 1, 501, 501)));
                StringAssert.Contains("説明: Issue-500", UnityEditor.EditorGUIUtility.systemCopyBuffer);
                StringAssert.DoesNotContain("説明: Issue-499", UnityEditor.EditorGUIUtility.systemCopyBuffer);
                Assert.That(
                    GetField<string>(window, "interactionMessage"),
                    Is.EqualTo("画面に表示中の問題 1 件をクリップボードへコピーしました。"));
                Assert.That(GetField<int>(window, "issuePage"), Is.EqualTo(1));
                Assert.That(GetField<int>(window, "selectedIssueIndex"), Is.EqualTo(500));

                SetField(window, "searchText", "Issue-500");
                SetField(window, "issueScrollPosition", new Vector2(5f, 6f));
                SetField(window, "detailScrollPosition", new Vector2(7f, 8f));
                SetField(window, "interactionMessage", "複製後の状態");
                Invoke(window, "RebuildVisibleIssues", true);

                Assert.That(GetField<List<int>>(window, "visibleIssueIndices"), Is.EqualTo(new[] { 500 }));
                Assert.That(GetField<int>(window, "issuePage"), Is.Zero);
                Assert.That(GetField<int>(window, "selectedIssueIndex"), Is.EqualTo(500));
                Assert.That(GetField<Vector2>(window, "issueScrollPosition"), Is.EqualTo(Vector2.zero));
                Assert.That(GetField<Vector2>(window, "detailScrollPosition"), Is.EqualTo(Vector2.zero));
                Assert.That(GetField<string>(window, "interactionMessage"), Is.Empty);
                AssertCounts(
                    GetField<AuditEditor.LocalizationKeyAuditWindow.IssueCategoryCounts>(
                        window,
                        "issueCategoryCounts"),
                    501,
                    0,
                    0,
                    0);
            }
            finally
            {
                UnityEditor.EditorGUIUtility.systemCopyBuffer = previousClipboard;
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        /// <summary>未完了結果の内訳は検索や区分で表示一覧が変わっても絞り込み前のままです。</summary>
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

        /// <summary>成功した未完了監査は古い集計を新しい結果の内訳へ置き換えます。</summary>
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
                SetField(window, "issuePage", 1);
                SetField(window, "issueScrollPosition", new Vector2(1f, 2f));
                SetField(window, "detailScrollPosition", new Vector2(3f, 4f));
                SetField(window, "requiredLocalesText", "en");
                SetField(window, "declaredAssetPathsText", "Packages/com.example/Runtime\nAssets/Scenes");
                SetField(window, "scopeDescription", "異なる論理ルート");
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
                Assert.That(GetField<int>(window, "issuePage"), Is.Zero);
                Assert.That(GetField<int>(window, "selectedIssueIndex"), Is.Zero);
                Assert.That(GetField<Vector2>(window, "issueScrollPosition"), Is.EqualTo(Vector2.zero));
                Assert.That(GetField<Vector2>(window, "detailScrollPosition"), Is.EqualTo(Vector2.zero));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        /// <summary>監査開始時の例外は古い結果と4区分集計を同時に破棄します。</summary>
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
                SetField(window, "issuePage", 1);
                SetField(window, "selectedIssueIndex", 0);
                SetField(window, "issueScrollPosition", new Vector2(1f, 2f));
                SetField(window, "detailScrollPosition", new Vector2(3f, 4f));
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
                Assert.That(GetField<int>(window, "issuePage"), Is.Zero);
                Assert.That(GetField<int>(window, "selectedIssueIndex"), Is.EqualTo(-1));
                Assert.That(GetField<Vector2>(window, "issueScrollPosition"), Is.EqualTo(Vector2.zero));
                Assert.That(GetField<Vector2>(window, "detailScrollPosition"), Is.EqualTo(Vector2.zero));
                Assert.That(
                    GetField<string>(window, "interactionMessage"),
                    Is.EqualTo("監査を開始できませんでした: LocalizationKeyAuditLimitException"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        /// <summary>現在の区分に一致する結果順、同内容の重複、2ページの表示範囲を見出し込みで固定します。</summary>
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
                    0,
                    out var clipboardText,
                    out var copiedIssueCount);

                var expectedHeader = BuildExpectedClipboardHeader(
                    true,
                    true,
                    1,
                    2,
                    1,
                    500,
                    500,
                    501,
                    1002);
                Assert.That(succeeded, Is.True);
                Assert.That(copiedIssueCount, Is.EqualTo(500));
                Assert.That(
                    clipboardText,
                    Does.StartWith(
                        expectedHeader +
                        Environment.NewLine +
                        Environment.NewLine +
                        "種別: 解決不能な静的参照" +
                        Environment.NewLine +
                        "説明: Static-000"));
                Assert.That(CountOccurrences(clipboardText, "説明: Duplicate"), Is.EqualTo(2));
                Assert.That(
                    clipboardText.IndexOf("説明: Static-000", StringComparison.Ordinal),
                    Is.LessThan(clipboardText.IndexOf("説明: Duplicate", StringComparison.Ordinal)));
                Assert.That(
                    clipboardText.IndexOf("説明: Duplicate", StringComparison.Ordinal),
                    Is.LessThan(clipboardText.IndexOf("説明: Static-003", StringComparison.Ordinal)));
                StringAssert.Contains("説明: Static-499", clipboardText);
                StringAssert.DoesNotContain("説明: Static-500", clipboardText);
                StringAssert.DoesNotContain("説明: Terminal-", clipboardText);
                Assert.That(clipboardText.Length, Is.LessThanOrEqualTo(
                    AuditEditor.LocalizationKeyAuditWindow.MaximumDisplayedIssueClipboardCharacters));
                Assert.That(
                    visibleIndices,
                    Is.EqualTo(Enumerable.Range(0, 501).Select(index => index * 2 + 1).ToArray()));

                var pageTwoSucceeded = AuditEditor.LocalizationKeyAuditWindow.TryBuildDisplayedIssuesClipboardText(
                    result,
                    visibleIndices,
                    1,
                    out var pageTwoText,
                    out var pageTwoCopiedCount);

                Assert.That(pageTwoSucceeded, Is.True);
                Assert.That(pageTwoCopiedCount, Is.EqualTo(1));
                Assert.That(
                    pageTwoText,
                    Does.StartWith(
                        BuildExpectedClipboardHeader(true, true, 2, 2, 501, 501, 1, 501, 1002) +
                        Environment.NewLine +
                        Environment.NewLine +
                        "種別: 解決不能な静的参照" +
                        Environment.NewLine +
                        "説明: Static-500"));
                StringAssert.DoesNotContain("説明: Static-499", pageTwoText);
                StringAssert.DoesNotContain("説明: Duplicate", pageTwoText);
                StringAssert.DoesNotContain("説明: Terminal-", pageTwoText);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        /// <summary>見出しと区切りを含むUTF-16長ちょうど1Miを許可し、1超過を切り詰めず拒否します。</summary>
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
                0,
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
                    Is.EqualTo("表示中の問題をクリップボードへコピーできませんでした。監査結果と絞り込み条件を再確認してください。"));
            }
            finally
            {
                UnityEditor.EditorGUIUtility.systemCopyBuffer = previousClipboard;
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        /// <summary>空参照、未知の種別、不正な添字は部分本文・件数を返さず、安全側で失敗します。</summary>
        [Test]
        public void TryBuildDisplayedIssuesClipboardText_RejectsInvalidSnapshotsAtomically()
        {
            var validIssue = CreateMinimalIssue(
                AuditEditor.LocalizationKeyAuditIssueKind.AuditFailed,
                "Valid");
            var validResult = CreateResult(true, new[] { validIssue });
            var twoIssueResult = CreateResult(true, new[] { validIssue, validIssue });
            var validPagedIssues = Enumerable.Repeat(validIssue, 501).ToArray();
            var invalidOffPageIssues = Enumerable.Repeat(validIssue, 501).ToArray();
            invalidOffPageIssues[500] = CreateMinimalIssue(
                (AuditEditor.LocalizationKeyAuditIssueKind)int.MaxValue,
                "別ページの不正な問題");
            var invalidOffPageVisibleIndices = Enumerable.Range(0, 501).ToArray();
            invalidOffPageVisibleIndices[500] = 501;

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
                    CreateMinimalIssue((AuditEditor.LocalizationKeyAuditIssueKind)int.MaxValue, "非表示の不正な問題")
                }),
                new[] { 0 });
            AssertClipboardBuildRejected(validResult, new[] { -1 });
            AssertClipboardBuildRejected(validResult, new[] { 1 });
            AssertClipboardBuildRejected(twoIssueResult, new[] { 0, 0 });
            AssertClipboardBuildRejected(twoIssueResult, new[] { 1, 0 });
            AssertClipboardBuildRejected(validResult, new[] { 0 }, -1);
            AssertClipboardBuildRejected(validResult, new[] { 0 }, 1);
            AssertClipboardBuildRejected(
                CreateResult(true, invalidOffPageIssues),
                Enumerable.Range(0, 501).ToArray(),
                0);
            AssertClipboardBuildRejected(
                CreateResult(true, validPagedIssues),
                invalidOffPageVisibleIndices,
                0);
        }

        /// <summary>処理上限ちょうどの200ページ目を受け付け、範囲外ページと件数1超過を要素参照前に拒否します。</summary>
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
                199,
                out var exactText,
                out var exactCopiedCount);

            Assert.That(exactSucceeded, Is.True);
            Assert.That(exactCopiedCount, Is.EqualTo(500));
            Assert.That(
                exactText,
                Does.StartWith(BuildExpectedClipboardHeader(
                    true,
                    true,
                    200,
                    200,
                    99501,
                    100000,
                    500,
                    100000,
                    100000)));
            AssertClipboardBuildRejected(result, exactVisibleIndices, 200);

            AssertClipboardBuildRejected(
                result,
                new CountOnlyReadOnlyList<int>(AuditEditor.LocalizationKeyAuditLimits.MaximumIssues + 1));

            SetResultIssuesForTest(
                result,
                new CountOnlyReadOnlyList<AuditEditor.LocalizationKeyAuditIssue>(
                    AuditEditor.LocalizationKeyAuditLimits.MaximumIssues + 1));
            AssertClipboardBuildRejected(result, new[] { 0 });
        }

        /// <summary>未完了でも現在ページだけをコピーし、絞り込み・選択・4区分集計を変更しません。</summary>
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
                Assert.That(GetField<int>(window, "issuePage"), Is.Zero);
                Assert.That(GetField<int>(window, "selectedIssueIndex"), Is.EqualTo(1));
                Assert.That(AuditEditor.LocalizationKeyAuditWindow.TryBuildDisplayedIssuesClipboardText(
                    result,
                    visibleIndices,
                    0,
                    out var expectedText,
                    out var expectedCount), Is.True);

                UnityEditor.EditorGUIUtility.systemCopyBuffer = "clipboard-success-sentinel";
                Invoke(window, "CopyDisplayedIssues");

                Assert.That(expectedCount, Is.EqualTo(1));
                Assert.That(UnityEditor.EditorGUIUtility.systemCopyBuffer, Is.EqualTo(expectedText));
                Assert.That(
                    expectedText,
                    Does.StartWith(BuildExpectedClipboardHeader(false, false, 1, 1, 1, 1, 1, 1, 2)));
                Assert.That(
                    GetField<string>(window, "interactionMessage"),
                    Is.EqualTo("画面に表示中の問題 1 件をクリップボードへコピーしました。"));
                Assert.That(GetField<AuditEditor.LocalizationKeyAuditResult>(window, "result"), Is.SameAs(result));
                Assert.That(visibleIndices, Is.EqualTo(new[] { 1 }));
                Assert.That(GetField<int>(window, "issuePage"), Is.Zero);
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

        /// <summary>不正な表示情報、結果消去、監査例外の後は古い問題をクリップボードへ再利用しません。</summary>
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
                    Is.EqualTo("表示中の問題をクリップボードへコピーできませんでした。監査結果と絞り込み条件を再確認してください。"));

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
                    Is.EqualTo("表示中の問題をクリップボードへコピーできませんでした。監査結果と絞り込み条件を再確認してください。"));
            }
            finally
            {
                UnityEditor.EditorGUIUtility.systemCopyBuffer = previousClipboard;
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        /// <summary>一括コピーと単一問題の詳細コピーが同じ日本語項目名と順序を維持します。</summary>
        [Test]
        public void BuildIssueDetails_RemainsExactAfterDisplayedCopyAddition()
        {
            var issue = CreateIssue(AuditEditor.LocalizationKeyAuditIssueKind.DanglingStaticReference);
            var details = InvokeStatic<string>("BuildIssueDetails", issue);

            Assert.That(details, Is.EqualTo(string.Join(Environment.NewLine, new[]
            {
                "種別: 解決不能な静的参照",
                "説明: Unique Message",
                "アセット: Assets/Source.prefab",
                "関連アセット: Assets/Related.asset",
                "コレクション: Collection Display",
                "コレクション識別子（GUID）: 11111111-2222-3333-4444-555555555555",
                "ロケール: ja-JP",
                "項目キー: Entry Key",
                "項目識別子: 123456"
            })));
        }

        /// <summary>検索対象の全項目を埋めた問題を作ります。</summary>
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

        /// <summary>クリップボード本文の長さと順序を制御しやすい最小問題を作ります。</summary>
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

        /// <summary>クリップボード見出しのページ・範囲を含む8行を実装契約どおり組み立てます。</summary>
        private static string BuildExpectedClipboardHeader(
            bool isComplete,
            bool isCoverageComplete,
            int displayedPage,
            int pageCount,
            int displayedRangeStart,
            int displayedRangeEnd,
            int displayedCount,
            int filteredCount,
            int totalCount)
        {
            return string.Join(Environment.NewLine, new[]
            {
                "ローカライズキー監査 - 表示中の問題",
                $"監査結果: {(isComplete ? "完了" : "未完了")}",
                $"静的参照網羅: {(isCoverageComplete ? "完了" : "未完了")}",
                $"表示ページ: {displayedPage} / {pageCount}",
                $"表示範囲: {displayedRangeStart}-{displayedRangeEnd}",
                $"表示件数: {displayedCount}",
                $"絞り込み後の件数: {filteredCount}",
                $"問題総数: {totalCount}"
            });
        }

        /// <summary>33件の問題へ説明長を分配し、指定したUTF-16長ちょうどの候補結果を作ります。</summary>
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
                0,
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

            Assert.That(remainingCharacters, Is.Zero, "試験データの収容量");
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

        /// <summary>失敗時の出力本文と件数が必ず初期値へ戻ることを共通検証します。</summary>
        private static void AssertClipboardBuildRejected(
            AuditEditor.LocalizationKeyAuditResult result,
            IReadOnlyList<int> visibleIndices,
            int issuePage = 0)
        {
            var succeeded = AuditEditor.LocalizationKeyAuditWindow.TryBuildDisplayedIssuesClipboardText(
                result,
                visibleIndices,
                issuePage,
                out var clipboardText,
                out var copiedIssueCount);

            Assert.That(succeeded, Is.False);
            Assert.That(clipboardText, Is.Empty);
            Assert.That(copiedIssueCount, Is.Zero);
        }

        /// <summary>重複した本文断片の出現回数を序数比較で数えます。</summary>
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

        /// <summary>巨大配列を二重生成せず、結果の問題件数が上限を1超える状態だけを注入します。</summary>
        private static void SetResultIssuesForTest(
            AuditEditor.LocalizationKeyAuditResult result,
            IReadOnlyList<AuditEditor.LocalizationKeyAuditIssue> issues)
        {
            var field = typeof(AuditEditor.LocalizationKeyAuditResult).GetField(
                "<Issues>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "LocalizationKeyAuditResult.Issuesの裏側フィールド");
            field.SetValue(result, issues);
        }

        /// <summary>結果消去前の状態へ入れる最小の完了結果を作ります。</summary>
        private static AuditEditor.LocalizationKeyAuditResult CreateEmptyResult()
        {
            return CreateResult(
                true,
                Array.Empty<AuditEditor.LocalizationKeyAuditIssue>());
        }

        /// <summary>画面状態へ入れる最小結果を作ります。</summary>
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
                    isCoverageComplete ? string.Empty : "走査は未完了"),
                Array.Empty<string>(),
                Array.Empty<AuditEditor.LocalizationKeyAuditCollectionSnapshot>(),
                issues,
                0);
        }

        /// <summary>4区分と合計の正確な件数を検証します。</summary>
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

        /// <summary>必須の非公開インスタンス項目を取得します。</summary>
        private static T GetField<T>(AuditEditor.LocalizationKeyAuditWindow window, string fieldName)
        {
            var field = GetFieldInfo(fieldName);
            return (T)field.GetValue(window);
        }

        /// <summary>必須の非公開インスタンス項目へ値を設定します。</summary>
        private static void SetField<T>(
            AuditEditor.LocalizationKeyAuditWindow window,
            string fieldName,
            T value)
        {
            GetFieldInfo(fieldName).SetValue(window, value);
        }

        /// <summary>必須の非公開インスタンス項目情報を取得します。</summary>
        private static FieldInfo GetFieldInfo(string fieldName)
        {
            var field = typeof(AuditEditor.LocalizationKeyAuditWindow).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return field;
        }

        /// <summary>必須の非公開静的項目を取得します。</summary>
        private static T GetStaticField<T>(string fieldName)
        {
            var field = typeof(AuditEditor.LocalizationKeyAuditWindow).GetField(
                fieldName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(null);
        }

        /// <summary>非公開インスタンス関数を指定引数で呼び出します。</summary>
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

        /// <summary>必須の非公開静的関数を呼び、戻り値を取得します。</summary>
        private static T InvokeStatic<T>(string methodName, params object[] arguments)
        {
            var method = typeof(AuditEditor.LocalizationKeyAuditWindow).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return (T)method.Invoke(null, arguments);
        }

        /// <summary>同じ要素を指定件数だけ返し、処理上限の試験データが使うメモリを抑えます。</summary>
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

        /// <summary>0から件数未満までを追加確保なしで返し、表示件数の上限ちょうどを検証します。</summary>
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

        /// <summary>件数上限の確認後に要素へ触らないことを検証する境界一覧です。</summary>
        private sealed class CountOnlyReadOnlyList<T> : IReadOnlyList<T>
        {
            internal CountOnlyReadOnlyList(int count)
            {
                Count = count;
            }

            public int Count { get; }

            public T this[int index] => throw new InvalidOperationException("件数上限の確認より後へ進みました。");

            public IEnumerator<T> GetEnumerator()
            {
                throw new InvalidOperationException("件数上限の確認より後へ進みました。");
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
