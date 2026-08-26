using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AssemblyDependencyAudit.Editor
{
    /// <summary>
    /// asmdef graph、asmref target、検出問題を読み取り専用で表示します。
    /// </summary>
    internal sealed class AssemblyDependencyAuditWindow : EditorWindow
    {
        /// <summary>1 つの一覧へ描画する最大行数です。</summary>
        internal const int MaximumDisplayedRows = 500;

        /// <summary>1回のfilterで評価するsearch文字数です。</summary>
        internal const int MaximumSearchCharacters = 512;

        /// <summary>asmref rowのfile名に表示する最大文字数です。</summary>
        private const int MaximumAssemblyReferenceRowNameCharacters = 20;

        /// <summary>tooltipとDetailsで1項目へ表示する最大文字数です。</summary>
        internal const int MaximumDisplayedTextCharacters = 4096;

        /// <summary>asmref row tooltipへ表示するpathの最大文字数です。</summary>
        private const int MaximumAssemblyReferenceTooltipPathCharacters = 1024;

        /// <summary>固定高のissue rowへ表示する説明の最大文字数です。</summary>
        private const int MaximumIssueRowMessageCharacters = 160;

        /// <summary>監査対象 path の範囲です。</summary>
        internal enum ScopeFilter
        {
            /// <summary>Assets と Packages の両方です。</summary>
            All,

            /// <summary>Assets 配下だけです。</summary>
            Assets,

            /// <summary>Packages 配下だけです。</summary>
            Packages
        }

        /// <summary>Player で利用できるかによる絞り込みです。</summary>
        internal enum PlatformFilter
        {
            /// <summary>platform 設定を問わず表示します。</summary>
            All,

            /// <summary>Editor 専用ではない assembly を表示します。</summary>
            PlayerCapable,

            /// <summary>includePlatforms が Editor だけの assembly を表示します。</summary>
            EditorOnly
        }

        /// <summary>検出問題の有無による絞り込みです。</summary>
        internal enum IssueFilter
        {
            /// <summary>問題の有無を問わず表示します。</summary>
            All,

            /// <summary>問題に関係する assembly だけを表示します。</summary>
            WithIssues,

            /// <summary>問題に関係しない assembly だけを表示します。</summary>
            WithoutIssues
        }

        /// <summary>名前または path を絞り込む文字列です。</summary>
        [SerializeField] private string _searchText = string.Empty;

        /// <summary>Assets と Packages の表示範囲です。</summary>
        [SerializeField] private ScopeFilter _scopeFilter = ScopeFilter.All;

        /// <summary>Player 用と Editor 専用の表示範囲です。</summary>
        [SerializeField] private PlatformFilter _platformFilter = PlatformFilter.All;

        /// <summary>問題の有無による表示範囲です。</summary>
        [SerializeField] private IssueFilter _issueFilter = IssueFilter.All;

        /// <summary>左列の縦方向位置です。</summary>
        [SerializeField] private Vector2 _dependentsScrollPosition;

        /// <summary>中央列の縦方向位置です。</summary>
        [SerializeField] private Vector2 _assembliesScrollPosition;

        /// <summary>右列の縦方向位置です。</summary>
        [SerializeField] private Vector2 _dependenciesScrollPosition;

        /// <summary>問題一覧の縦方向位置です。</summary>
        [SerializeField] private Vector2 _issuesScrollPosition;

        /// <summary>asmref target一覧の縦方向位置です。</summary>
        [SerializeField] private Vector2 _assemblyReferencesScrollPosition;

        /// <summary>filtered asmref target一覧で表示する0始まりpageです。</summary>
        [SerializeField] private int _assemblyReferencePage;

        /// <summary>詳細欄の縦方向位置です。</summary>
        [SerializeField] private Vector2 _detailsScrollPosition;

        /// <summary>現在の project を読み取る監査処理です。</summary>
        private AssemblyDependencyAuditService _service;

        /// <summary>最後に完全に成功した監査結果です。</summary>
        private AssemblyDependencyAuditResult _result;

        /// <summary>現在の filter に一致する assembly index です。</summary>
        private readonly List<int> _visibleAssemblyIndices = new List<int>();

        /// <summary>現在のsearch・scope・issue filterに一致するasmref target indexです。</summary>
        private readonly List<int> _visibleAssemblyReferenceIndices = new List<int>();

        /// <summary>問題へ直接または関連先として含まれる path です。</summary>
        private readonly HashSet<string> _issuePaths = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>path ごとに関係する問題 index を保持します。</summary>
        private readonly Dictionary<string, List<int>> _issueIndicesByPath = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        /// <summary>中央または左右の列で選択した assembly index です。</summary>
        private int _selectedAssemblyIndex = -1;

        /// <summary>問題一覧で選択した問題 index です。</summary>
        private int _selectedIssueIndex = -1;

        /// <summary>別一覧で選択した asmref target index です。</summary>
        private int _selectedAssemblyReferenceIndex = -1;

        /// <summary>監査に失敗した理由です。</summary>
        private string _auditErrorMessage = string.Empty;

        /// <summary>Ping、Open、Copy の実行結果です。</summary>
        private string _interactionMessage = string.Empty;

        /// <summary>監査結果に含まれる参照総数です。</summary>
        private int _referenceCount;

        /// <summary>一覧行を描画する style です。</summary>
        private GUIStyle _rowStyle;

        /// <summary>asmrefの2行表示を折り返さない専用styleです。</summary>
        private GUIStyle _assemblyReferenceRowStyle;

        /// <summary>折り返す補足表示の style です。</summary>
        private GUIStyle _wrappedMiniLabelStyle;

        /// <summary>
        /// Window を開き、監査結果を表示できる最小 size を設定します。
        /// </summary>
        internal static void Open()
        {
            var window = GetWindow<AssemblyDependencyAuditWindow>();
            window.titleContent = new GUIContent("Assembly Dependency Audit");
            window.minSize = new Vector2(860f, 660f);
            window.Show();
        }

        /// <summary>
        /// domain reload 後に Unity を使う依存を作り直します。
        /// </summary>
        private void OnEnable()
        {
            minSize = new Vector2(860f, 660f);
            if (_service == null)
            {
                _service = new AssemblyDependencyAuditService();
            }
        }

        /// <summary>
        /// filter、3 列 graph、問題と詳細を順に描画します。
        /// </summary>
        private void OnGUI()
        {
            EnsureStyles();
            EditorGUILayout.HelpBox(
                "Assets と Packages の asmdef graph と asmref target を読み取り専用で監査します。Refresh、Ping、Open、Copy は project file を変更しません。",
                MessageType.Info);

            DrawToolbar();
            DrawStatus();

            if (_result == null)
            {
                EditorGUILayout.HelpBox("Refresh を押すと Assembly Definition の依存関係を表示します。", MessageType.None);
                return;
            }

            EnsureSelection();
            var graphHeight = Mathf.Max(180f, Mathf.Min(360f, position.height * 0.36f));
            DrawDependencyColumns(graphHeight);

            EditorGUILayout.Space(6f);
            var lowerHeight = Mathf.Max(150f, position.height - graphHeight - 270f);
            DrawIssuesAndDetails(lowerHeight);
        }

        /// <summary>
        /// 検索、範囲、platform、問題 filter と再監査操作を描画します。
        /// </summary>
        private void DrawToolbar()
        {
            var filtersChanged = false;
            var refreshRequested = false;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                _searchText = LimitSearchText(EditorGUILayout.TextField(
                    "Search",
                    _searchText,
                    EditorStyles.toolbarSearchField,
                    GUILayout.MinWidth(180f)));
                GUILayout.Label("Scope", GUILayout.Width(40f));
                _scopeFilter = (ScopeFilter)EditorGUILayout.EnumPopup(_scopeFilter, EditorStyles.toolbarPopup, GUILayout.Width(82f));
                GUILayout.Label("Platform", GUILayout.Width(52f));
                _platformFilter = (PlatformFilter)EditorGUILayout.EnumPopup(_platformFilter, EditorStyles.toolbarPopup, GUILayout.Width(105f));
                GUILayout.Label("Issues", GUILayout.Width(36f));
                _issueFilter = (IssueFilter)EditorGUILayout.EnumPopup(_issueFilter, EditorStyles.toolbarPopup, GUILayout.Width(92f));
                filtersChanged = EditorGUI.EndChangeCheck();

                refreshRequested = GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(68f));
            }

            if (filtersChanged)
            {
                RebuildVisibleAssemblyIndices(true);
            }

            if (refreshRequested)
            {
                RefreshAudit();
            }
        }

        /// <summary>
        /// 監査件数、表示件数と 500 行上限を表示します。
        /// </summary>
        private void DrawStatus()
        {
            if (!string.IsNullOrEmpty(_auditErrorMessage))
            {
                EditorGUILayout.HelpBox(_auditErrorMessage, MessageType.Error);
            }

            if (!string.IsNullOrEmpty(_interactionMessage))
            {
                EditorGUILayout.HelpBox(_interactionMessage, MessageType.None);
            }

            if (_result == null)
            {
                EditorGUILayout.LabelField($"表示上限: 各一覧 {MaximumDisplayedRows} 件", EditorStyles.miniLabel);
                return;
            }

            EditorGUILayout.LabelField(
                $"Assemblies {_result.Assemblies.Count} / References {_referenceCount} / Assembly References {_result.AssemblyReferences.Count} / Issues {_result.Issues.Count}",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"Filtered: {_visibleAssemblyIndices.Count} asmdef / {_visibleAssemblyReferenceIndices.Count} asmref",
                EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(
                $"表示上限: 各一覧 {MaximumDisplayedRows} 件 / Search {MaximumSearchCharacters} 文字。超過分は Search と filter で絞り込んでください。",
                EditorStyles.miniLabel);
        }

        /// <summary>
        /// Referenced By、Assemblies、Depends On の 3 列を描画します。
        /// </summary>
        private void DrawDependencyColumns(float height)
        {
            var selectedIndex = IsAssemblyIndexValid(_selectedAssemblyIndex) ? _selectedAssemblyIndex : -1;
            var dependents = selectedIndex >= 0 ? _result.Dependents[selectedIndex] : Array.Empty<int>();
            var dependencies = selectedIndex >= 0 ? _result.Dependencies[selectedIndex] : Array.Empty<int>();

            using (new EditorGUILayout.HorizontalScope())
            {
                _dependentsScrollPosition = DrawAssemblyColumn("Referenced By", dependents, _dependentsScrollPosition, height);
                _assembliesScrollPosition = DrawAssemblyColumn("Assemblies", _visibleAssemblyIndices, _assembliesScrollPosition, height);
                _dependenciesScrollPosition = DrawAssemblyColumn("Depends On", dependencies, _dependenciesScrollPosition, height);
            }
        }

        /// <summary>
        /// 指定された assembly index を決定論的な受取順で最大 500 件描画します。
        /// </summary>
        private Vector2 DrawAssemblyColumn(string heading, IReadOnlyList<int> indices, Vector2 scrollPosition, float height)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField($"{heading} ({indices.Count})", EditorStyles.boldLabel);
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(height));
                var displayedCount = Math.Min(indices.Count, MaximumDisplayedRows);
                if (displayedCount == 0)
                {
                    EditorGUILayout.LabelField("該当する assembly はありません。", _wrappedMiniLabelStyle);
                }

                for (var rowIndex = 0; rowIndex < displayedCount; rowIndex++)
                {
                    var assemblyIndex = indices[rowIndex];
                    if (!IsAssemblyIndexValid(assemblyIndex))
                    {
                        continue;
                    }

                    var node = _result.Assemblies[assemblyIndex];
                    var selected = assemblyIndex == _selectedAssemblyIndex;
                    var content = new GUIContent(FormatAssemblyRow(node), node.AssetPath);
                    var next = GUILayout.Toggle(
                        selected,
                        content,
                        _rowStyle,
                        GUILayout.Height(42f));
                    if (next && !selected)
                    {
                        SelectAssembly(assemblyIndex);
                    }
                }

                if (indices.Count > MaximumDisplayedRows)
                {
                    EditorGUILayout.HelpBox($"先頭 {MaximumDisplayedRows} / {indices.Count} 件を表示しています。", MessageType.Warning);
                }

                EditorGUILayout.EndScrollView();
            }

            return scrollPosition;
        }

        /// <summary>
        /// 選択 assembly に関係する問題一覧と詳細を左右へ描画します。
        /// </summary>
        private void DrawIssuesAndDetails(float height)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawIssueList(height);
                DrawAssemblyReferenceList(height);
                DrawDetails(height);
            }
        }

        /// <summary>
        /// 選択 assembly が発生元または関連先になっている問題を描画します。
        /// </summary>
        private void DrawIssueList(float height)
        {
            var issueIndices = GetSelectedIssueIndices();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(position.width * 0.29f)))
            {
                EditorGUILayout.LabelField($"Issues ({issueIndices.Count})", EditorStyles.boldLabel);
                _issuesScrollPosition = EditorGUILayout.BeginScrollView(_issuesScrollPosition, GUILayout.Height(height));
                var displayedCount = Math.Min(issueIndices.Count, MaximumDisplayedRows);
                if (displayedCount == 0)
                {
                    EditorGUILayout.LabelField("選択対象に関係する問題はありません。", _wrappedMiniLabelStyle);
                }

                for (var rowIndex = 0; rowIndex < displayedCount; rowIndex++)
                {
                    var issueIndex = issueIndices[rowIndex];
                    if (issueIndex < 0 || issueIndex >= _result.Issues.Count)
                    {
                        continue;
                    }

                    var issue = _result.Issues[issueIndex];
                    var selected = issueIndex == _selectedIssueIndex;
                    var content = new GUIContent(
                        $"{issue.Kind}\n{LimitText(issue.Message, MaximumIssueRowMessageCharacters)}",
                        issue.AssetPath);
                    var next = GUILayout.Toggle(selected, content, _rowStyle, GUILayout.Height(44f));
                    if (next && !selected)
                    {
                        _selectedIssueIndex = issueIndex;
                        _interactionMessage = string.Empty;
                    }
                }

                if (issueIndices.Count > MaximumDisplayedRows)
                {
                    EditorGUILayout.HelpBox($"先頭 {MaximumDisplayedRows} / {issueIndices.Count} 件を表示しています。", MessageType.Warning);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        /// <summary>
        /// 解決成否を問わず全asmref targetを独立した常設一覧へ描画します。
        /// </summary>
        private void DrawAssemblyReferenceList(float height)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(position.width * 0.34f)))
            {
                EditorGUILayout.LabelField(
                    $"Assembly References ({_visibleAssemblyReferenceIndices.Count} / {_result.AssemblyReferences.Count})",
                    EditorStyles.boldLabel);
                DrawAssemblyReferencePageControls();
                _assemblyReferencesScrollPosition = EditorGUILayout.BeginScrollView(
                    _assemblyReferencesScrollPosition,
                    GUILayout.Height(height));
                var pageStart = GetAssemblyReferencePageStart(
                    _assemblyReferencePage,
                    _visibleAssemblyReferenceIndices.Count);
                var displayedCount = Math.Min(
                    _visibleAssemblyReferenceIndices.Count - pageStart,
                    MaximumDisplayedRows);
                if (displayedCount == 0)
                {
                    EditorGUILayout.LabelField(
                        _result.AssemblyReferences.Count == 0 ? "asmref はありません。" : "filterに一致するasmrefはありません。",
                        _wrappedMiniLabelStyle);
                }

                for (var rowIndex = 0; rowIndex < displayedCount; rowIndex++)
                {
                    var index = _visibleAssemblyReferenceIndices[pageStart + rowIndex];
                    var target = _result.AssemblyReferences[index];
                    var selected = index == _selectedAssemblyReferenceIndex;
                    var content = new GUIContent(
                        FormatAssemblyReferenceRow(target),
                        FormatAssemblyReferenceTooltip(target));
                    var next = GUILayout.Toggle(selected, content, _assemblyReferenceRowStyle, GUILayout.Height(42f));
                    if (next && !selected)
                    {
                        SelectAssemblyReference(index);
                    }
                }

                if (_visibleAssemblyReferenceIndices.Count > MaximumDisplayedRows)
                {
                    EditorGUILayout.HelpBox(
                        $"{pageStart + 1}-{pageStart + displayedCount} / {_visibleAssemblyReferenceIndices.Count} 件を表示しています。",
                        MessageType.Warning);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        /// <summary>filtered asmref一覧のPrev・page番号・Nextを描画します。</summary>
        private void DrawAssemblyReferencePageControls()
        {
            var pageCount = GetAssemblyReferencePageCount(_visibleAssemblyReferenceIndices.Count);
            _assemblyReferencePage = ClampAssemblyReferencePage(_assemblyReferencePage, pageCount);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_assemblyReferencePage <= 0))
                {
                    if (GUILayout.Button("Prev", GUILayout.Width(48f)))
                    {
                        SetAssemblyReferencePage(_assemblyReferencePage - 1);
                    }
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(
                    $"Page {_assemblyReferencePage + 1} / {pageCount}",
                    EditorStyles.miniLabel,
                    GUILayout.Width(82f));
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(_assemblyReferencePage + 1 >= pageCount))
                {
                    if (GUILayout.Button("Next", GUILayout.Width(48f)))
                    {
                        SetAssemblyReferencePage(_assemblyReferencePage + 1);
                    }
                }
            }
        }

        /// <summary>
        /// 選択 assembly と問題の path、platform、参照件数、操作を描画します。
        /// </summary>
        private void DrawDetails(float height)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("Details", EditorStyles.boldLabel);
                _detailsScrollPosition = EditorGUILayout.BeginScrollView(_detailsScrollPosition, GUILayout.Height(height));
                var target = GetSelectedAssemblyReference();
                var node = GetSelectedNode();
                if (target == null && node == null)
                {
                    EditorGUILayout.LabelField("Assembly または asmref target を選択してください。", _wrappedMiniLabelStyle);
                    EditorGUILayout.EndScrollView();
                    return;
                }

                if (target != null)
                {
                    DrawDetailLine("Kind", target.Kind.ToString());
                    DrawDetailLine("Path", target.AssetPath);
                    DrawDetailLine("Reference", EmptyAsNone(target.RawReference));
                    DrawDetailLine("Target", EmptyAsUnresolved(target.ResolvedTargetAssetPath));
                    if (GUILayout.Button("Copy Reference"))
                    {
                        CopySelectedAssemblyReference();
                    }
                }
                else
                {
                    DrawDetailLine("Name", DisplayName(node));
                    DrawDetailLine("Path", node.AssetPath);
                    DrawDetailLine("GUID", node.Guid);
                    DrawDetailLine("Platform", node.IsEditorOnly ? "Editor Only" : "Player Capable");
                    DrawDetailLine("Include", JoinOrNone(node.IncludePlatforms));
                    DrawDetailLine("Exclude", JoinOrNone(node.ExcludePlatforms));
                    DrawDetailLine("Declared Refs", node.References.Count.ToString());
                    DrawDetailLine("Referenced By", _result.Dependents[_selectedAssemblyIndex].Count.ToString());
                    DrawDetailLine("Depends On", _result.Dependencies[_selectedAssemblyIndex].Count.ToString());

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Ping"))
                        {
                            PingSelectedAssembly();
                        }

                        if (GUILayout.Button("Open"))
                        {
                            OpenSelectedAssembly();
                        }

                        if (GUILayout.Button("Copy"))
                        {
                            CopySelectedAssembly();
                        }
                    }
                }

                var issue = GetSelectedIssue();
                if (issue != null)
                {
                    EditorGUILayout.Space(6f);
                    EditorGUILayout.LabelField("Issue Details", EditorStyles.boldLabel);
                    DrawDetailLine("Kind", issue.Kind.ToString());
                    DrawDetailLine("Path", issue.AssetPath);
                    DrawDetailLine("Related", EmptyAsNone(issue.RelatedAssetPath));
                    DrawDetailLine("Reference", EmptyAsNone(issue.Reference));
                    DrawDetailLine("Message", issue.Message);
                    if (GUILayout.Button("Copy Issue"))
                    {
                        CopySelectedIssue();
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        /// <summary>
        /// label と折り返し可能な値を 1 行として表示します。
        /// </summary>
        private void DrawDetailLine(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel, GUILayout.Width(84f));
                EditorGUILayout.LabelField(
                    LimitText(value, MaximumDisplayedTextCharacters),
                    _wrappedMiniLabelStyle,
                    GUILayout.ExpandWidth(true));
            }
        }

        /// <summary>
        /// 現在の project を再監査し、完全な結果だけへ置き換えます。
        /// </summary>
        private void RefreshAudit()
        {
            if (_service == null)
            {
                _service = new AssemblyDependencyAuditService();
            }

            var previouslySelectedPath = GetSelectedNode()?.AssetPath ?? string.Empty;
            var previouslySelectedAssemblyReferencePath = GetSelectedAssemblyReference()?.AssetPath ?? string.Empty;
            _auditErrorMessage = string.Empty;
            _interactionMessage = string.Empty;

            try
            {
                if (!_service.TryAudit(out var result, out var error, out var errorMessage))
                {
                    ClearAuditResult();
                    _auditErrorMessage = string.IsNullOrEmpty(errorMessage)
                        ? $"Refresh に失敗しました: {error}"
                        : $"Refresh に失敗しました: {error} - {errorMessage}";
                    return;
                }

                _result = result;
                RebuildResultCaches();
                RebuildVisibleAssemblyIndices(false);
                RestoreSelection(previouslySelectedPath);
                RestoreAssemblyReferenceSelection(previouslySelectedAssemblyReferencePath);
            }
            catch (Exception exception)
            {
                ClearAuditResult();
                _auditErrorMessage = $"Refresh に失敗しました: {exception.Message}";
            }

            Repaint();
        }

        /// <summary>
        /// 失敗後に古い結果を残さず、表示 cache を空にします。
        /// </summary>
        private void ClearAuditResult()
        {
            _result = null;
            _visibleAssemblyIndices.Clear();
            _visibleAssemblyReferenceIndices.Clear();
            _issuePaths.Clear();
            _issueIndicesByPath.Clear();
            _selectedAssemblyIndex = -1;
            _selectedIssueIndex = -1;
            _selectedAssemblyReferenceIndex = -1;
            _assemblyReferencePage = 0;
            _referenceCount = 0;
        }

        /// <summary>
        /// 問題 path、path ごとの問題 index、参照総数を構築します。
        /// </summary>
        private void RebuildResultCaches()
        {
            _issuePaths.Clear();
            _issueIndicesByPath.Clear();
            _referenceCount = 0;

            for (var assemblyIndex = 0; assemblyIndex < _result.Assemblies.Count; assemblyIndex++)
            {
                _referenceCount += _result.Assemblies[assemblyIndex].References.Count;
            }

            var issueIndicesByPath = BuildIssueIndicesByPath(_result);
            foreach (var pair in issueIndicesByPath)
            {
                _issuePaths.Add(pair.Key);
                _issueIndicesByPath.Add(pair.Key, pair.Value);
            }
        }

        /// <summary>
        /// 問題の発生元、関連先、循環 component 全 member を path ごとの問題 index へ変換します。
        /// </summary>
        internal static Dictionary<string, List<int>> BuildIssueIndicesByPath(AssemblyDependencyAuditResult result)
        {
            var issueIndicesByPath = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            if (result == null)
            {
                return issueIndicesByPath;
            }

            var cycleIssueIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var issueIndex = 0; issueIndex < result.Issues.Count; issueIndex++)
            {
                var issue = result.Issues[issueIndex];
                AddIssueIndex(issueIndicesByPath, issue.AssetPath, issueIndex);
                if (!string.Equals(issue.AssetPath, issue.RelatedAssetPath, StringComparison.Ordinal))
                {
                    AddIssueIndex(issueIndicesByPath, issue.RelatedAssetPath, issueIndex);
                }

                if (issue.Kind == AssemblyDependencyIssueKind.DependencyCycle)
                {
                    var key = CreateCycleIssueKey(issue.AssetPath, issue.RelatedAssetPath);
                    if (!cycleIssueIndices.ContainsKey(key))
                    {
                        cycleIssueIndices.Add(key, issueIndex);
                    }
                }
            }

            for (var cycleIndex = 0; cycleIndex < result.Cycles.Count; cycleIndex++)
            {
                var cycle = result.Cycles[cycleIndex];
                if (cycle.Count < 2 ||
                    cycle[0] < 0 || cycle[0] >= result.Assemblies.Count ||
                    cycle[1] < 0 || cycle[1] >= result.Assemblies.Count)
                {
                    continue;
                }

                var firstPath = result.Assemblies[cycle[0]].AssetPath;
                var secondPath = result.Assemblies[cycle[1]].AssetPath;
                if (!cycleIssueIndices.TryGetValue(CreateCycleIssueKey(firstPath, secondPath), out var issueIndex))
                {
                    continue;
                }

                for (var memberIndex = 0; memberIndex < cycle.Count; memberIndex++)
                {
                    var assemblyIndex = cycle[memberIndex];
                    if (assemblyIndex >= 0 && assemblyIndex < result.Assemblies.Count)
                    {
                        AddIssueIndex(issueIndicesByPath, result.Assemblies[assemblyIndex].AssetPath, issueIndex);
                    }
                }
            }

            foreach (var pair in issueIndicesByPath)
            {
                pair.Value.Sort();
                for (var issueIndex = pair.Value.Count - 1; issueIndex > 0; issueIndex--)
                {
                    if (pair.Value[issueIndex] == pair.Value[issueIndex - 1])
                    {
                        pair.Value.RemoveAt(issueIndex);
                    }
                }
            }

            return issueIndicesByPath;
        }

        /// <summary>
        /// 空でない path へ問題 index を追加します。並べ替えと重複除去は構築完了後に行います。
        /// </summary>
        private static void AddIssueIndex(Dictionary<string, List<int>> issueIndicesByPath, string assetPath, int issueIndex)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            if (!issueIndicesByPath.TryGetValue(assetPath, out var issueIndices))
            {
                issueIndices = new List<int>();
                issueIndicesByPath.Add(assetPath, issueIndices);
            }

            issueIndices.Add(issueIndex);
        }

        /// <summary>
        /// 循環問題の代表 2 path を衝突しない検索 key へ変換します。
        /// </summary>
        private static string CreateCycleIssueKey(string firstPath, string secondPath)
        {
            return (firstPath ?? string.Empty) + "\0" + (secondPath ?? string.Empty);
        }

        /// <summary>
        /// filter に一致する assembly index を元の asset path 順で構築します。
        /// </summary>
        private void RebuildVisibleAssemblyIndices(bool replaceSelection)
        {
            var hadSelectedAssemblyReference = _selectedAssemblyReferenceIndex >= 0;
            _visibleAssemblyIndices.Clear();
            _visibleAssemblyReferenceIndices.Clear();
            if (_result == null)
            {
                _selectedAssemblyIndex = -1;
                _selectedIssueIndex = -1;
                return;
            }

            if (replaceSelection)
            {
                _assemblyReferencePage = 0;
            }

            var normalizedSearch = NormalizeSearchText(_searchText);
            for (var assemblyIndex = 0; assemblyIndex < _result.Assemblies.Count; assemblyIndex++)
            {
                var node = _result.Assemblies[assemblyIndex];
                var hasIssue = _issuePaths.Contains(node.AssetPath);
                if (MatchesFiltersWithNormalizedSearch(
                        node,
                        normalizedSearch,
                        _scopeFilter,
                        _platformFilter,
                        _issueFilter,
                        hasIssue))
                {
                    _visibleAssemblyIndices.Add(assemblyIndex);
                }
            }

            for (var assemblyReferenceIndex = 0;
                assemblyReferenceIndex < _result.AssemblyReferences.Count;
                assemblyReferenceIndex++)
            {
                var target = _result.AssemblyReferences[assemblyReferenceIndex];
                var hasIssue = _issuePaths.Contains(target.AssetPath);
                if (MatchesAssemblyReferenceFiltersWithNormalizedSearch(
                        target,
                        normalizedSearch,
                        _scopeFilter,
                        _issueFilter,
                        hasIssue))
                {
                    _visibleAssemblyReferenceIndices.Add(assemblyReferenceIndex);
                }
            }

            if (replaceSelection && !_visibleAssemblyIndices.Contains(_selectedAssemblyIndex))
            {
                if (_visibleAssemblyReferenceIndices.Contains(_selectedAssemblyReferenceIndex))
                {
                    _selectedAssemblyIndex = -1;
                }
                else
                {
                    SelectAssembly(_visibleAssemblyIndices.Count == 0 ? -1 : _visibleAssemblyIndices[0]);
                }
            }

            if (replaceSelection &&
                hadSelectedAssemblyReference &&
                !_visibleAssemblyReferenceIndices.Contains(_selectedAssemblyReferenceIndex))
            {
                var replacementAssemblyIndex = _visibleAssemblyIndices.Contains(_selectedAssemblyIndex)
                    ? _selectedAssemblyIndex
                    : _visibleAssemblyIndices.Count == 0 ? -1 : _visibleAssemblyIndices[0];
                SelectAssembly(replacementAssemblyIndex);
            }

            _assemblyReferencePage = ClampAssemblyReferencePage(
                _assemblyReferencePage,
                GetAssemblyReferencePageCount(_visibleAssemblyReferenceIndices.Count));
            if (replaceSelection && _selectedAssemblyReferenceIndex >= 0)
            {
                var selectedVisibleIndex = _visibleAssemblyReferenceIndices.IndexOf(_selectedAssemblyReferenceIndex);
                if (selectedVisibleIndex < 0 || selectedVisibleIndex >= MaximumDisplayedRows)
                {
                    var replacementAssemblyIndex = _visibleAssemblyIndices.Contains(_selectedAssemblyIndex)
                        ? _selectedAssemblyIndex
                        : _visibleAssemblyIndices.Count == 0 ? -1 : _visibleAssemblyIndices[0];
                    SelectAssembly(replacementAssemblyIndex);
                }
            }

            Repaint();
        }

        /// <summary>
        /// assembly が検索、範囲、platform、問題 filter の全てへ一致するかを返します。
        /// </summary>
        internal static bool MatchesFilters(
            AssemblyDependencyNode node,
            string searchText,
            ScopeFilter scopeFilter,
            PlatformFilter platformFilter,
            IssueFilter issueFilter,
            bool hasIssue)
        {
            return MatchesFiltersWithNormalizedSearch(
                node,
                NormalizeSearchText(searchText),
                scopeFilter,
                platformFilter,
                issueFilter,
                hasIssue);
        }

        /// <summary>正規化済みsearchを使ってasmdef filterをallocationなしで評価します。</summary>
        private static bool MatchesFiltersWithNormalizedSearch(
            AssemblyDependencyNode node,
            string search,
            ScopeFilter scopeFilter,
            PlatformFilter platformFilter,
            IssueFilter issueFilter,
            bool hasIssue)
        {
            if (node == null)
            {
                return false;
            }

            if (search.Length > 0 &&
                !ContainsOrdinalIgnoreCase(node.Name, search) &&
                !ContainsOrdinalIgnoreCase(node.AssetPath, search) &&
                !ContainsOrdinalIgnoreCase(node.Guid, search))
            {
                return false;
            }

            if (scopeFilter == ScopeFilter.Assets && !IsPathUnder(node.AssetPath, "Assets"))
            {
                return false;
            }

            if (scopeFilter == ScopeFilter.Packages && !IsPathUnder(node.AssetPath, "Packages"))
            {
                return false;
            }

            if (platformFilter == PlatformFilter.PlayerCapable && node.IsEditorOnly)
            {
                return false;
            }

            if (platformFilter == PlatformFilter.EditorOnly && !node.IsEditorOnly)
            {
                return false;
            }

            if (issueFilter == IssueFilter.WithIssues && !hasIssue)
            {
                return false;
            }

            return issueFilter != IssueFilter.WithoutIssues || !hasIssue;
        }

        /// <summary>
        /// asmref targetがsearch、scope、issue filterの全てへ一致するかを返します。
        /// </summary>
        internal static bool MatchesAssemblyReferenceFilters(
            AssemblyReferenceTarget target,
            string searchText,
            ScopeFilter scopeFilter,
            IssueFilter issueFilter,
            bool hasIssue)
        {
            return MatchesAssemblyReferenceFiltersWithNormalizedSearch(
                target,
                NormalizeSearchText(searchText),
                scopeFilter,
                issueFilter,
                hasIssue);
        }

        /// <summary>正規化済みsearchを使ってasmref filterをallocationなしで評価します。</summary>
        private static bool MatchesAssemblyReferenceFiltersWithNormalizedSearch(
            AssemblyReferenceTarget target,
            string search,
            ScopeFilter scopeFilter,
            IssueFilter issueFilter,
            bool hasIssue)
        {
            if (target == null)
            {
                return false;
            }

            if (search.Length > 0 &&
                !ContainsOrdinalIgnoreCase(target.AssetPath, search) &&
                !ContainsOrdinalIgnoreCase(target.RawReference, search) &&
                !ContainsOrdinalIgnoreCase(target.ResolvedTargetAssetPath, search) &&
                !ContainsOrdinalIgnoreCase(target.Kind.ToString(), search))
            {
                return false;
            }

            if (scopeFilter == ScopeFilter.Assets && !IsPathUnder(target.AssetPath, "Assets"))
            {
                return false;
            }

            if (scopeFilter == ScopeFilter.Packages && !IsPathUnder(target.AssetPath, "Packages"))
            {
                return false;
            }

            if (issueFilter == IssueFilter.WithIssues && !hasIssue)
            {
                return false;
            }

            return issueFilter != IssueFilter.WithoutIssues || !hasIssue;
        }

        /// <summary>
        /// 前回選択 path が現在も表示対象なら復元し、なければ先頭を選びます。
        /// </summary>
        private void RestoreSelection(string assetPath)
        {
            var restoredIndex = -1;
            if (!string.IsNullOrEmpty(assetPath))
            {
                for (var visibleIndex = 0; visibleIndex < _visibleAssemblyIndices.Count; visibleIndex++)
                {
                    var assemblyIndex = _visibleAssemblyIndices[visibleIndex];
                    if (string.Equals(_result.Assemblies[assemblyIndex].AssetPath, assetPath, StringComparison.Ordinal))
                    {
                        restoredIndex = assemblyIndex;
                        break;
                    }
                }
            }

            SelectAssembly(restoredIndex >= 0
                ? restoredIndex
                : _visibleAssemblyIndices.Count == 0 ? -1 : _visibleAssemblyIndices[0]);
        }

        /// <summary>前回選択したasmref pathがあれば別一覧の選択を復元します。</summary>
        private void RestoreAssemblyReferenceSelection(string assetPath)
        {
            _selectedAssemblyReferenceIndex = -1;
            if (string.IsNullOrEmpty(assetPath) || _result == null)
            {
                return;
            }

            for (var visibleIndex = 0; visibleIndex < _visibleAssemblyReferenceIndices.Count; visibleIndex++)
            {
                var index = _visibleAssemblyReferenceIndices[visibleIndex];
                if (string.Equals(_result.AssemblyReferences[index].AssetPath, assetPath, StringComparison.Ordinal))
                {
                    _assemblyReferencePage = visibleIndex / MaximumDisplayedRows;
                    SelectAssemblyReference(index);
                    return;
                }
            }
        }

        /// <summary>
        /// 無効な選択を現在の filter の先頭へ移します。
        /// </summary>
        private void EnsureSelection()
        {
            if (GetSelectedAssemblyReference() == null &&
                !IsAssemblyIndexValid(_selectedAssemblyIndex) &&
                _visibleAssemblyIndices.Count > 0)
            {
                SelectAssembly(_visibleAssemblyIndices[0]);
            }
        }

        /// <summary>
        /// assembly を選択し、その assembly に関係する先頭の問題も選びます。
        /// </summary>
        private void SelectAssembly(int assemblyIndex)
        {
            _selectedAssemblyIndex = IsAssemblyIndexValid(assemblyIndex) ? assemblyIndex : -1;
            _selectedAssemblyReferenceIndex = -1;
            var issueIndices = GetSelectedIssueIndices();
            _selectedIssueIndex = issueIndices.Count == 0 ? -1 : issueIndices[0];
            _interactionMessage = string.Empty;
        }

        /// <summary>asmref targetを選択し、そのpathに関係する先頭の問題も選びます。</summary>
        private void SelectAssemblyReference(int assemblyReferenceIndex)
        {
            _selectedAssemblyReferenceIndex = IsAssemblyReferenceIndexValid(assemblyReferenceIndex)
                ? assemblyReferenceIndex
                : -1;
            var issueIndices = GetSelectedIssueIndices();
            _selectedIssueIndex = issueIndices.Count == 0 ? -1 : issueIndices[0];
            _interactionMessage = string.Empty;
        }

        /// <summary>asmref pageを変更し、非表示になったasmref選択とissue選択を同期します。</summary>
        private void SetAssemblyReferencePage(int page)
        {
            var pageCount = GetAssemblyReferencePageCount(_visibleAssemblyReferenceIndices.Count);
            _assemblyReferencePage = ClampAssemblyReferencePage(page, pageCount);
            var pageStart = GetAssemblyReferencePageStart(
                _assemblyReferencePage,
                _visibleAssemblyReferenceIndices.Count);
            var pageEnd = Math.Min(
                pageStart + MaximumDisplayedRows,
                _visibleAssemblyReferenceIndices.Count);
            var selectedVisibleIndex = _visibleAssemblyReferenceIndices.IndexOf(_selectedAssemblyReferenceIndex);
            if (selectedVisibleIndex < pageStart || selectedVisibleIndex >= pageEnd)
            {
                var replacementAssemblyIndex = _visibleAssemblyIndices.Contains(_selectedAssemblyIndex)
                    ? _selectedAssemblyIndex
                    : _visibleAssemblyIndices.Count == 0 ? -1 : _visibleAssemblyIndices[0];
                SelectAssembly(replacementAssemblyIndex);
            }

            _assemblyReferencesScrollPosition = Vector2.zero;
            Repaint();
        }

        /// <summary>
        /// 現在選択中の assembly を返します。
        /// </summary>
        private AssemblyDependencyNode GetSelectedNode()
        {
            return IsAssemblyIndexValid(_selectedAssemblyIndex)
                ? _result.Assemblies[_selectedAssemblyIndex]
                : null;
        }

        /// <summary>
        /// 現在選択中の問題を返します。
        /// </summary>
        private AssemblyDependencyIssue GetSelectedIssue()
        {
            return _result != null && _selectedIssueIndex >= 0 && _selectedIssueIndex < _result.Issues.Count
                ? _result.Issues[_selectedIssueIndex]
                : null;
        }

        /// <summary>現在選択中のasmref targetを返します。</summary>
        private AssemblyReferenceTarget GetSelectedAssemblyReference()
        {
            return IsAssemblyReferenceIndexValid(_selectedAssemblyReferenceIndex)
                ? _result.AssemblyReferences[_selectedAssemblyReferenceIndex]
                : null;
        }

        /// <summary>
        /// 選択 assembly に関係する問題 index を監査結果と同じ順序で返します。
        /// </summary>
        private IReadOnlyList<int> GetSelectedIssueIndices()
        {
            var target = GetSelectedAssemblyReference();
            if (target != null && _issueIndicesByPath.TryGetValue(target.AssetPath, out var targetIssueIndices))
            {
                return targetIssueIndices;
            }

            var node = GetSelectedNode();
            if (node != null && _issueIndicesByPath.TryGetValue(node.AssetPath, out var issueIndices))
            {
                return issueIndices;
            }

            return Array.Empty<int>();
        }

        /// <summary>
        /// 選択 asmdef を Project view で示します。
        /// </summary>
        private void PingSelectedAssembly()
        {
            var asset = LoadSelectedAsset();
            if (asset == null)
            {
                _interactionMessage = "選択中の asmdef asset を読み込めませんでした。";
                return;
            }

            EditorGUIUtility.PingObject(asset);
            _interactionMessage = $"Ping: {GetSelectedNode().AssetPath}";
        }

        /// <summary>
        /// 選択 asmdef を Unity の既定 editor で開きます。
        /// </summary>
        private void OpenSelectedAssembly()
        {
            var asset = LoadSelectedAsset();
            if (asset == null || !AssetDatabase.OpenAsset(asset))
            {
                _interactionMessage = "選択中の asmdef asset を開けませんでした。";
                return;
            }

            _interactionMessage = $"Open: {GetSelectedNode().AssetPath}";
        }

        /// <summary>
        /// 選択 assembly の詳細を clipboard へ copy します。
        /// </summary>
        private void CopySelectedAssembly()
        {
            var node = GetSelectedNode();
            if (node == null)
            {
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine($"Name: {DisplayName(node)}");
            builder.AppendLine($"Path: {node.AssetPath}");
            builder.AppendLine($"GUID: {node.Guid}");
            builder.AppendLine($"Platform: {(node.IsEditorOnly ? "Editor Only" : "Player Capable")}");
            builder.AppendLine($"Referenced By: {_result.Dependents[_selectedAssemblyIndex].Count}");
            builder.AppendLine($"Depends On: {_result.Dependencies[_selectedAssemblyIndex].Count}");
            EditorGUIUtility.systemCopyBuffer = builder.ToString().TrimEnd();
            _interactionMessage = "Assembly details を clipboard へ copy しました。";
        }

        /// <summary>
        /// 選択問題の詳細を clipboard へ copy します。
        /// </summary>
        private void CopySelectedIssue()
        {
            var issue = GetSelectedIssue();
            if (issue == null)
            {
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine($"Kind: {issue.Kind}");
            builder.AppendLine($"Path: {issue.AssetPath}");
            builder.AppendLine($"Related: {issue.RelatedAssetPath}");
            builder.AppendLine($"Reference: {issue.Reference}");
            builder.AppendLine($"Message: {issue.Message}");
            EditorGUIUtility.systemCopyBuffer = builder.ToString().TrimEnd();
            _interactionMessage = "Issue details を clipboard へ copy しました。";
        }

        /// <summary>選択asmref targetの詳細をclipboardへcopyします。</summary>
        private void CopySelectedAssemblyReference()
        {
            var target = GetSelectedAssemblyReference();
            if (target == null)
            {
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine($"Path: {target.AssetPath}");
            builder.AppendLine($"Reference: {target.RawReference}");
            builder.AppendLine($"Kind: {target.Kind}");
            builder.AppendLine($"Target: {target.ResolvedTargetAssetPath}");
            EditorGUIUtility.systemCopyBuffer = builder.ToString().TrimEnd();
            _interactionMessage = "asmref target details を clipboard へ copy しました。";
        }

        /// <summary>
        /// 選択 path から asmdef asset を読み込みます。
        /// </summary>
        private UnityEngine.Object LoadSelectedAsset()
        {
            var node = GetSelectedNode();
            return node == null ? null : AssetDatabase.LoadMainAssetAtPath(node.AssetPath);
        }

        /// <summary>
        /// assembly index が現在の結果に含まれるかを返します。
        /// </summary>
        private bool IsAssemblyIndexValid(int assemblyIndex)
        {
            return _result != null && assemblyIndex >= 0 && assemblyIndex < _result.Assemblies.Count;
        }

        /// <summary>asmref target indexが現在の結果に含まれるかを返します。</summary>
        private bool IsAssemblyReferenceIndexValid(int assemblyReferenceIndex)
        {
            return _result != null &&
                assemblyReferenceIndex >= 0 &&
                assemblyReferenceIndex < _result.AssemblyReferences.Count;
        }

        /// <summary>
        /// assembly の名前と path を一覧行へ整形します。
        /// </summary>
        private static string FormatAssemblyRow(AssemblyDependencyNode node)
        {
            return $"{LimitText(DisplayName(node), MaximumIssueRowMessageCharacters)}\n" +
                LimitText(node.AssetPath, MaximumIssueRowMessageCharacters);
        }

        /// <summary>asmrefの指定方法、解決状態、短縮したfile名を一覧行へ整形します。</summary>
        internal static string FormatAssemblyReferenceRow(AssemblyReferenceTarget target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            var status = target.IsResolved ? "Resolved" : "Unresolved";
            return $"{target.Kind}: {status}\n{TruncateForRow(GetLeafName(target.AssetPath))}";
        }

        /// <summary>asmrefの解決状態と上限付きpathを軽量なrow tooltipへ整形します。</summary>
        internal static string FormatAssemblyReferenceTooltip(AssemblyReferenceTarget target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            var status = target.IsResolved ? "Resolved" : "Unresolved";
            return $"{target.Kind}: {status}\n" +
                $"Path: {LimitText(target.AssetPath, MaximumAssemblyReferenceTooltipPathCharacters)}\n" +
                "Select to view Reference and Target.";
        }

        /// <summary>filtered件数から500件単位のpage数を返します。</summary>
        internal static int GetAssemblyReferencePageCount(int visibleCount)
        {
            return visibleCount <= 0
                ? 1
                : ((visibleCount - 1) / MaximumDisplayedRows) + 1;
        }

        /// <summary>pageを有効範囲へ制限します。</summary>
        internal static int ClampAssemblyReferencePage(int page, int pageCount)
        {
            var safePageCount = Math.Max(1, pageCount);
            return Math.Max(0, Math.Min(page, safePageCount - 1));
        }

        /// <summary>指定pageがfiltered一覧で開始するindexを返します。</summary>
        internal static int GetAssemblyReferencePageStart(int page, int visibleCount)
        {
            var pageCount = GetAssemblyReferencePageCount(visibleCount);
            return ClampAssemblyReferencePage(page, pageCount) * MaximumDisplayedRows;
        }

        /// <summary>
        /// 空名に読み取り可能な代替表示を付けます。
        /// </summary>
        private static string DisplayName(AssemblyDependencyNode node)
        {
            return string.IsNullOrEmpty(node.Name) ? "(名前なし)" : node.Name;
        }

        /// <summary>
        /// platform 一覧を表示用文字列へ変換します。
        /// </summary>
        private static string JoinOrNone(IReadOnlyList<string> values)
        {
            return values == null || values.Count == 0 ? "(none)" : string.Join(", ", values);
        }

        /// <summary>
        /// 空文字へ代替表示を付けます。
        /// </summary>
        private static string EmptyAsNone(string value)
        {
            return string.IsNullOrEmpty(value) ? "(none)" : value;
        }

        /// <summary>空の解決先へ未解決表示を付けます。</summary>
        private static string EmptyAsUnresolved(string value)
        {
            return string.IsNullOrEmpty(value) ? "(unresolved)" : value;
        }

        /// <summary>
        /// Ordinal 規則で大文字小文字を無視して部分一致を調べます。
        /// </summary>
        private static bool ContainsOrdinalIgnoreCase(string value, string search)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>searchをtrimし、評価上限へ切り詰めます。</summary>
        private static string NormalizeSearchText(string searchText)
        {
            return LimitSearchText(searchText?.Trim() ?? string.Empty);
        }

        /// <summary>search文字列を安全上限へ切り詰めます。</summary>
        private static string LimitSearchText(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= MaximumSearchCharacters)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, GetSurrogateSafePrefixLength(value, MaximumSearchCharacters));
        }

        /// <summary>Unity pathの末尾名を返します。</summary>
        private static string GetLeafName(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return "(pathなし)";
            }

            var separatorIndex = assetPath.LastIndexOf('/');
            return separatorIndex >= 0 && separatorIndex + 1 < assetPath.Length
                ? assetPath.Substring(separatorIndex + 1)
                : assetPath;
        }

        /// <summary>row表示用文字列を末尾ellipsis付きで制限します。</summary>
        private static string TruncateForRow(string value)
        {
            return LimitText(value, MaximumAssemblyReferenceRowNameCharacters);
        }

        /// <summary>表示文字列を末尾ellipsis付きで指定上限へ制限します。</summary>
        internal static string LimitText(string value, int maximumCharacters)
        {
            if (maximumCharacters <= 0 || string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.Length <= maximumCharacters)
            {
                return value;
            }

            if (maximumCharacters == 1)
            {
                return "…";
            }

            var prefixLength = GetSurrogateSafePrefixLength(value, maximumCharacters - 1);
            return value.Substring(0, prefixLength) + "…";
        }

        /// <summary>surrogate pairを分断しないprefix長を返します。</summary>
        private static int GetSurrogateSafePrefixLength(string value, int maximumCodeUnits)
        {
            var prefixLength = Math.Max(0, Math.Min(value?.Length ?? 0, maximumCodeUnits));
            if (value != null &&
                prefixLength > 0 &&
                prefixLength < value.Length &&
                char.IsHighSurrogate(value[prefixLength - 1]) &&
                char.IsLowSurrogate(value[prefixLength]))
            {
                prefixLength--;
            }

            return prefixLength;
        }

        /// <summary>
        /// path が指定 root 自体または配下にあるかを返します。
        /// </summary>
        private static bool IsPathUnder(string assetPath, string root)
        {
            return string.Equals(assetPath, root, StringComparison.Ordinal) ||
                assetPath.StartsWith(root + "/", StringComparison.Ordinal);
        }

        /// <summary>
        /// 一覧と補足表示に使う style を domain reload 後へ作ります。
        /// </summary>
        private void EnsureStyles()
        {
            if (_rowStyle == null)
            {
                _rowStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = true,
                    padding = new RectOffset(7, 5, 3, 3)
                };
            }

            if (_wrappedMiniLabelStyle == null)
            {
                _wrappedMiniLabelStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel);
            }

            if (_assemblyReferenceRowStyle == null)
            {
                _assemblyReferenceRowStyle = new GUIStyle(_rowStyle)
                {
                    wordWrap = false
                };
            }
        }
    }
}
