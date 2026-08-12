using System;
using System.Collections.Generic;

namespace DebugMenu
{
    /// <summary>検索で見つかった行 1 件。</summary>
    public readonly struct DebugSearchHit
    {
        /// <summary>その行が属するページ。</summary>
        public readonly DebugPage Page;

        /// <summary>その行へ到達する起点の最上位ページ。</summary>
        public readonly DebugPage RootPage;

        /// <summary>見つかった行。</summary>
        public readonly DebugElement Element;

        /// <summary>ページ名から辿った経路。表示に使う。</summary>
        public readonly string Path;

        /// <summary>ページ・行・経路を指定して作る。</summary>
        /// <param name="page">属するページ。</param>
        /// <param name="element">見つかった行。</param>
        /// <param name="path">表示用の経路。</param>
        public DebugSearchHit(DebugPage page, DebugElement element, string path)
            : this(page, page, element, path)
        {
        }

        /// <summary>起点ページ・所属ページ・行・経路を指定して作る。</summary>
        /// <param name="rootPage">最上位の起点ページ。</param>
        /// <param name="page">属するページ。</param>
        /// <param name="element">見つかった行。</param>
        /// <param name="path">表示用の経路。</param>
        public DebugSearchHit(DebugPage rootPage, DebugPage page, DebugElement element, string path)
        {
            RootPage = rootPage;
            Page = page;
            Element = element;
            Path = path;
        }
    }

    /// <summary>
    /// メニュー全体から行を名前で探す。
    /// <para>
    /// 索引は単語を辞書順で保持し、<b>語の先頭一致</b>で引く方式にしてあり、
    /// <c>spd</c> では当たらないが <c>speed</c> なら「Move Speed」に当たる。
    /// 部分一致にしないのは、項目が数百あるときに候補が絞れなくなるため。
    /// 語ごとに索引を張るので、後ろの語からでも引ける。
    /// </para>
    /// <para>
    /// 索引は行の追加・削除では自動更新されない。構成を変えたら
    /// <see cref="Rebuild"/> を呼ぶこと。毎フレーム張り直すと項目数に比例して重くなる。
    /// </para>
    /// </summary>
    public sealed class DebugMenuSearch
    {
        private const int MaxRebuildPasses = 2;

        /// <summary>完成した索引一式。参照を差し替えて半構築状態を公開しない。</summary>
        private sealed class SearchIndex
        {
            // 小文字化した単語ごとに、メニュー走査順の候補を保持する。
            public readonly Dictionary<string, List<DebugSearchHit>> Buckets =
                new Dictionary<string, List<DebugSearchHit>>(StringComparer.Ordinal);

            // 前方一致の開始位置を二分探索するための辞書順単語一覧。
            public readonly List<string> SortedWords = new List<string>();

            // 索引に載せた行数。語数ではない。
            public int IndexedCount;
        }

        private SearchIndex _index = new SearchIndex();

        // 索引作成中の再入要求を外側の処理へ渡す。
        private DebugMenuRoot _pendingRebuildMenu;

        // 利用側の取得処理から同じ索引作成へ再入したかを判定する。
        private bool _rebuilding;

        /// <summary>索引に載っている行の数（語ではなく行の数）。</summary>
        public int IndexedCount => _index.IndexedCount;

