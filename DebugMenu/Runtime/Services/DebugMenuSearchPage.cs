using System;
using Containers;

namespace DebugMenu
{
    /// <summary>検索語の入力、候補表示、元の行への移動を 1 ページにまとめる。</summary>
    public sealed class DebugMenuSearchPage
    {
        private const int ResultLimit = 64;

        private readonly DebugMenuRoot _menu;
        private readonly DebugMenuSearch _search = new DebugMenuSearch();
        private readonly FastList<DebugSearchHit> _hits = new FastList<DebugSearchHit>();
        private readonly DebugSearchQuery _queryElement;

        private string _query = string.Empty;

        /// <summary>対象メニューを指定して検索ページを作る。</summary>
        /// <param name="menu">検索と移動の対象。</param>
        public DebugMenuSearchPage(DebugMenuRoot menu)
        {
            _menu = menu ?? throw new ArgumentNullException(nameof(menu));
            Page = new DebugPage("Search")
            {
                Description = "検索語を入力し、候補を決定すると元の行へ移動する。",
            };

            _queryElement = new DebugSearchQuery(this);
            RebuildIndex();
        }

        /// <summary>トップレベルへ登録する検索ページ。</summary>
        public DebugPage Page { get; }

        /// <summary>現在の検索語。</summary>
        public string Query => _query;

        /// <summary>現在表示している候補数。</summary>
        public int ResultCount => _hits.Count;

        /// <summary>検索語を入力する行。画面を開いた直後のフォーカスに使える。</summary>
        public DebugElement QueryElement => _queryElement;

        /// <summary>メニューの現在構成から索引を作り直す。</summary>
        public void RebuildIndex()
        {
            _search.Rebuild(_menu);
            RebuildResults();
        }

        /// <summary>検索語を設定して候補を更新する。</summary>
        /// <param name="query">空白なら案内行だけを表示する。</param>
        public void SetQuery(string query)
        {
            var next = query ?? string.Empty;
            if (string.Equals(_query, next, StringComparison.Ordinal)) return;

            _query = next;
            RebuildResults();
        }

        /// <summary>検索ページを開き、検索語の行へカーソルを合わせる。</summary>
        public void Open()
        {
            RebuildIndex();
            _menu.SetRootPage(Page);
            Page.FocusOn(_queryElement);
        }

        private void RebuildResults()
        {
            _hits.Clear();
            if (!string.IsNullOrWhiteSpace(_query)) _search.Query(_query, _hits, ResultLimit);

            Page.Root.ClearChildren();
            Page.Root.Add(_queryElement);

            if (string.IsNullOrWhiteSpace(_query))
            {
                Page.Root.Add(new DebugSearchMessage("検索語を入力してください"));
            }
            else if (_hits.Count == 0)
            {
                Page.Root.Add(new DebugSearchMessage("一致する項目はありません"));
            }
            else
            {
                for (var i = 0; i < _hits.Count; i++)
                {
                    var hit = _hits[i];
                    hit.Element.TryGetDisplayLabel(out var label);
                    Page.Root.Add(new DebugSearchResult(label, hit.Path, () => NavigateTo(hit)));
                }
            }

            Page.Invalidate();
            Page.FocusOn(_queryElement);
        }

        private void NavigateTo(in DebugSearchHit hit)
        {
            if (hit.Element == null || hit.Page == null) return;

            // 隠れている親を全て開き、移動後に対象行が可視行へ入るようにする。
            for (var parent = hit.Element.Parent; parent != null; parent = parent.Parent)
            {
                parent.IsExpanded = true;
            }

            var rootPage = hit.RootPage ?? hit.Page;
            _menu.SetRootPage(rootPage);
            if (!ReferenceEquals(rootPage, hit.Page)) _menu.PushPage(hit.Page);

            hit.Page.Invalidate();
            hit.Page.FocusOn(hit.Element);
        }

        /// <summary>検索語を直接入力する補助行。</summary>
        private sealed class DebugSearchQuery : DebugElement
        {
            private readonly DebugMenuSearchPage _owner;

            public DebugSearchQuery(DebugMenuSearchPage owner) : base("Search")
            {
                _owner = owner;
                IsExpandable = false;
                MarkerVisibility = DebugMarkerVisibility.Never;
            }

            public override bool CanTypeValue => true;

            public override bool IsSaveable => false;

            public override bool IsSearchable => false;

            public override string GetValueText() =>
                string.IsNullOrEmpty(_owner._query) ? "type to search" : _owner._query;

            public override string GetEditText() => _owner._query;

            public override bool CommitEditText(string text)
            {
                _owner.SetQuery(text);
                return true;
            }
        }

        /// <summary>候補が無い場合の案内行。</summary>
        private sealed class DebugSearchMessage : DebugElement
        {
            public DebugSearchMessage(string label) : base(label)
            {
                IsExpandable = false;
                MarkerVisibility = DebugMarkerVisibility.Never;
            }

            public override bool IsSaveable => false;

            public override bool IsSearchable => false;
        }

        /// <summary>決定すると元の行へ移動する候補行。</summary>
        private sealed class DebugSearchResult : DebugElement
        {
            private readonly Action _navigate;

            public DebugSearchResult(string label, string path, Action navigate) : base(label, path)
            {
                _navigate = navigate ?? throw new ArgumentNullException(nameof(navigate));
                IsExpandable = false;
                MarkerVisibility = DebugMarkerVisibility.Never;
            }

            public override bool IsSaveable => false;

            public override bool IsSearchable => false;

            public override void OnDecide() => _navigate();
        }
    }
}
