using System;
using System.Collections.Generic;
using Containers;
using Containers.Spatial;

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
    /// 索引は <see cref="Trie{TValue}"/>。<b>語の先頭一致</b>で引く方式にしてあり、
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
        private readonly Trie<FastList<DebugSearchHit>> _index = new Trie<FastList<DebugSearchHit>>();

        /// <summary>索引に載っている行の数（語ではなく行の数）。</summary>
        public int IndexedCount { get; private set; }

        /// <summary>メニュー全体を走査して索引を張り直す。</summary>
        /// <param name="menu">対象のメニュー。</param>
        public void Rebuild(DebugMenuRoot menu)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));

            _index.Clear();
            IndexedCount = 0;

            var visited = new HashSet<DebugPage>();
            var pages = menu.Pages;
            for (var i = 0; i < pages.Count; i++)
            {
                var rootPage = pages[i];
                IndexPage(rootPage, rootPage, rootPage.Name, visited);
            }
        }

        /// <summary>
        /// 語の先頭が一致する行を集める。空文字を渡すと何も返さない
        /// （全件を返すと「絞り込み」にならないため）。
        /// </summary>
        /// <param name="query">探す語。</param>
        /// <param name="results">追記先。</param>
        /// <param name="limit">集める最大件数。</param>
        public void Query(string query, FastList<DebugSearchHit> results, int limit = 64)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (string.IsNullOrWhiteSpace(query)) return;

            using var buckets = TempList<FastList<DebugSearchHit>>.Rent();
            using var entries = TempList<System.Collections.Generic.KeyValuePair<string, FastList<DebugSearchHit>>>.Rent();

            _index.EntriesWithPrefix(query.Trim().ToLowerInvariant(), entries.List, limit);

            // 同じ行が複数の語から当たることがあるので、重複を落とす。
            using var seen = TempList<DebugElement>.Rent();

            for (var i = 0; i < entries.List.Count && results.Count < limit; i++)
            {
                var bucket = entries.List[i].Value;

                for (var j = 0; j < bucket.Count && results.Count < limit; j++)
                {
                    var hit = bucket[j];
                    if (seen.List.Contains(hit.Element)) continue;

                    seen.List.Add(hit.Element);
                    results.Add(hit);
                }
            }
        }

        /// <summary>索引を捨てる。</summary>
        public void Clear()
        {
            _index.Clear();
            IndexedCount = 0;
        }

        private void IndexWords(string label, in DebugSearchHit hit)
        {
            if (string.IsNullOrEmpty(label)) return;

            var lowered = label.ToLowerInvariant();

            // 語の区切りで分けて、それぞれの先頭から引けるようにする。
            var start = 0;
            for (var i = 0; i <= lowered.Length; i++)
            {
                var isBoundary = i == lowered.Length || IsSeparator(lowered[i]);
                if (!isBoundary) continue;

                if (i > start) AddToIndex(lowered.Substring(start, i - start), hit);
                start = i + 1;
            }
        }

        private void AddToIndex(string word, in DebugSearchHit hit)
        {
            if (!_index.TryGetValue(word, out var bucket))
            {
                bucket = new FastList<DebugSearchHit>();
                _index.Set(word, bucket);
            }

            bucket.Add(hit);
        }

        private static bool IsSeparator(char character) =>
            character == ' ' || character == '_' || character == '.' || character == '/' || character == '-';

        private void IndexPage(DebugPage rootPage, DebugPage page, string path, HashSet<DebugPage> visited)
        {
            if (page == null || !visited.Add(page)) return;

            var children = page.Root.Children;
            for (var i = 0; i < children.Count; i++)
            {
                IndexElement(rootPage, page, children[i], path, visited);
            }
        }

        private void IndexElement(
            DebugPage rootPage,
            DebugPage page,
            DebugElement element,
            string parentPath,
            HashSet<DebugPage> visited)
        {
            if (element == null) return;

            var label = element.DisplayLabel ?? string.Empty;
            var path = string.IsNullOrEmpty(parentPath) || string.IsNullOrEmpty(label)
                ? parentPath
                : parentPath + " / " + label;

            // 見出しや区切り、検索UI自身は候補にしない。子は引き続き辿る。
            if (!(element is DebugGroup) && !(element is DebugSeparator) && element.IsSearchable)
            {
                var hit = new DebugSearchHit(rootPage, page, element, parentPath);
                IndexedCount++;
                IndexWords(label, hit);
            }

            if (element is DebugPageLink link)
            {
                IndexPage(rootPage, link.Target, path, visited);
                return;
            }

            var children = element.Children;
            for (var i = 0; i < children.Count; i++)
            {
                IndexElement(rootPage, page, children[i], path, visited);
            }
        }
    }
}