        /// <summary>メニュー全体を走査して索引を張り直す。</summary>
        /// <param name="menu">対象のメニュー。</param>
        public void Rebuild(DebugMenuRoot menu)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));
            if (_rebuilding)
            {
                _pendingRebuildMenu = menu;
                return;
            }

            _rebuilding = true;
            _pendingRebuildMenu = null;
            try
            {
                var targetMenu = menu;
                for (var pass = 0; pass < MaxRebuildPasses; pass++)
                {
                    // 完成するまで現在の索引を残し、再入したQueryへ途中状態を見せない。
                    _index = BuildIndex(targetMenu);

                    if (_pendingRebuildMenu == null || pass + 1 >= MaxRebuildPasses) break;

                    targetMenu = _pendingRebuildMenu;
                    _pendingRebuildMenu = null;
                }
            }
            finally
            {
                // 2世代目からの自己要求は、公開呼出しを必ず有限時間で返すため次回の明示Rebuildへ委ねる。
                _pendingRebuildMenu = null;
                _rebuilding = false;
            }
        }

        /// <summary>対象メニューから、公開前の索引一式を作る。</summary>
        private SearchIndex BuildIndex(DebugMenuRoot menu)
        {
            var index = new SearchIndex();

            // 索引作成中の利用側処理がページを追加しても、今回の走査範囲は開始時点へ固定する。
            var pages = new List<DebugPage>(menu.Pages);
            var topLevelPages = new HashSet<DebugPage>();
            for (var i = 0; i < pages.Count; i++)
            {
                if (pages[i] != null) topLevelPages.Add(pages[i]);
            }

            var visited = new HashSet<DebugPage>();
            for (var i = 0; i < pages.Count; i++)
            {
                var rootPage = pages[i];
                IndexPage(index, rootPage, rootPage, rootPage.Name, visited, topLevelPages);
            }

            index.SortedWords.AddRange(index.Buckets.Keys);
            index.SortedWords.Sort(StringComparer.Ordinal);
            return index;
        }

        /// <summary>
        /// 語の先頭が一致する行を集める。空文字を渡すと何も返さない
        /// （全件を返すと「絞り込み」にならないため）。
        /// </summary>
        /// <param name="query">探す語。</param>
        /// <param name="results">追記先。</param>
        /// <param name="limit">追記前の要素を含む最大件数。</param>
        public void Query(string query, ICollection<DebugSearchHit> results, int limit = 64)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (string.IsNullOrWhiteSpace(query)) return;
            if (results.Count >= limit) return;

            var prefix = query.Trim().ToLowerInvariant();
            var seen = new HashSet<DebugElement>();
            foreach (var existing in results)
            {
                if (existing.Element != null) seen.Add(existing.Element);
            }

            var index = _index;
            for (var i = FindFirstWord(index, prefix); i < index.SortedWords.Count; i++)
            {
                var word = index.SortedWords[i];
                if (!word.StartsWith(prefix, StringComparison.Ordinal)) break;

                var bucket = index.Buckets[word];
                for (var j = 0; j < bucket.Count && results.Count < limit; j++)
                {
                    var hit = bucket[j];
                    if (!seen.Add(hit.Element)) continue;

                    results.Add(hit);
                }

                if (results.Count >= limit) break;
            }
        }

        /// <summary>索引を捨てる。</summary>
        public void Clear()
        {
            _index = new SearchIndex();
        }

        private static void IndexWords(SearchIndex index, string label, in DebugSearchHit hit)
        {
            if (string.IsNullOrEmpty(label)) return;

            var lowered = label.ToLowerInvariant();

            // 語の区切りで分けて、それぞれの先頭から引けるようにする。
            var start = 0;
            for (var i = 0; i <= lowered.Length; i++)
            {
                var isBoundary = i == lowered.Length || IsSeparator(lowered[i]);
                if (!isBoundary) continue;

                if (i > start) AddToIndex(index, lowered.Substring(start, i - start), hit);
                start = i + 1;
            }
        }

        private static void AddToIndex(SearchIndex index, string word, in DebugSearchHit hit)
        {
            if (!index.Buckets.TryGetValue(word, out var bucket))
            {
                bucket = new List<DebugSearchHit>();
                index.Buckets.Add(word, bucket);
            }

            bucket.Add(hit);
        }

        /// <summary>指定した語以上になる最初の索引位置を返す。</summary>
        /// <param name="prefix">小文字化済みの検索語。</param>
        private static int FindFirstWord(SearchIndex index, string prefix)
        {
            var lower = 0;
            var upper = index.SortedWords.Count;
            while (lower < upper)
            {
                var middle = lower + ((upper - lower) / 2);
                if (string.CompareOrdinal(index.SortedWords[middle], prefix) < 0) lower = middle + 1;
                else upper = middle;
            }

            return lower;
        }

        private static bool IsSeparator(char character) =>
            character == ' ' || character == '_' || character == '.' || character == '/' || character == '-';

        private void IndexPage(
            SearchIndex index,
            DebugPage rootPage,
            DebugPage page,
            string path,
            HashSet<DebugPage> visited,
            HashSet<DebugPage> topLevelPages)
        {
            if (page == null || !visited.Add(page)) return;

            IndexChildren(index, rootPage, page, page.Root, path, visited, topLevelPages);
        }

        private void IndexElement(
            SearchIndex index,
            DebugPage rootPage,
            DebugPage page,
            DebugElement element,
            string parentPath,
            HashSet<DebugPage> visited,
            HashSet<DebugPage> topLevelPages)
        {
            if (element == null) return;

            element.TryGetDisplayLabel(out var label);
            var path = string.IsNullOrEmpty(parentPath) || string.IsNullOrEmpty(label)
                ? parentPath
                : parentPath + " / " + label;

            // 見出しや区切り、検索UI自身は候補にしない。子は引き続き辿る。
            if (!(element is DebugGroup) &&
                !(element is DebugSeparator) &&
                TryGetSearchable(element, out var searchable) &&
                searchable)
            {
                var hit = new DebugSearchHit(rootPage, page, element, parentPath);
                index.IndexedCount++;
                IndexWords(index, label, hit);
            }

            if (element is DebugPageLink link)
            {
                // トップレベルにも登録されているページは、自身を起点にした経路を正とする。
                if (!topLevelPages.Contains(link.Target))
                    IndexPage(index, rootPage, link.Target, path, visited, topLevelPages);
                return;
            }

            IndexChildren(index, rootPage, page, element, path, visited, topLevelPages);
        }

        /// <summary>所有している子行だけを、独自行の例外を枝単位で隔離しながら辿る。</summary>
        private void IndexChildren(
            SearchIndex index,
            DebugPage rootPage,
            DebugPage page,
            DebugElement parent,
            string path,
            HashSet<DebugPage> visited,
            HashSet<DebugPage> topLevelPages)
        {
            IReadOnlyList<DebugElement> children;
            try
            {
                children = parent.Children;
            }
            catch (Exception exception)
            {
                parent.ReportReadError("検索構造取得", exception);
                return;
            }

            int count;
            try
            {
                count = children.Count;
            }
            catch (Exception exception)
            {
                parent.ReportReadError("検索構造取得", exception);
                return;
            }

            var failed = false;
            for (var i = 0; i < count; i++)
            {
                DebugElement child;
                try
                {
                    child = children[i];
                }
                catch (Exception exception)
                {
                    parent.ReportReadError("検索構造取得", exception);
                    failed = true;
                    continue;
                }

                // お気に入り・最近項目・検索結果に並ぶ借用行は、元の所有経路でだけ索引化する。
                if (child == null || !ReferenceEquals(child.Parent, parent)) continue;

                IndexElement(index, rootPage, page, child, path, visited, topLevelPages);
            }

            if (!failed) parent.ClearReadError("検索構造取得");
        }

        /// <summary>独自行の検索対象判定を例外境界の内側で読む。</summary>
        private static bool TryGetSearchable(DebugElement element, out bool searchable)
        {
            try
            {
                searchable = element.IsSearchable;
                element.ClearReadError("検索対象確認");
                return true;
            }
            catch (Exception exception)
            {
                searchable = false;
                element.ReportReadError("検索対象確認", exception);
                return false;
            }
        }
    }
}
