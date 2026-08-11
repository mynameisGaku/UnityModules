using System;
using System.Collections.Generic;

namespace Containers.Spatial
{
    /// <summary>
    /// 文字列から値への対応を持つ前方一致木。存在意義は「前方一致で引けること」そのもの。
    /// <para>
    /// 辞書は「このキーがあるか」には速く答えるが、「<c>spawn.</c> で始まるものは何か」には
    /// 全キーを舐めないと答えられない。デバッグコンソールの補完、ローカライズキーの一覧、
    /// アイテム検索欄、単語ゲームはどれも後者を必要とする。
    /// </para>
    /// </summary>
    public sealed class Trie<TValue>
    {
        private sealed class Node
        {
            public Dictionary<char, Node> Children;
            public TValue Value;
            public bool HasValue;
        }

        private readonly Node _root = new Node();
        private readonly bool _caseSensitive;

        /// <summary>大文字小文字の扱いを指定して作る。</summary>
        /// <param name="caseSensitive">true なら大文字小文字を区別する。</param>
        public Trie(bool caseSensitive = false) => _caseSensitive = caseSensitive;

        /// <summary>値が入っているキーの数。</summary>
        public int Count { get; private set; }

        /// <summary>値の読み書き。読み取りでキーが無ければ例外。</summary>
        public TValue this[string key]
        {
            get => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException(key);
            set => Set(key, value);
        }

        /// <summary>値を登録する。既にあれば上書きする。</summary>
        /// <param name="key">キー。空文字は不可。</param>
        /// <param name="value">値。</param>
        public void Set(string key, TValue value)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("キーは必須。", nameof(key));

            var node = _root;
            for (var i = 0; i < key.Length; i++)
            {
                var character = Normalize(key[i]);
                node.Children ??= new Dictionary<char, Node>();

                if (!node.Children.TryGetValue(character, out var child))
                {
                    child = new Node();
                    node.Children.Add(character, child);
                }

                node = child;
            }

            if (!node.HasValue) Count++;
            node.HasValue = true;
            node.Value = value;
        }

        /// <summary>完全一致で値を読む。</summary>
        /// <param name="key">キー。</param>
        /// <param name="value">読み取った値。</param>
        public bool TryGetValue(string key, out TValue value)
        {
            var node = FindNode(key);
            if (node == null || !node.HasValue)
            {
                value = default;
                return false;
            }

            value = node.Value;
            return true;
        }

        /// <summary>完全一致するキーがあるか。</summary>
        /// <param name="key">キー。</param>
        public bool ContainsKey(string key)
        {
            var node = FindNode(key);
            return node != null && node.HasValue;
        }

        /// <summary>この前置きで始まるキーが 1 つでもあるか。完全一致しなくてもよい。</summary>
        /// <param name="prefix">前置き。</param>
        public bool ContainsPrefix(string prefix) => FindNode(prefix) != null;

        /// <summary>値を取り除く。</summary>
        /// <param name="key">キー。</param>
        public bool Remove(string key)
        {
            var node = FindNode(key);
            if (node == null || !node.HasValue) return false;

            // 使われなくなった経路はそのまま残す。安上がりだし、次の登録で再利用される。
            node.HasValue = false;
            node.Value = default;
            Count--;
            return true;
        }

        /// <summary>全て捨てる。</summary>
        public void Clear()
        {
            _root.Children = null;
            _root.HasValue = false;
            Count = 0;
        }

        /// <summary>
        /// 前置きで始まるキーを辞書順に集める。空の前置きなら全件。
        /// </summary>
        /// <param name="prefix">前置き。</param>
        /// <param name="results">追記先。</param>
        /// <param name="limit">集める最大件数。</param>
        public void KeysWithPrefix(string prefix, FastList<string> results, int limit = int.MaxValue)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            var start = prefix == null ? _root : FindNode(prefix);
            if (start == null) return;

            Collect(start, prefix ?? string.Empty, results, null, limit);
        }

        /// <summary>前置きで始まるキーと値を辞書順に集める。</summary>
        /// <param name="prefix">前置き。</param>
        /// <param name="results">追記先。</param>
        /// <param name="limit">集める最大件数。</param>
        public void EntriesWithPrefix(string prefix, FastList<KeyValuePair<string, TValue>> results, int limit = int.MaxValue)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            var start = prefix == null ? _root : FindNode(prefix);
            if (start == null) return;

            Collect(start, prefix ?? string.Empty, null, results, limit);
        }

        /// <summary>
        /// 前置きで始まる全キーに共通する、最長の続き。
        /// シェルで曖昧なコマンドに対して Tab を押したときの挙動。
        /// </summary>
        /// <param name="prefix">前置き。</param>
        public string LongestCommonCompletion(string prefix)
        {
            var node = prefix == null ? _root : FindNode(prefix);
            if (node == null) return prefix;

            var builder = new System.Text.StringBuilder(prefix ?? string.Empty);

            while (!node.HasValue && node.Children != null && node.Children.Count == 1)
            {
                foreach (var pair in node.Children)
                {
                    builder.Append(pair.Key);
                    node = pair.Value;
                    break;
                }
            }

            return builder.ToString();
        }

        private void Collect(Node node, string prefix, FastList<string> keys, FastList<KeyValuePair<string, TValue>> entries, int limit)
        {
            // keys / entries はどちらか片方だけが渡される。両方 null なら集める先が無い。
            var reached = keys?.Count ?? entries?.Count ?? 0;
            if (keys == null && entries == null) return;
            if (reached >= limit) return;

            if (node.HasValue)
            {
                keys?.Add(prefix);
                entries?.Add(new KeyValuePair<string, TValue>(prefix, node.Value));
            }

            if (node.Children == null) return;

            // 辞書のハッシュ順ではなく文字順に並べる。補完候補の並びが毎回変わらないように。
            using var characters = TempList<char>.Rent();
            foreach (var pair in node.Children) characters.List.Add(pair.Key);
            characters.List.Sort();

            for (var i = 0; i < characters.List.Count; i++)
            {
                var character = characters.List[i];
                Collect(node.Children[character], prefix + character, keys, entries, limit);
            }
        }

        private Node FindNode(string key)
        {
            if (key == null) return null;

            var node = _root;
            for (var i = 0; i < key.Length; i++)
            {
                if (node.Children == null) return null;
                if (!node.Children.TryGetValue(Normalize(key[i]), out node)) return null;
            }

            return node;
        }

        private char Normalize(char character) => _caseSensitive ? character : char.ToLowerInvariant(character);
    }
}
