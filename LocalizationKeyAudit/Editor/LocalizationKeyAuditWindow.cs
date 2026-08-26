// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 必須 Locale の direct entry と宣言済み asset scope の static reference coverage を手動表示します。
    /// </summary>
    internal sealed class LocalizationKeyAuditWindow : EditorWindow
    {
        /// <summary>問題一覧へ描画する最大行数です。</summary>
        internal const int MaximumDisplayedIssues = 500;

        /// <summary>表示中の問題を一括copyするときのUTF-16 code unit上限です。</summary>
        internal const int MaximumDisplayedIssueClipboardCharacters = 1_048_576;

        /// <summary>問題種別を用途別に絞り込む表示区分です。</summary>
        internal enum IssueCategoryFilter
        {
            /// <summary>全問題を表示します。</summary>
            All,

            /// <summary>監査を完了できなかった問題です。</summary>
            Terminal,

            /// <summary>必須 Locale の direct table/value coverage 問題です。</summary>
            RequiredLocaleCoverage,

            /// <summary>宣言済み scope の static reference 問題です。</summary>
            StaticReferences,

            /// <summary>table、entry、Locale identity の整合性問題です。</summary>
            Integrity
        }

        /// <summary>filter前の監査結果へ発行された問題件数を4区分で保持します。</summary>
        internal readonly struct IssueCategoryCounts
        {
            /// <summary>4区分の件数を固定します。</summary>
            internal IssueCategoryCounts(
                int terminal,
                int requiredLocaleCoverage,
                int staticReferences,
                int integrity)
            {
                Terminal = terminal;
                RequiredLocaleCoverage = requiredLocaleCoverage;
                StaticReferences = staticReferences;
                Integrity = integrity;
            }

            /// <summary>監査を完了できなかった問題件数です。</summary>
            internal int Terminal { get; }

            /// <summary>必須Locale coverageの問題件数です。</summary>
            internal int RequiredLocaleCoverage { get; }

            /// <summary>static referenceの問題件数です。</summary>
            internal int StaticReferences { get; }

            /// <summary>table、entry、Locale identityの整合性問題件数です。</summary>
            internal int Integrity { get; }

            /// <summary>4区分の合計件数です。</summary>
            internal int Total => Terminal + RequiredLocaleCoverage + StaticReferences + Integrity;
        }

        /// <summary>カンマまたは改行区切りの必須 Locale identifiers です。</summary>
        [SerializeField] private string requiredLocalesText = string.Empty;

        /// <summary>改行区切りのAssetsまたは1 registered Package static-reference scopeです。</summary>
        [SerializeField] private string declaredAssetPathsText = "Assets";

        /// <summary>結果へ残す coverage scope の説明です。</summary>
        [SerializeField] private string scopeDescription = "Assets text .unity/.prefab/.asset の GUID + key ID direct references";

        /// <summary>問題の検索語です。</summary>
        [SerializeField] private string searchText = string.Empty;

        /// <summary>問題の表示区分です。</summary>
        [SerializeField] private IssueCategoryFilter issueCategory = IssueCategoryFilter.All;

        /// <summary>問題一覧の scroll 位置です。</summary>
        [SerializeField] private Vector2 issueScrollPosition;

        /// <summary>最小Windowで全sectionへ到達する外側のscroll位置です。</summary>
        [SerializeField] private Vector2 windowScrollPosition;

        /// <summary>詳細欄の scroll 位置です。</summary>
        [SerializeField] private Vector2 detailScrollPosition;

        /// <summary>最後の手動監査結果です。</summary>
        private LocalizationKeyAuditResult result;

        /// <summary>最後の監査結果全体から1回だけ集計したfilter前の問題件数です。</summary>
        private IssueCategoryCounts issueCategoryCounts;

        /// <summary>filter に一致する問題 index です。</summary>
        private readonly List<int> visibleIssueIndices = new List<int>();

        /// <summary>現在選択中の問題 index です。</summary>
        private int selectedIssueIndex = -1;

        /// <summary>Window 操作または予期しない例外の案内です。</summary>
        private string interactionMessage = string.Empty;

        /// <summary>一覧行へ使う折り返し style です。</summary>
        private GUIStyle issueRowStyle;

        /// <summary>補足文へ使う折り返し style です。</summary>
        private GUIStyle wrappedMiniLabelStyle;

        /// <summary>Window を開き、実用上の最小 size を設定します。</summary>
        internal static void Open()
        {
            var window = GetWindow<LocalizationKeyAuditWindow>();
            window.titleContent = new GUIContent("Localization Key Audit");
            window.minSize = new Vector2(820f, 640f);
            window.Show();
        }

        /// <summary>domain reload 後にも最小 size だけを復元し、自動監査は行いません。</summary>
        private void OnEnable()
        {
            minSize = new Vector2(820f, 640f);
        }

        /// <summary>設定、手動操作、summary、問題一覧、詳細を順に描画します。</summary>
        private void OnGUI()
        {
            EnsureStyles();
            windowScrollPosition = EditorGUILayout.BeginScrollView(windowScrollPosition);
            EditorGUILayout.HelpBox(
                "Editor-only の手動・読み取り専用・advisory 監査です。必須 Locale の direct value と、宣言済み Assets または1つのregistered Package scope で認識した GUID + key ID 参照だけを扱います。fallback や runtime の最終翻訳を保証せず、参照が見つからない key を『未使用』とは断定しません。",
                MessageType.Info);

            DrawRequestSettings();
            DrawToolbar();
            DrawStatus();

            if (result == null)
            {
                EditorGUILayout.HelpBox(
                    "Required Locales と asset scope を確認して Audit を押してください。asset は自動変更されず、監査は build を止めません。",
                    MessageType.None);
            }
            else
            {
                EnsureSelection();
                var issueHeight = Mathf.Max(140f, Mathf.Min(280f, position.height * 0.26f));
                DrawIssues(issueHeight);
                EditorGUILayout.Space(6f);
                DrawDetails(Mathf.Max(110f, position.height - issueHeight - 430f));
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>必須Locale、scope説明、Assets/Packages pathを明示入力させます。</summary>
        private void DrawRequestSettings()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Audit Request", EditorStyles.boldLabel);
                requiredLocalesText = EditorGUILayout.TextField(
                    new GUIContent("Required Locales", "例: en, ja。カンマまたは改行で区切ります。"),
                    requiredLocalesText);
                scopeDescription = EditorGUILayout.TextField(
                    new GUIContent("Scope Description", "結果へそのまま残る、人が確認できる走査範囲の説明です。"),
                    scopeDescription);
                EditorGUILayout.LabelField(
                    new GUIContent(
                        "Declared Asset Paths",
                        "改行区切り。同じroot内で複数指定できます。1回の監査ではAssets、または1つのPackages/<registered-package-name>だけをrootにします。"));
                declaredAssetPathsText = EditorGUILayout.TextArea(
                    declaredAssetPathsText,
                    GUILayout.MinHeight(42f),
                    GUILayout.MaxHeight(72f));
            }
        }

        /// <summary>検索、区分 filter、Audit、Clear を描画します。</summary>
        private void DrawToolbar()
        {
            var filterChanged = false;
            var auditRequested = false;
            var clearRequested = false;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                searchText = EditorGUILayout.TextField(
                    "Search",
                    searchText,
                    EditorStyles.toolbarSearchField,
                    GUILayout.MinWidth(190f));
                GUILayout.Label("Category", GUILayout.Width(53f));
                issueCategory = (IssueCategoryFilter)EditorGUILayout.EnumPopup(
                    issueCategory,
                    EditorStyles.toolbarPopup,
                    GUILayout.Width(158f));
                filterChanged = EditorGUI.EndChangeCheck();

                GUILayout.FlexibleSpace();
                auditRequested = GUILayout.Button("Audit", EditorStyles.toolbarButton, GUILayout.Width(64f));
                clearRequested = GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(56f));
            }

            if (filterChanged)
            {
                RebuildVisibleIssues(true);
            }

            if (auditRequested)
            {
                RunAudit();
            }

            if (clearRequested)
            {
                ClearResult();
            }
        }

        /// <summary>監査完了性、件数、coverage 境界、表示上限を描画します。</summary>
        private void DrawStatus()
        {
            if (!string.IsNullOrEmpty(interactionMessage))
            {
                EditorGUILayout.HelpBox(interactionMessage, MessageType.None);
            }

            if (result == null)
            {
                EditorGUILayout.LabelField($"表示上限: 問題 {MaximumDisplayedIssues} 件", EditorStyles.miniLabel);
                return;
            }

            var completion = result.IsComplete ? "Complete" : "Incomplete";
            EditorGUILayout.LabelField(
                $"{completion} / Locales {result.LocaleIdentifiers.Count} / Collections {result.Collections.Count} / Orphan Tables {result.OrphanLocaleTables.Count} / Issues {result.Issues.Count} / Edges {result.GraphEdgeCount}",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"Issue Categories (unfiltered result): Terminal {issueCategoryCounts.Terminal} / Required Locale Coverage {issueCategoryCounts.RequiredLocaleCoverage} / Static References {issueCategoryCounts.StaticReferences} / Integrity {issueCategoryCounts.Integrity}",
                wrappedMiniLabelStyle);
            EditorGUILayout.LabelField(
                $"Static coverage: {(result.Coverage.IsComplete ? "Complete" : "Incomplete")} / References {result.Coverage.RecognizedReferences.Count} / Declared Paths {result.Coverage.DeclaredAssetPaths.Count} / Filtered Issues {visibleIssueIndices.Count}",
                wrappedMiniLabelStyle);
            EditorGUILayout.LabelField(
                $"Scope: {result.Coverage.ScopeDescription}",
                wrappedMiniLabelStyle);
            if (!result.Coverage.IsComplete)
            {
                EditorGUILayout.HelpBox(result.Coverage.IncompleteReason, MessageType.Warning);
            }

            EditorGUILayout.LabelField(
                $"表示上限: 問題 {MaximumDisplayedIssues} 件。超過分は Search と Category で絞り込んでください。",
                EditorStyles.miniLabel);
        }

        /// <summary>決定論的な result 順を維持した問題一覧を最大 500 件描画します。</summary>
        private void DrawIssues(float height)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var displayedCount = Math.Min(visibleIssueIndices.Count, MaximumDisplayedIssues);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Issues ({visibleIssueIndices.Count})", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(displayedCount == 0))
                    {
                        if (GUILayout.Button("Copy Displayed", GUILayout.Width(112f)))
                        {
                            CopyDisplayedIssues();
                        }
                    }
                }

                issueScrollPosition = EditorGUILayout.BeginScrollView(issueScrollPosition, GUILayout.Height(height));
                if (displayedCount == 0)
                {
                    EditorGUILayout.LabelField("現在の filter に一致する問題はありません。", wrappedMiniLabelStyle);
                }

                for (var visibleIndex = 0; visibleIndex < displayedCount; visibleIndex++)
                {
                    var resultIndex = visibleIssueIndices[visibleIndex];
                    var issue = result.Issues[resultIndex];
                    var selected = resultIndex == selectedIssueIndex;
                    var label = BuildIssueLabel(issue);
                    if (GUILayout.Toggle(selected, label, issueRowStyle) && !selected)
                    {
                        selectedIssueIndex = resultIndex;
                        detailScrollPosition = Vector2.zero;
                        interactionMessage = string.Empty;
                    }
                }

                if (visibleIssueIndices.Count > MaximumDisplayedIssues)
                {
                    EditorGUILayout.HelpBox(
                        $"先頭 {MaximumDisplayedIssues} 件だけを表示しています。",
                        MessageType.Warning);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        /// <summary>選択問題の identity と安全な interaction を表示します。</summary>
        private void DrawDetails(float height)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Details", EditorStyles.boldLabel);
                if (!IsIssueIndexValid(selectedIssueIndex))
                {
                    EditorGUILayout.LabelField("問題を選択してください。", wrappedMiniLabelStyle);
                    return;
                }

                var issue = result.Issues[selectedIssueIndex];
                detailScrollPosition = EditorGUILayout.BeginScrollView(detailScrollPosition, GUILayout.Height(height));
                DrawDetailRow("Kind", issue.Kind.ToString());
                DrawDetailRow("Message", issue.Message);
                DrawDetailRow("Asset", issue.AssetPath);
                DrawDetailRow("Related", issue.RelatedAssetPath);
                DrawDetailRow("Collection", issue.CollectionName);
                DrawDetailRow("Collection GUID", issue.CollectionGuid == Guid.Empty ? string.Empty : issue.CollectionGuid.ToString("D"));
                DrawDetailRow("Locale", issue.LocaleIdentifier);
                DrawDetailRow("Entry Key", issue.EntryKey);
                DrawDetailRow("Entry ID", issue.EntryId == 0 ? string.Empty : issue.EntryId.ToString());
                EditorGUILayout.EndScrollView();

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(issue.AssetPath)))
                    {
                        if (GUILayout.Button("Copy Asset Path", GUILayout.Width(112f)))
                        {
                            CopyPath(issue.AssetPath);
                        }
                    }

                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(issue.RelatedAssetPath)))
                    {
                        if (GUILayout.Button("Copy Related Path", GUILayout.Width(124f)))
                        {
                            CopyPath(issue.RelatedAssetPath);
                        }
                    }

                    if (GUILayout.Button("Copy Details", GUILayout.Width(96f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = BuildIssueDetails(issue);
                        interactionMessage = "選択した問題の詳細を clipboard へ copy しました。";
                    }
                }
            }
        }

        /// <summary>現在の入力から coverage と監査結果を 1 回だけ取得します。</summary>
        private void RunAudit()
        {
            interactionMessage = string.Empty;
            try
            {
                var nextResult = LocalizationKeyAuditService.Audit(
                    ParseRequiredLocales(requiredLocalesText),
                    scopeDescription,
                    ParseDeclaredAssetPaths(declaredAssetPathsText));
                var nextIssueCategoryCounts = CountIssueCategories(nextResult.Issues);
                result = nextResult;
                issueCategoryCounts = nextIssueCategoryCounts;
                RebuildVisibleIssues(true);
                issueScrollPosition = Vector2.zero;
                detailScrollPosition = Vector2.zero;
            }
            catch (Exception exception)
            {
                result = null;
                issueCategoryCounts = default;
                visibleIssueIndices.Clear();
                selectedIssueIndex = -1;
                interactionMessage = $"監査を開始できませんでした: {exception.GetType().Name}";
            }
        }

        /// <summary>入力は維持したまま結果、選択、操作案内だけを消します。</summary>
        private void ClearResult()
        {
            result = null;
            issueCategoryCounts = default;
            visibleIssueIndices.Clear();
            selectedIssueIndex = -1;
            issueScrollPosition = Vector2.zero;
            detailScrollPosition = Vector2.zero;
            windowScrollPosition = Vector2.zero;
            interactionMessage = string.Empty;
        }

        /// <summary>現在のfilterで画面に表示する問題だけを検証後に1回でclipboardへcopyします。</summary>
        private void CopyDisplayedIssues()
        {
            if (!TryBuildDisplayedIssuesClipboardText(
                    result,
                    visibleIssueIndices,
                    out var clipboardText,
                    out var copiedIssueCount))
            {
                interactionMessage =
                    "表示中の問題をclipboardへcopyできませんでした。監査結果とfilterを再確認してください。";
                return;
            }

            EditorGUIUtility.systemCopyBuffer = clipboardText;
            interactionMessage = $"画面に表示中の問題 {copiedIssueCount} 件をclipboardへcopyしました。";
        }

        /// <summary>必須 Locale のカンマ・semicolon・改行区切りを順序保持で解析します。</summary>
        internal static IReadOnlyList<string> ParseRequiredLocales(string text)
        {
            return ParseTokens(
                text,
                new[] { ',', ';', '\r', '\n' },
                LocalizationKeyAuditLimits.MaximumRequiredLocales,
                "required Locale",
                true);
        }

        /// <summary>asset scopeの改行区切りを順序保持で解析します。</summary>
        internal static IReadOnlyList<string> ParseDeclaredAssetPaths(string text)
        {
            return ParseTokens(
                text,
                new[] { '\r', '\n' },
                LocalizationKeyAuditLimits.MaximumDeclaredAssetPaths,
                "declared asset path",
                false);
        }

        /// <summary>検索語と区分 filter が問題へ一致するかを pure に判定します。</summary>
        internal static bool MatchesFilter(
            LocalizationKeyAuditIssue issue,
            string candidateSearchText,
            IssueCategoryFilter category)
        {
            return MatchesNormalizedFilter(issue, NormalizeSearchText(candidateSearchText), category);
        }

        /// <summary>検索語を1回だけbounded trimし、issue loop内の再allocationを防ぎます。</summary>
        internal static string NormalizeSearchText(string candidateSearchText)
        {
            var value = candidateSearchText ?? string.Empty;
            if (value.Length > LocalizationKeyAuditLimits.MaximumTextCharacters)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"検索語が文字数上限 {LocalizationKeyAuditLimits.MaximumTextCharacters} を超えています。");
            }

            return value.Trim();
        }

        /// <summary>正規化済み検索語と区分filterをallocationなしで照合します。</summary>
        private static bool MatchesNormalizedFilter(
            LocalizationKeyAuditIssue issue,
            string normalizedSearchText,
            IssueCategoryFilter category)
        {
            if (issue == null || !MatchesCategory(issue.Kind, category))
            {
                return false;
            }

            if (normalizedSearchText.Length == 0)
            {
                return true;
            }

            return ContainsOrdinalIgnoreCase(issue.Kind.ToString(), normalizedSearchText) ||
                ContainsOrdinalIgnoreCase(issue.Message, normalizedSearchText) ||
                ContainsOrdinalIgnoreCase(issue.AssetPath, normalizedSearchText) ||
                ContainsOrdinalIgnoreCase(issue.RelatedAssetPath, normalizedSearchText) ||
                ContainsOrdinalIgnoreCase(issue.CollectionName, normalizedSearchText) ||
                ContainsOrdinalIgnoreCase(issue.CollectionGuid == Guid.Empty ? string.Empty : issue.CollectionGuid.ToString("D"), normalizedSearchText) ||
                ContainsOrdinalIgnoreCase(issue.LocaleIdentifier, normalizedSearchText) ||
                ContainsOrdinalIgnoreCase(issue.EntryKey, normalizedSearchText) ||
                ContainsOrdinalIgnoreCase(issue.EntryId == 0 ? string.Empty : issue.EntryId.ToString(), normalizedSearchText);
        }

        /// <summary>現在の filter に一致する問題 index を再構築します。</summary>
        private void RebuildVisibleIssues(bool resetSelection)
        {
            visibleIssueIndices.Clear();
            string normalizedSearchText;
            try
            {
                normalizedSearchText = NormalizeSearchText(searchText);
            }
            catch (LocalizationKeyAuditLimitException exception)
            {
                selectedIssueIndex = -1;
                interactionMessage = exception.Message;
                return;
            }

            if (result != null)
            {
                for (var index = 0; index < result.Issues.Count; index++)
                {
                    if (MatchesNormalizedFilter(result.Issues[index], normalizedSearchText, issueCategory))
                    {
                        visibleIssueIndices.Add(index);
                    }
                }
            }

            if (resetSelection || !visibleIssueIndices.Contains(selectedIssueIndex))
            {
                selectedIssueIndex = visibleIssueIndices.Count == 0 ? -1 : visibleIssueIndices[0];
            }
        }

        /// <summary>選択が現在 result 内かを維持します。</summary>
        private void EnsureSelection()
        {
            if (!IsIssueIndexValid(selectedIssueIndex) || !visibleIssueIndices.Contains(selectedIssueIndex))
            {
                selectedIssueIndex = visibleIssueIndices.Count == 0 ? -1 : visibleIssueIndices[0];
            }
        }

        /// <summary>filter前の問題一覧を1回だけ走査し、4区分の件数を返します。</summary>
        internal static IssueCategoryCounts CountIssueCategories(
            IReadOnlyList<LocalizationKeyAuditIssue> issues)
        {
            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }

            var terminal = 0;
            var requiredLocaleCoverage = 0;
            var staticReferences = 0;
            var integrity = 0;
            for (var index = 0; index < issues.Count; index++)
            {
                var issue = issues[index];
                if (issue == null)
                {
                    throw new ArgumentException("問題一覧にnull要素が含まれています。", nameof(issues));
                }

                switch (ClassifyIssueKind(issue.Kind))
                {
                    case IssueCategoryFilter.Terminal:
                        terminal++;
                        break;
                    case IssueCategoryFilter.RequiredLocaleCoverage:
                        requiredLocaleCoverage++;
                        break;
                    case IssueCategoryFilter.StaticReferences:
                        staticReferences++;
                        break;
                    case IssueCategoryFilter.Integrity:
                        integrity++;
                        break;
                    default:
                        throw new InvalidOperationException("問題区分を集計できませんでした。");
                }
            }

            return new IssueCategoryCounts(
                terminal,
                requiredLocaleCoverage,
                staticReferences,
                integrity);
        }

        /// <summary>問題kindをfilterと集計で共用する1つの表示区分へ割り当てます。</summary>
        internal static IssueCategoryFilter ClassifyIssueKind(LocalizationKeyAuditIssueKind kind)
        {
            switch (kind)
            {
                case LocalizationKeyAuditIssueKind.ReadOnlyGuaranteeUnavailable:
                case LocalizationKeyAuditIssueKind.InvalidConfiguration:
                case LocalizationKeyAuditIssueKind.LimitExceeded:
                case LocalizationKeyAuditIssueKind.AuditFailed:
                    return IssueCategoryFilter.Terminal;
                case LocalizationKeyAuditIssueKind.RequiredLocaleNotConfigured:
                case LocalizationKeyAuditIssueKind.MissingLocaleTable:
                case LocalizationKeyAuditIssueKind.MissingDirectEntry:
                case LocalizationKeyAuditIssueKind.EmptyDirectValue:
                    return IssueCategoryFilter.RequiredLocaleCoverage;
                case LocalizationKeyAuditIssueKind.DanglingStaticReference:
                case LocalizationKeyAuditIssueKind.NoStaticReferenceFoundWithinDeclaredScope:
                case LocalizationKeyAuditIssueKind.StaticReferenceCoverageIncomplete:
                    return IssueCategoryFilter.StaticReferences;
                case LocalizationKeyAuditIssueKind.DuplicateCollectionName:
                case LocalizationKeyAuditIssueKind.DuplicateCollectionGuid:
                case LocalizationKeyAuditIssueKind.DuplicateSharedEntryId:
                case LocalizationKeyAuditIssueKind.DuplicateSharedEntryKey:
                case LocalizationKeyAuditIssueKind.DuplicateLocaleTable:
                case LocalizationKeyAuditIssueKind.DuplicateLocalizedEntryId:
                case LocalizationKeyAuditIssueKind.OrphanedLocalizedEntry:
                case LocalizationKeyAuditIssueKind.OrphanedLocaleTable:
                case LocalizationKeyAuditIssueKind.OrphanedSharedTableData:
                case LocalizationKeyAuditIssueKind.DuplicateLocaleIdentifier:
                    return IssueCategoryFilter.Integrity;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "未分類の問題kindです。");
            }
        }

        /// <summary>問題kindが指定した表示区分へ一致するかを判定します。</summary>
        private static bool MatchesCategory(LocalizationKeyAuditIssueKind kind, IssueCategoryFilter category)
        {
            var issueCategory = ClassifyIssueKind(kind);

            return category == IssueCategoryFilter.All || category == issueCategory;
        }

        /// <summary>区切り文字で分け、空白だけの token を除きます。</summary>
        private static IReadOnlyList<string> ParseTokens(
            string text,
            char[] separators,
            int maximumCount,
            string valueKind,
            bool trimTokens)
        {
            var values = new List<string>();
            var source = text ?? string.Empty;
            var tokenStart = 0;
            for (var index = 0; index <= source.Length; index++)
            {
                if (index < source.Length && Array.IndexOf(separators, source[index]) < 0)
                {
                    continue;
                }

                var rawStart = tokenStart;
                var rawEnd = index;
                var start = rawStart;
                var end = rawEnd;
                while (start < end && char.IsWhiteSpace(source[start]))
                {
                    start++;
                }

                while (end > start && char.IsWhiteSpace(source[end - 1]))
                {
                    end--;
                }

                if (end > start)
                {
                    if (!trimTokens)
                    {
                        start = rawStart;
                        end = rawEnd;
                    }

                    if (end - start > LocalizationKeyAuditLimits.MaximumTextCharacters)
                    {
                        throw new LocalizationKeyAuditLimitException(
                            $"{valueKind}が文字数上限 {LocalizationKeyAuditLimits.MaximumTextCharacters} を超えています。");
                    }

                    if (values.Count >= maximumCount)
                    {
                        throw new LocalizationKeyAuditLimitException(
                            $"{valueKind}数が上限 {maximumCount} 件を超えています。");
                    }

                    values.Add(source.Substring(start, end - start));
                }

                tokenStart = index + 1;
            }

            return values;
        }

        /// <summary>問題一覧向けの 1 行表示を作ります。</summary>
        private static string BuildIssueLabel(LocalizationKeyAuditIssue issue)
        {
            var identity = !string.IsNullOrEmpty(issue.EntryKey)
                ? issue.EntryKey
                : !string.IsNullOrEmpty(issue.CollectionName)
                    ? issue.CollectionName
                    : !string.IsNullOrEmpty(issue.LocaleIdentifier)
                        ? issue.LocaleIdentifier
                        : !string.IsNullOrEmpty(issue.AssetPath)
                            ? issue.AssetPath
                            : issue.RelatedAssetPath;
            return string.IsNullOrEmpty(identity)
                ? issue.Kind.ToString()
                : $"{issue.Kind}  |  {identity}";
        }

        /// <summary>clipboard 用の全詳細を組み立てます。</summary>
        private static string BuildIssueDetails(LocalizationKeyAuditIssue issue)
        {
            var builder = new StringBuilder();
            AppendDetail(builder, "Kind", issue.Kind.ToString());
            AppendDetail(builder, "Message", issue.Message);
            AppendDetail(builder, "Asset", issue.AssetPath);
            AppendDetail(builder, "Related", issue.RelatedAssetPath);
            AppendDetail(builder, "Collection", issue.CollectionName);
            AppendDetail(builder, "Collection GUID", issue.CollectionGuid == Guid.Empty ? string.Empty : issue.CollectionGuid.ToString("D"));
            AppendDetail(builder, "Locale", issue.LocaleIdentifier);
            AppendDetail(builder, "Entry Key", issue.EntryKey);
            AppendDetail(builder, "Entry ID", issue.EntryId == 0 ? string.Empty : issue.EntryId.ToString());
            return builder.ToString().TrimEnd();
        }

        /// <summary>
        /// filter済みindexの先頭500件を、完了状態と件数header付きのbounded clipboard本文へ変換します。
        /// </summary>
        internal static bool TryBuildDisplayedIssuesClipboardText(
            LocalizationKeyAuditResult candidateResult,
            IReadOnlyList<int> candidateVisibleIssueIndices,
            out string clipboardText,
            out int copiedIssueCount)
        {
            clipboardText = string.Empty;
            copiedIssueCount = 0;
            if (candidateResult == null ||
                candidateResult.Issues == null ||
                candidateVisibleIssueIndices == null ||
                candidateResult.Issues.Count == 0 ||
                candidateResult.Issues.Count > LocalizationKeyAuditLimits.MaximumIssues ||
                candidateVisibleIssueIndices.Count == 0 ||
                candidateVisibleIssueIndices.Count > LocalizationKeyAuditLimits.MaximumIssues)
            {
                return false;
            }

            for (var issueIndex = 0; issueIndex < candidateResult.Issues.Count; issueIndex++)
            {
                var issue = candidateResult.Issues[issueIndex];
                if (issue == null || !IsKnownIssueKind(issue.Kind))
                {
                    return false;
                }
            }

            var previousResultIndex = -1;
            for (var visibleIndex = 0; visibleIndex < candidateVisibleIssueIndices.Count; visibleIndex++)
            {
                var resultIndex = candidateVisibleIssueIndices[visibleIndex];
                if (resultIndex < 0 ||
                    resultIndex >= candidateResult.Issues.Count ||
                    resultIndex <= previousResultIndex)
                {
                    return false;
                }

                previousResultIndex = resultIndex;
            }

            var displayedCount = Math.Min(candidateVisibleIssueIndices.Count, MaximumDisplayedIssues);
            var header = BuildDisplayedIssuesClipboardHeader(
                candidateResult,
                displayedCount,
                candidateVisibleIssueIndices.Count);
            var blockSeparator = Environment.NewLine + Environment.NewLine;
            long requiredLength = header.Length;
            var detailLengths = new int[displayedCount];
            for (var displayedIndex = 0; displayedIndex < displayedCount; displayedIndex++)
            {
                var issue = candidateResult.Issues[candidateVisibleIssueIndices[displayedIndex]];
                if (!TryGetIssueDetailsLength(issue, out var detailLength))
                {
                    return false;
                }

                detailLengths[displayedIndex] = detailLength;
                requiredLength += blockSeparator.Length + detailLength;
                if (requiredLength > MaximumDisplayedIssueClipboardCharacters)
                {
                    return false;
                }
            }

            var builder = new StringBuilder((int)requiredLength);
            builder.Append(header);
            for (var displayedIndex = 0; displayedIndex < displayedCount; displayedIndex++)
            {
                var issue = candidateResult.Issues[candidateVisibleIssueIndices[displayedIndex]];
                var details = BuildIssueDetails(issue);
                if (details.Length != detailLengths[displayedIndex])
                {
                    return false;
                }

                builder.Append(blockSeparator).Append(details);
            }

            if (builder.Length != requiredLength)
            {
                return false;
            }

            clipboardText = builder.ToString();
            copiedIssueCount = displayedCount;
            return true;
        }

        /// <summary>一括copy対象がどのsnapshotと表示sliceかを誤読しないheaderを作ります。</summary>
        private static string BuildDisplayedIssuesClipboardHeader(
            LocalizationKeyAuditResult candidateResult,
            int displayedCount,
            int filteredCount)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Localization Key Audit - Displayed Issues");
            builder.Append("Result: ").AppendLine(candidateResult.IsComplete ? "Complete" : "Incomplete");
            builder.Append("Static Coverage: ").AppendLine(
                candidateResult.Coverage.IsComplete ? "Complete" : "Incomplete");
            builder.Append("Displayed Issues: ").AppendLine(displayedCount.ToString());
            builder.Append("Filtered Issues: ").AppendLine(filteredCount.ToString());
            builder.Append("Total Issues: ").Append(candidateResult.Issues.Count);
            return builder.ToString();
        }

        /// <summary>既存の単一issue詳細formatが生成するUTF-16 code unit数をallocation前に求めます。</summary>
        private static bool TryGetIssueDetailsLength(
            LocalizationKeyAuditIssue issue,
            out int detailLength)
        {
            detailLength = 0;
            if (issue == null || !IsKnownIssueKind(issue.Kind))
            {
                return false;
            }

            long length = 0;
            string lastValue = null;
            AddDetailLength(ref length, ref lastValue, "Kind", issue.Kind.ToString());
            AddDetailLength(ref length, ref lastValue, "Message", issue.Message);
            AddDetailLength(ref length, ref lastValue, "Asset", issue.AssetPath);
            AddDetailLength(ref length, ref lastValue, "Related", issue.RelatedAssetPath);
            AddDetailLength(ref length, ref lastValue, "Collection", issue.CollectionName);
            AddDetailLength(
                ref length,
                ref lastValue,
                "Collection GUID",
                issue.CollectionGuid == Guid.Empty ? string.Empty : issue.CollectionGuid.ToString("D"));
            AddDetailLength(ref length, ref lastValue, "Locale", issue.LocaleIdentifier);
            AddDetailLength(ref length, ref lastValue, "Entry Key", issue.EntryKey);
            AddDetailLength(
                ref length,
                ref lastValue,
                "Entry ID",
                issue.EntryId == 0 ? string.Empty : issue.EntryId.ToString());

            if (lastValue == null)
            {
                return false;
            }

            length -= Environment.NewLine.Length;
            var trailingWhitespace = 0;
            for (var index = lastValue.Length - 1; index >= 0 && char.IsWhiteSpace(lastValue[index]); index--)
            {
                trailingWhitespace++;
            }

            length -= trailingWhitespace;
            if (trailingWhitespace == lastValue.Length)
            {
                length--;
            }

            if (length < 0 || length > int.MaxValue)
            {
                return false;
            }

            detailLength = (int)length;
            return true;
        }

        /// <summary>空でないdetail行の未trim長と最後の値を蓄積します。</summary>
        private static void AddDetailLength(
            ref long length,
            ref string lastValue,
            string label,
            string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            length += label.Length + 2L + value.Length + Environment.NewLine.Length;
            lastValue = value;
        }

        /// <summary>未知kindを本文へ数値表示せず、既存の単一classifierで検証します。</summary>
        private static bool IsKnownIssueKind(LocalizationKeyAuditIssueKind kind)
        {
            try
            {
                ClassifyIssueKind(kind);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        /// <summary>値がある詳細だけを追記します。</summary>
        private static void AppendDetail(StringBuilder builder, string label, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                builder.Append(label).Append(": ").AppendLine(value);
            }
        }

        /// <summary>詳細欄の label/value を折り返して表示します。</summary>
        private void DrawDetailRow(string label, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(112f));
                EditorGUILayout.SelectableLabel(
                    value,
                    wrappedMiniLabelStyle,
                    GUILayout.MinHeight(EditorGUIUtility.singleLineHeight));
            }
        }

        /// <summary>assetをloadせずpath文字列だけをclipboardへcopyします。</summary>
        private void CopyPath(string assetPath)
        {
            EditorGUIUtility.systemCopyBuffer = assetPath;
            interactionMessage = $"asset pathをclipboardへcopyしました: {assetPath}";
        }

        /// <summary>指定 index が現在 result の問題を指すか調べます。</summary>
        private bool IsIssueIndexValid(int index)
        {
            return result != null && index >= 0 && index < result.Issues.Count;
        }

        /// <summary>大小文字を無視した ordinal 部分一致です。</summary>
        private static bool ContainsOrdinalIgnoreCase(string source, string value)
        {
            return !string.IsNullOrEmpty(source) &&
                source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>一覧と補足に使う style を domain reload 後に作ります。</summary>
        private void EnsureStyles()
        {
            if (issueRowStyle == null)
            {
                issueRowStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = true,
                    fixedHeight = 0f,
                    stretchWidth = true
                };
            }

            if (wrappedMiniLabelStyle == null)
            {
                wrappedMiniLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    wordWrap = true,
                    stretchWidth = true
                };
            }
        }
    }
}
