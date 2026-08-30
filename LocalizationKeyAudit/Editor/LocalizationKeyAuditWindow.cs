// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 必須ロケールの直接項目と、宣言済みアセット範囲の静的参照網羅を手動表示します。
    /// </summary>
    internal sealed class LocalizationKeyAuditWindow : EditorWindow
    {
        /// <summary>問題一覧の1ページへ描画する最大行数です。</summary>
        internal const int MaximumDisplayedIssues = 500;

        /// <summary>表示中の問題を一括コピーするときのUTF-16符号単位上限です。</summary>
        internal const int MaximumDisplayedIssueClipboardCharacters = 1_048_576;

        /// <summary>新規表示とドメイン再読み込み後の双方で使うウィンドウ名です。</summary>
        internal const string WindowTitle = "ローカライズキー監査";

        /// <summary>新しいウィンドウへ設定する既定の走査範囲説明です。</summary>
        internal const string DefaultScopeDescription =
            "Assets内のテキスト形式の.unity、.prefab、.assetにあるGUIDと項目識別子の直接参照";

        /// <summary>利用者が変更していない旧版の既定値だけを判別する文字列です。</summary>
        private const string LegacyDefaultScopeDescription =
            "Assets text .unity/.prefab/.asset の GUID + key ID direct references";

        /// <summary>問題種別を用途別に絞り込む表示区分です。</summary>
        internal enum IssueCategoryFilter
        {
            /// <summary>全問題を表示します。</summary>
            All,

            /// <summary>監査を完了できなかった問題です。</summary>
            Terminal,

            /// <summary>必須ロケールの直接テーブルと値の網羅問題です。</summary>
            RequiredLocaleCoverage,

            /// <summary>宣言済み範囲の静的参照問題です。</summary>
            StaticReferences,

            /// <summary>テーブル、項目、ロケール識別情報の整合性問題です。</summary>
            Integrity
        }

        /// <summary>区分選択欄へ列挙値と同じ順序で表示する日本語名です。</summary>
        private static readonly string[] IssueCategoryDisplayNames =
        {
            GetIssueCategoryDisplayName(IssueCategoryFilter.All),
            GetIssueCategoryDisplayName(IssueCategoryFilter.Terminal),
            GetIssueCategoryDisplayName(IssueCategoryFilter.RequiredLocaleCoverage),
            GetIssueCategoryDisplayName(IssueCategoryFilter.StaticReferences),
            GetIssueCategoryDisplayName(IssueCategoryFilter.Integrity)
        };

        /// <summary>絞り込み前の監査結果へ発行された問題件数を4区分で保持します。</summary>
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

            /// <summary>必須ロケール網羅の問題件数です。</summary>
            internal int RequiredLocaleCoverage { get; }

            /// <summary>静的参照の問題件数です。</summary>
            internal int StaticReferences { get; }

            /// <summary>テーブル、項目、ロケール識別情報の整合性問題件数です。</summary>
            internal int Integrity { get; }

            /// <summary>4区分の合計件数です。</summary>
            internal int Total => Terminal + RequiredLocaleCoverage + StaticReferences + Integrity;
        }

        /// <summary>カンマまたは改行区切りの必須ロケール識別子です。</summary>
        [SerializeField] private string requiredLocalesText = string.Empty;

        /// <summary>改行区切りのAssetsまたは登録済みパッケージ1件の静的参照範囲です。</summary>
        [SerializeField] private string declaredAssetPathsText = "Assets";

        /// <summary>結果へ残す静的参照網羅範囲の説明です。</summary>
        [SerializeField] private string scopeDescription = DefaultScopeDescription;

        /// <summary>問題の検索語です。</summary>
        [SerializeField] private string searchText = string.Empty;

        /// <summary>問題の表示区分です。</summary>
        [SerializeField] private IssueCategoryFilter issueCategory = IssueCategoryFilter.All;

        /// <summary>絞り込み後の問題一覧で表示する0始まりのページです。</summary>
        [SerializeField] private int issuePage;

        /// <summary>問題一覧のスクロール位置です。</summary>
        [SerializeField] private Vector2 issueScrollPosition;

        /// <summary>最小ウィンドウで全区画へ到達する外側のスクロール位置です。</summary>
        [SerializeField] private Vector2 windowScrollPosition;

        /// <summary>詳細欄のスクロール位置です。</summary>
        [SerializeField] private Vector2 detailScrollPosition;

        /// <summary>最後の手動監査結果です。</summary>
        private LocalizationKeyAuditResult result;

        /// <summary>最後の監査結果全体から1回だけ集計した絞り込み前の問題件数です。</summary>
        private IssueCategoryCounts issueCategoryCounts;

        /// <summary>絞り込み条件に一致する問題の添字です。</summary>
        private readonly List<int> visibleIssueIndices = new List<int>();

        /// <summary>現在選択中の問題の添字です。</summary>
        private int selectedIssueIndex = -1;

        /// <summary>ウィンドウ操作または予期しない例外の案内です。</summary>
        private string interactionMessage = string.Empty;

        /// <summary>一覧行へ使う折り返し表示形式です。</summary>
        private GUIStyle issueRowStyle;

        /// <summary>補足文へ使う折り返し表示形式です。</summary>
        private GUIStyle wrappedMiniLabelStyle;

        /// <summary>ウィンドウを開き、実用上の最小寸法を設定します。</summary>
        internal static void Open()
        {
            var window = GetWindow<LocalizationKeyAuditWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(820f, 640f);
            window.Show();
        }

        /// <summary>ドメイン再読み込み後にウィンドウ名と最小寸法を復元し、未編集の旧既定説明を移行して、自動監査は行いません。</summary>
        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            minSize = new Vector2(820f, 640f);
            if (string.Equals(scopeDescription, LegacyDefaultScopeDescription, StringComparison.Ordinal))
            {
                scopeDescription = DefaultScopeDescription;
            }
        }

        /// <summary>設定、手動操作、概要、問題一覧、詳細を順に描画します。</summary>
        private void OnGUI()
        {
            EnsureStyles();
            windowScrollPosition = EditorGUILayout.BeginScrollView(windowScrollPosition);
            EditorGUILayout.HelpBox(
                "Unityエディター専用の手動・読み取り専用監査です。結果は判断材料として使用してください。必須ロケールの直接値と、宣言済みのAssetsまたは登録済みパッケージ1件の範囲で認識したGUIDと項目識別子の参照だけを扱います。代替処理後の実行時翻訳を保証せず、参照が見つからない項目を「未使用」とは断定しません。",
                MessageType.Info);

            DrawRequestSettings();
            DrawToolbar();
            DrawStatus();

            if (result == null)
            {
                EditorGUILayout.HelpBox(
                    "必須ロケールとアセットの範囲を確認して「監査」を押してください。アセットは自動変更されず、監査はビルドを停止しません。",
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

        /// <summary>必須ロケール、範囲説明、AssetsまたはPackagesのパスを明示入力させます。</summary>
        private void DrawRequestSettings()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("監査設定", EditorStyles.boldLabel);
                requiredLocalesText = EditorGUILayout.TextField(
                    new GUIContent("必須ロケール", "例: en, ja。カンマまたは改行で区切ります。"),
                    requiredLocalesText);
                scopeDescription = EditorGUILayout.TextField(
                    new GUIContent("走査範囲の説明", "結果へそのまま残る、人が確認できる走査範囲の説明です。"),
                    scopeDescription);
                EditorGUILayout.LabelField(
                    new GUIContent(
                        "対象アセットのパス",
                        "改行区切り。同じルート内で複数指定できます。1回の監査では「Assets」または「Packages/<登録済みパッケージ名>」のどちらか1つだけをルートにします。"));
                declaredAssetPathsText = EditorGUILayout.TextArea(
                    declaredAssetPathsText,
                    GUILayout.MinHeight(42f),
                    GUILayout.MaxHeight(72f));
            }
        }

        /// <summary>検索、区分、監査、結果消去の操作欄を描画します。</summary>
        private void DrawToolbar()
        {
            var filterChanged = false;
            var categoryReset = false;
            var auditRequested = false;
            var clearRequested = false;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                searchText = EditorGUILayout.TextField(
                    "検索",
                    searchText,
                    EditorStyles.toolbarSearchField,
                    GUILayout.MinWidth(190f));
                GUILayout.Label("区分", GUILayout.Width(35f));
                var issueCategoryIndex = (int)issueCategory;
                if (issueCategoryIndex < 0 || issueCategoryIndex >= IssueCategoryDisplayNames.Length)
                {
                    issueCategory = IssueCategoryFilter.All;
                    issueCategoryIndex = 0;
                    categoryReset = true;
                }

                issueCategory = (IssueCategoryFilter)EditorGUILayout.Popup(
                    issueCategoryIndex,
                    IssueCategoryDisplayNames,
                    EditorStyles.toolbarPopup,
                    GUILayout.Width(158f));
                filterChanged = EditorGUI.EndChangeCheck();

                GUILayout.FlexibleSpace();
                auditRequested = GUILayout.Button("監査", EditorStyles.toolbarButton, GUILayout.Width(52f));
                clearRequested = GUILayout.Button("結果を消去", EditorStyles.toolbarButton, GUILayout.Width(80f));
            }

            if (filterChanged || categoryReset)
            {
                RebuildVisibleIssues(true);
            }

            if (categoryReset)
            {
                interactionMessage = "問題区分の設定が不正だったため、「すべて」に戻しました。";
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

        /// <summary>監査完了性、件数、静的参照網羅の境界、表示上限を描画します。</summary>
        private void DrawStatus()
        {
            if (!string.IsNullOrEmpty(interactionMessage))
            {
                EditorGUILayout.HelpBox(interactionMessage, MessageType.None);
            }

            if (result == null)
            {
                EditorGUILayout.LabelField($"表示上限: 1ページにつき問題 {MaximumDisplayedIssues} 件", EditorStyles.miniLabel);
                return;
            }

            var completion = result.IsComplete ? "完了" : "未完了";
            EditorGUILayout.LabelField(
                $"{completion} / ロケール {result.LocaleIdentifiers.Count} / コレクション {result.Collections.Count} / 所属先なしテーブル {result.OrphanLocaleTables.Count} / 問題 {result.Issues.Count} / 参照関係 {result.GraphEdgeCount}",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"問題区分（絞り込み前の結果）: 監査停止 {issueCategoryCounts.Terminal} / 必須ロケール網羅 {issueCategoryCounts.RequiredLocaleCoverage} / 静的参照 {issueCategoryCounts.StaticReferences} / 整合性 {issueCategoryCounts.Integrity}",
                wrappedMiniLabelStyle);
            EditorGUILayout.LabelField(
                $"静的参照網羅: {(result.Coverage.IsComplete ? "完了" : "未完了")} / 認識済み参照 {result.Coverage.RecognizedReferences.Count} / 対象パス {result.Coverage.DeclaredAssetPaths.Count} / 絞り込み後の問題 {visibleIssueIndices.Count}",
                wrappedMiniLabelStyle);
            EditorGUILayout.LabelField(
                $"走査範囲: {result.Coverage.ScopeDescription}",
                wrappedMiniLabelStyle);
            if (!result.Coverage.IsComplete)
            {
                EditorGUILayout.HelpBox(result.Coverage.IncompleteReason, MessageType.Warning);
            }

            EditorGUILayout.LabelField(
                $"表示上限: 1ページにつき問題 {MaximumDisplayedIssues} 件。絞り込み後の全件はページを切り替えて確認できます。",
                EditorStyles.miniLabel);
        }

        /// <summary>決定論的な結果順を維持し、絞り込み後の問題の現在ページだけを描画します。</summary>
        private void DrawIssues(float height)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                issuePage = ClampIssuePage(
                    issuePage,
                    GetIssuePageCount(visibleIssueIndices.Count));
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"問題 ({visibleIssueIndices.Count})", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(visibleIssueIndices.Count == 0))
                    {
                        if (GUILayout.Button("表示分をコピー", GUILayout.Width(112f)))
                        {
                            CopyDisplayedIssues();
                        }
                    }
                }

                DrawIssuePageControls();
                var pageStart = GetIssuePageStart(issuePage, visibleIssueIndices.Count);
                var displayedCount = Math.Min(
                    visibleIssueIndices.Count - pageStart,
                    MaximumDisplayedIssues);
                issueScrollPosition = EditorGUILayout.BeginScrollView(issueScrollPosition, GUILayout.Height(height));
                if (displayedCount == 0)
                {
                    EditorGUILayout.LabelField("現在の絞り込み条件に一致する問題はありません。", wrappedMiniLabelStyle);
                }

                for (var rowIndex = 0; rowIndex < displayedCount; rowIndex++)
                {
                    var visibleIndex = pageStart + rowIndex;
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
                        $"{pageStart + 1}-{pageStart + displayedCount} / {visibleIssueIndices.Count} 件を表示しています。",
                        MessageType.Warning);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        /// <summary>絞り込み後の問題一覧へ前後移動とページ番号を描画します。</summary>
        private void DrawIssuePageControls()
        {
            var pageCount = GetIssuePageCount(visibleIssueIndices.Count);
            issuePage = ClampIssuePage(issuePage, pageCount);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(issuePage <= 0))
                {
                    if (GUILayout.Button("前へ", GUILayout.Width(52f)))
                    {
                        SetIssuePage(issuePage - 1);
                    }
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(
                    $"ページ {issuePage + 1} / {pageCount}",
                    EditorStyles.miniLabel,
                    GUILayout.Width(96f));
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(issuePage + 1 >= pageCount))
                {
                    if (GUILayout.Button("次へ", GUILayout.Width(52f)))
                    {
                        SetIssuePage(issuePage + 1);
                    }
                }
            }
        }

        /// <summary>選択問題の識別情報と安全な操作を表示します。</summary>
        private void DrawDetails(float height)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("詳細", EditorStyles.boldLabel);
                if (!IsIssueIndexValid(selectedIssueIndex))
                {
                    EditorGUILayout.LabelField("問題を選択してください。", wrappedMiniLabelStyle);
                    return;
                }

                var issue = result.Issues[selectedIssueIndex];
                detailScrollPosition = EditorGUILayout.BeginScrollView(detailScrollPosition, GUILayout.Height(height));
                DrawDetailRow("種別", GetIssueKindDisplayName(issue.Kind));
                DrawDetailRow("説明", issue.Message);
                DrawDetailRow("アセット", issue.AssetPath);
                DrawDetailRow("関連アセット", issue.RelatedAssetPath);
                DrawDetailRow("コレクション", issue.CollectionName);
                DrawDetailRow("コレクション識別子（GUID）", issue.CollectionGuid == Guid.Empty ? string.Empty : issue.CollectionGuid.ToString("D"));
                DrawDetailRow("ロケール", issue.LocaleIdentifier);
                DrawDetailRow("項目キー", issue.EntryKey);
                DrawDetailRow("項目識別子", issue.EntryId == 0 ? string.Empty : issue.EntryId.ToString());
                EditorGUILayout.EndScrollView();

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(issue.AssetPath)))
                    {
                        if (GUILayout.Button("アセットパスをコピー", GUILayout.Width(148f)))
                        {
                            CopyPath(issue.AssetPath);
                        }
                    }

                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(issue.RelatedAssetPath)))
                    {
                        if (GUILayout.Button("関連パスをコピー", GUILayout.Width(132f)))
                        {
                            CopyPath(issue.RelatedAssetPath);
                        }
                    }

                    if (GUILayout.Button("詳細をコピー", GUILayout.Width(104f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = BuildIssueDetails(issue);
                        interactionMessage = "選択した問題の詳細をクリップボードへコピーしました。";
                    }
                }
            }
        }

        /// <summary>現在の入力から静的参照網羅と監査結果を1回だけ取得します。</summary>
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
                issuePage = 0;
                selectedIssueIndex = -1;
                issueScrollPosition = Vector2.zero;
                detailScrollPosition = Vector2.zero;
                interactionMessage = $"監査を開始できませんでした: {exception.GetType().Name}";
            }
        }

        /// <summary>入力は維持したまま結果、選択、操作案内だけを消します。</summary>
        private void ClearResult()
        {
            result = null;
            issueCategoryCounts = default;
            visibleIssueIndices.Clear();
            issuePage = 0;
            selectedIssueIndex = -1;
            issueScrollPosition = Vector2.zero;
            detailScrollPosition = Vector2.zero;
            windowScrollPosition = Vector2.zero;
            interactionMessage = string.Empty;
        }

        /// <summary>現在の絞り込みで画面に表示する問題だけを検証後に1回でクリップボードへコピーします。</summary>
        private void CopyDisplayedIssues()
        {
            if (!TryBuildDisplayedIssuesClipboardText(
                    result,
                    visibleIssueIndices,
                    issuePage,
                    out var clipboardText,
                    out var copiedIssueCount))
            {
                interactionMessage =
                    "表示中の問題をクリップボードへコピーできませんでした。監査結果と絞り込み条件を再確認してください。";
                return;
            }

            EditorGUIUtility.systemCopyBuffer = clipboardText;
            interactionMessage = $"画面に表示中の問題 {copiedIssueCount} 件をクリップボードへコピーしました。";
        }

        /// <summary>必須ロケールのカンマ・セミコロン・改行区切りを順序保持で解析します。</summary>
        internal static IReadOnlyList<string> ParseRequiredLocales(string text)
        {
            return ParseTokens(
                text,
                new[] { ',', ';', '\r', '\n' },
                LocalizationKeyAuditLimits.MaximumRequiredLocales,
                "必須ロケール",
                true);
        }

        /// <summary>アセット範囲の改行区切りを順序保持で解析します。</summary>
        internal static IReadOnlyList<string> ParseDeclaredAssetPaths(string text)
        {
            return ParseTokens(
                text,
                new[] { '\r', '\n' },
                LocalizationKeyAuditLimits.MaximumDeclaredAssetPaths,
                "対象アセットパス",
                false);
        }

        /// <summary>検索語と区分が問題へ一致するかを副作用なしで判定します。</summary>
        internal static bool MatchesFilter(
            LocalizationKeyAuditIssue issue,
            string candidateSearchText,
            IssueCategoryFilter category)
        {
            return MatchesNormalizedFilter(issue, NormalizeSearchText(candidateSearchText), category);
        }

        /// <summary>検索語の上限確認と前後空白の除去を1回だけ行い、問題走査中の再確保を防ぎます。</summary>
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

        /// <summary>正規化済み検索語と区分を追加のメモリ確保なしで照合します。</summary>
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

            return ContainsOrdinalIgnoreCase(GetIssueKindDisplayName(issue.Kind), normalizedSearchText) ||
                ContainsOrdinalIgnoreCase(issue.Kind.ToString(), normalizedSearchText) ||
                ContainsOrdinalIgnoreCase(issue.Message, normalizedSearchText) ||
                ContainsOrdinalIgnoreCase(issue.AssetPath, normalizedSearchText) ||
                ContainsOrdinalIgnoreCase(issue.RelatedAssetPath, normalizedSearchText) ||
                ContainsOrdinalIgnoreCase(issue.CollectionName, normalizedSearchText) ||
                ContainsOrdinalIgnoreCase(issue.CollectionGuid == Guid.Empty ? string.Empty : issue.CollectionGuid.ToString("D"), normalizedSearchText) ||
                ContainsOrdinalIgnoreCase(issue.LocaleIdentifier, normalizedSearchText) ||
                ContainsOrdinalIgnoreCase(issue.EntryKey, normalizedSearchText) ||
                ContainsOrdinalIgnoreCase(issue.EntryId == 0 ? string.Empty : issue.EntryId.ToString(), normalizedSearchText);
        }

        /// <summary>現在の絞り込み条件に一致する問題の添字を再構築します。</summary>
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
                issuePage = 0;
                selectedIssueIndex = -1;
                issueScrollPosition = Vector2.zero;
                detailScrollPosition = Vector2.zero;
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

            if (resetSelection)
            {
                SetIssuePage(0);
                return;
            }

            issuePage = ClampIssuePage(issuePage, GetIssuePageCount(visibleIssueIndices.Count));
            EnsureSelection();
        }

        /// <summary>選択が現在ページの絞り込み後の問題に含まれる状態を維持します。</summary>
        private void EnsureSelection()
        {
            issuePage = ClampIssuePage(issuePage, GetIssuePageCount(visibleIssueIndices.Count));
            var pageStart = GetIssuePageStart(issuePage, visibleIssueIndices.Count);
            var pageEnd = Math.Min(pageStart + MaximumDisplayedIssues, visibleIssueIndices.Count);
            var selectedVisibleIndex = visibleIssueIndices.IndexOf(selectedIssueIndex);
            if (!IsIssueIndexValid(selectedIssueIndex) ||
                selectedVisibleIndex < pageStart ||
                selectedVisibleIndex >= pageEnd)
            {
                selectedIssueIndex = pageStart < pageEnd
                    ? visibleIssueIndices[pageStart]
                    : -1;
            }
        }

        /// <summary>問題ページを変更し、ページ先頭へ選択を同期します。</summary>
        private void SetIssuePage(int page)
        {
            issuePage = ClampIssuePage(page, GetIssuePageCount(visibleIssueIndices.Count));
            var pageStart = GetIssuePageStart(issuePage, visibleIssueIndices.Count);
            selectedIssueIndex = pageStart < visibleIssueIndices.Count
                ? visibleIssueIndices[pageStart]
                : -1;
            issueScrollPosition = Vector2.zero;
            detailScrollPosition = Vector2.zero;
            interactionMessage = string.Empty;
            Repaint();
        }

        /// <summary>絞り込み後の問題件数から500件単位のページ数を返します。</summary>
        internal static int GetIssuePageCount(int visibleCount)
        {
            return visibleCount <= 0
                ? 1
                : ((visibleCount - 1) / MaximumDisplayedIssues) + 1;
        }

        /// <summary>問題ページを有効範囲へ制限します。</summary>
        internal static int ClampIssuePage(int page, int pageCount)
        {
            var safePageCount = Math.Max(1, pageCount);
            return Math.Max(0, Math.Min(page, safePageCount - 1));
        }

        /// <summary>指定ページが絞り込み後の問題一覧で開始する添字を返します。</summary>
        internal static int GetIssuePageStart(int page, int visibleCount)
        {
            var pageCount = GetIssuePageCount(visibleCount);
            return ClampIssuePage(page, pageCount) * MaximumDisplayedIssues;
        }

        /// <summary>絞り込み前の問題一覧を1回だけ走査し、4区分の件数を返します。</summary>
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
                    throw new ArgumentException("問題一覧に空の要素が含まれています。", nameof(issues));
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

        /// <summary>問題種別を絞り込みと集計で共用する1つの表示区分へ割り当てます。</summary>
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
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "未分類の問題種別です。");
            }
        }

        /// <summary>表示区分の利用者向け日本語名を返します。</summary>
        internal static string GetIssueCategoryDisplayName(IssueCategoryFilter category)
        {
            switch (category)
            {
                case IssueCategoryFilter.All:
                    return "すべて";
                case IssueCategoryFilter.Terminal:
                    return "監査停止";
                case IssueCategoryFilter.RequiredLocaleCoverage:
                    return "必須ロケール網羅";
                case IssueCategoryFilter.StaticReferences:
                    return "静的参照";
                case IssueCategoryFilter.Integrity:
                    return "整合性";
                default:
                    throw new ArgumentOutOfRangeException(nameof(category), category, "未分類の表示区分です。");
            }
        }

        /// <summary>問題種別の利用者向け日本語名を返します。</summary>
        internal static string GetIssueKindDisplayName(LocalizationKeyAuditIssueKind kind)
        {
            switch (kind)
            {
                case LocalizationKeyAuditIssueKind.ReadOnlyGuaranteeUnavailable:
                    return "読み取り専用保証不可";
                case LocalizationKeyAuditIssueKind.InvalidConfiguration:
                    return "設定不備";
                case LocalizationKeyAuditIssueKind.LimitExceeded:
                    return "上限超過";
                case LocalizationKeyAuditIssueKind.AuditFailed:
                    return "監査失敗";
                case LocalizationKeyAuditIssueKind.RequiredLocaleNotConfigured:
                    return "必須ロケール未登録";
                case LocalizationKeyAuditIssueKind.MissingLocaleTable:
                    return "ロケールテーブル不足";
                case LocalizationKeyAuditIssueKind.MissingDirectEntry:
                    return "直接項目不足";
                case LocalizationKeyAuditIssueKind.EmptyDirectValue:
                    return "直接値が空";
                case LocalizationKeyAuditIssueKind.DanglingStaticReference:
                    return "解決不能な静的参照";
                case LocalizationKeyAuditIssueKind.NoStaticReferenceFoundWithinDeclaredScope:
                    return "宣言範囲内の静的参照なし";
                case LocalizationKeyAuditIssueKind.StaticReferenceCoverageIncomplete:
                    return "静的参照網羅が未完了";
                case LocalizationKeyAuditIssueKind.DuplicateCollectionName:
                    return "コレクション名重複";
                case LocalizationKeyAuditIssueKind.DuplicateCollectionGuid:
                    return "コレクション識別子（GUID）重複";
                case LocalizationKeyAuditIssueKind.DuplicateSharedEntryId:
                    return "共有項目識別子重複";
                case LocalizationKeyAuditIssueKind.DuplicateSharedEntryKey:
                    return "共有項目キー重複";
                case LocalizationKeyAuditIssueKind.DuplicateLocaleTable:
                    return "ロケールテーブル重複";
                case LocalizationKeyAuditIssueKind.DuplicateLocalizedEntryId:
                    return "翻訳項目識別子重複";
                case LocalizationKeyAuditIssueKind.OrphanedLocalizedEntry:
                    return "所属先なし翻訳項目";
                case LocalizationKeyAuditIssueKind.OrphanedLocaleTable:
                    return "所属先なしロケールテーブル";
                case LocalizationKeyAuditIssueKind.OrphanedSharedTableData:
                    return "所属先なし共有テーブルデータ";
                case LocalizationKeyAuditIssueKind.DuplicateLocaleIdentifier:
                    return "ロケール識別子重複";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "表示名のない問題種別です。");
            }
        }

        /// <summary>問題種別が指定した表示区分へ一致するかを判定します。</summary>
        private static bool MatchesCategory(LocalizationKeyAuditIssueKind kind, IssueCategoryFilter category)
        {
            var issueCategory = ClassifyIssueKind(kind);

            return category == IssueCategoryFilter.All || category == issueCategory;
        }

        /// <summary>区切り文字で分け、空白だけの要素を除きます。</summary>
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

        /// <summary>問題一覧向けの1行表示を作ります。</summary>
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
            var kindDisplayName = GetIssueKindDisplayName(issue.Kind);
            return string.IsNullOrEmpty(identity)
                ? kindDisplayName
                : $"{kindDisplayName}  |  {identity}";
        }

        /// <summary>クリップボード用の全詳細を組み立てます。</summary>
        private static string BuildIssueDetails(LocalizationKeyAuditIssue issue)
        {
            var builder = new StringBuilder();
            AppendDetail(builder, "種別", GetIssueKindDisplayName(issue.Kind));
            AppendDetail(builder, "説明", issue.Message);
            AppendDetail(builder, "アセット", issue.AssetPath);
            AppendDetail(builder, "関連アセット", issue.RelatedAssetPath);
            AppendDetail(builder, "コレクション", issue.CollectionName);
            AppendDetail(builder, "コレクション識別子（GUID）", issue.CollectionGuid == Guid.Empty ? string.Empty : issue.CollectionGuid.ToString("D"));
            AppendDetail(builder, "ロケール", issue.LocaleIdentifier);
            AppendDetail(builder, "項目キー", issue.EntryKey);
            AppendDetail(builder, "項目識別子", issue.EntryId == 0 ? string.Empty : issue.EntryId.ToString());
            return builder.ToString().TrimEnd();
        }

        /// <summary>
        /// 絞り込み後の添字の指定ページを、完了状態と件数見出し付きの上限制御された
        /// クリップボード本文へ変換します。
        /// </summary>
        internal static bool TryBuildDisplayedIssuesClipboardText(
            LocalizationKeyAuditResult candidateResult,
            IReadOnlyList<int> candidateVisibleIssueIndices,
            int candidateIssuePage,
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

            var pageCount = GetIssuePageCount(candidateVisibleIssueIndices.Count);
            if (candidateIssuePage < 0 || candidateIssuePage >= pageCount)
            {
                return false;
            }

            var pageStart = GetIssuePageStart(candidateIssuePage, candidateVisibleIssueIndices.Count);
            var displayedCount = Math.Min(
                candidateVisibleIssueIndices.Count - pageStart,
                MaximumDisplayedIssues);
            if (displayedCount <= 0)
            {
                return false;
            }

            var header = BuildDisplayedIssuesClipboardHeader(
                candidateResult,
                candidateIssuePage,
                pageCount,
                pageStart,
                displayedCount,
                candidateVisibleIssueIndices.Count);
            var blockSeparator = Environment.NewLine + Environment.NewLine;
            long requiredLength = header.Length;
            var detailLengths = new int[displayedCount];
            for (var displayedIndex = 0; displayedIndex < displayedCount; displayedIndex++)
            {
                var visibleIndex = pageStart + displayedIndex;
                var issue = candidateResult.Issues[candidateVisibleIssueIndices[visibleIndex]];
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
                var visibleIndex = pageStart + displayedIndex;
                var issue = candidateResult.Issues[candidateVisibleIssueIndices[visibleIndex]];
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

        /// <summary>一括コピー対象の監査結果と表示範囲を誤読しない見出しを作ります。</summary>
        private static string BuildDisplayedIssuesClipboardHeader(
            LocalizationKeyAuditResult candidateResult,
            int issuePage,
            int pageCount,
            int pageStart,
            int displayedCount,
            int filteredCount)
        {
            var builder = new StringBuilder();
            builder.AppendLine("ローカライズキー監査 - 表示中の問題");
            builder.Append("監査結果: ").AppendLine(candidateResult.IsComplete ? "完了" : "未完了");
            builder.Append("静的参照網羅: ").AppendLine(
                candidateResult.Coverage.IsComplete ? "完了" : "未完了");
            builder.Append("表示ページ: ").Append(issuePage + 1).Append(" / ").AppendLine(pageCount.ToString());
            builder.Append("表示範囲: ").Append(pageStart + 1).Append('-').AppendLine(
                (pageStart + displayedCount).ToString());
            builder.Append("表示件数: ").AppendLine(displayedCount.ToString());
            builder.Append("絞り込み後の件数: ").AppendLine(filteredCount.ToString());
            builder.Append("問題総数: ").Append(candidateResult.Issues.Count);
            return builder.ToString();
        }

        /// <summary>単一問題の詳細形式が生成するUTF-16符号単位数をメモリ確保前に求めます。</summary>
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
            AddDetailLength(ref length, ref lastValue, "種別", GetIssueKindDisplayName(issue.Kind));
            AddDetailLength(ref length, ref lastValue, "説明", issue.Message);
            AddDetailLength(ref length, ref lastValue, "アセット", issue.AssetPath);
            AddDetailLength(ref length, ref lastValue, "関連アセット", issue.RelatedAssetPath);
            AddDetailLength(ref length, ref lastValue, "コレクション", issue.CollectionName);
            AddDetailLength(
                ref length,
                ref lastValue,
                "コレクション識別子（GUID）",
                issue.CollectionGuid == Guid.Empty ? string.Empty : issue.CollectionGuid.ToString("D"));
            AddDetailLength(ref length, ref lastValue, "ロケール", issue.LocaleIdentifier);
            AddDetailLength(ref length, ref lastValue, "項目キー", issue.EntryKey);
            AddDetailLength(
                ref length,
                ref lastValue,
                "項目識別子",
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

        /// <summary>空でない詳細行の整形前の長さと最後の値を蓄積します。</summary>
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

        /// <summary>未知の問題種別を本文へ数値表示せず、既存の単一分類処理で検証します。</summary>
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

        /// <summary>詳細欄の項目名と値を折り返して表示します。</summary>
        private void DrawDetailRow(string label, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(136f));
                EditorGUILayout.SelectableLabel(
                    value,
                    wrappedMiniLabelStyle,
                    GUILayout.MinHeight(EditorGUIUtility.singleLineHeight));
            }
        }

        /// <summary>アセットを読み込まず、パス文字列だけをクリップボードへコピーします。</summary>
        private void CopyPath(string assetPath)
        {
            EditorGUIUtility.systemCopyBuffer = assetPath;
            interactionMessage = $"アセットパスをクリップボードへコピーしました: {assetPath}";
        }

        /// <summary>指定した添字が現在の結果にある問題を指すか調べます。</summary>
        private bool IsIssueIndexValid(int index)
        {
            return result != null && index >= 0 && index < result.Issues.Count;
        }

        /// <summary>言語文化に依存せず、大小文字を無視する部分一致です。</summary>
        private static bool ContainsOrdinalIgnoreCase(string source, string value)
        {
            return !string.IsNullOrEmpty(source) &&
                source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>一覧と補足に使う表示形式をドメイン再読み込み後に作ります。</summary>
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
