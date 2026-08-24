using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AssemblyDependencyAudit.Editor
{
    /// <summary>
    /// asmdef の参照元、一覧、参照先と検出問題を読み取り専用で表示します。
    /// </summary>
    internal sealed class AssemblyDependencyAuditWindow : EditorWindow
    {
        /// <summary>1 つの一覧へ描画する最大行数です。</summary>
        internal const int MaximumDisplayedRows = 500;

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

        /// <summary>詳細欄の縦方向位置です。</summary>
        [SerializeField] private Vector2 _detailsScrollPosition;

        /// <summary>現在の project を読み取る監査処理です。</summary>
        private AssemblyDependencyAuditService _service;

        /// <summary>最後に完全に成功した監査結果です。</summary>
        private AssemblyDependencyAuditResult _result;

        /// <summary>現在の filter に一致する assembly index です。</summary>
        private readonly List<int> _visibleAssemblyIndices = new List<int>();

        /// <summary>問題へ直接または関連先として含まれる path です。</summary>
        private readonly HashSet<string> _issuePaths = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>path ごとに関係する問題 index を保持します。</summary>
        private readonly Dictionary<string, List<int>> _issueIndicesByPath = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        /// <summary>中央または左右の列で選択した assembly index です。</summary>
        private int _selectedAssemblyIndex = -1;

        /// <summary>問題一覧で選択した問題 index です。</summary>
        private int _selectedIssueIndex = -1;

        /// <summary>監査に失敗した理由です。</summary>
        private string _auditErrorMessage = string.Empty;

        /// <summary>Ping、Open、Copy の実行結果です。</summary>
        private string _interactionMessage = string.Empty;

        /// <summary>監査結果に含まれる参照総数です。</summary>
        private int _referenceCount;

        /// <summary>一覧行を描画する style です。</summary>
        private GUIStyle _rowStyle;

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
                "Assets と Packages の asmdef を読み取り専用で監査します。Refresh、Ping、Open、Copy は project file を変更しません。",
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
                _searchText = EditorGUILayout.TextField("Search", _searchText, EditorStyles.toolbarSearchField, GUILayout.MinWidth(180f));
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
                $"Assemblies {_result.Assemblies.Count} / References {_referenceCount} / Issues {_result.Issues.Count} / Filtered {_visibleAssemblyIndices.Count}",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"表示上限: 各一覧 {MaximumDisplayedRows} 件。超過分は Search と filter で絞り込んでください。",
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
                    var next = GUILayout.Toggle(selected, content, _rowStyle, GUILayout.Height(42f));
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
                DrawDetails(height);
            }
        }

        /// <summary>
        /// 選択 assembly が発生元または関連先になっている問題を描画します。
        /// </summary>
        private void DrawIssueList(float height)
        {
            var issueIndices = GetSelectedIssueIndices();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(position.width * 0.44f)))
            {
                EditorGUILayout.LabelField($"Issues ({issueIndices.Count})", EditorStyles.boldLabel);
                _issuesScrollPosition = EditorGUILayout.BeginScrollView(_issuesScrollPosition, GUILayout.Height(height));
                var displayedCount = Math.Min(issueIndices.Count, MaximumDisplayedRows);
                if (displayedCount == 0)
                {
                    EditorGUILayout.LabelField("選択中の assembly に関係する問題はありません。", _wrappedMiniLabelStyle);
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
                    var content = new GUIContent($"{issue.Kind}\n{issue.Message}", issue.AssetPath);
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
        /// 選択 assembly と問題の path、platform、参照件数、操作を描画します。
        /// </summary>
        private void DrawDetails(float height)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("Details", EditorStyles.boldLabel);
                _detailsScrollPosition = EditorGUILayout.BeginScrollView(_detailsScrollPosition, GUILayout.Height(height));
                var node = GetSelectedNode();
                if (node == null)
                {
                    EditorGUILayout.LabelField("Assembly を選択してください。", _wrappedMiniLabelStyle);
                    EditorGUILayout.EndScrollView();
                    return;
                }

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
                EditorGUILayout.LabelField(value, _wrappedMiniLabelStyle, GUILayout.ExpandWidth(true));
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
            _issuePaths.Clear();
            _issueIndicesByPath.Clear();
            _selectedAssemblyIndex = -1;
            _selectedIssueIndex = -1;
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
            _visibleAssemblyIndices.Clear();
            if (_result == null)
            {
                _selectedAssemblyIndex = -1;
                _selectedIssueIndex = -1;
                return;
            }

            for (var assemblyIndex = 0; assemblyIndex < _result.Assemblies.Count; assemblyIndex++)
            {
                var node = _result.Assemblies[assemblyIndex];
                var hasIssue = _issuePaths.Contains(node.AssetPath);
                if (MatchesFilters(node, _searchText, _scopeFilter, _platformFilter, _issueFilter, hasIssue))
                {
                    _visibleAssemblyIndices.Add(assemblyIndex);
                }
            }

            if (replaceSelection && !_visibleAssemblyIndices.Contains(_selectedAssemblyIndex))
            {
                SelectAssembly(_visibleAssemblyIndices.Count == 0 ? -1 : _visibleAssemblyIndices[0]);
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
            if (node == null)
            {
                return false;
            }

            var search = searchText?.Trim() ?? string.Empty;
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

        /// <summary>
        /// 無効な選択を現在の filter の先頭へ移します。
        /// </summary>
        private void EnsureSelection()
        {
            if (!IsAssemblyIndexValid(_selectedAssemblyIndex))
            {
                SelectAssembly(_visibleAssemblyIndices.Count == 0 ? -1 : _visibleAssemblyIndices[0]);
            }
        }

        /// <summary>
        /// assembly を選択し、その assembly に関係する先頭の問題も選びます。
        /// </summary>
        private void SelectAssembly(int assemblyIndex)
        {
            _selectedAssemblyIndex = IsAssemblyIndexValid(assemblyIndex) ? assemblyIndex : -1;
            var issueIndices = GetSelectedIssueIndices();
            _selectedIssueIndex = issueIndices.Count == 0 ? -1 : issueIndices[0];
            _interactionMessage = string.Empty;
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

        /// <summary>
        /// 選択 assembly に関係する問題 index を監査結果と同じ順序で返します。
        /// </summary>
        private IReadOnlyList<int> GetSelectedIssueIndices()
        {
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

        /// <summary>
        /// assembly の名前と path を一覧行へ整形します。
        /// </summary>
        private static string FormatAssemblyRow(AssemblyDependencyNode node)
        {
            return $"{DisplayName(node)}\n{node.AssetPath}";
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

        /// <summary>
        /// Ordinal 規則で大文字小文字を無視して部分一致を調べます。
        /// </summary>
        private static bool ContainsOrdinalIgnoreCase(string value, string search)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
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
        }
    }
}
