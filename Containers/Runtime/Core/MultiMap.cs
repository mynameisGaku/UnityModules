using System;
using System.Collections;
using System.Collections.Generic;

namespace Containers
{
    /// <summary>
    /// 1 つのキーに複数の値を持たせる辞書。
    /// <para>
    /// キーごとのリストは<b>使い回さない</b>。内部リストは <c>map[key]</c> で外に出る設計なので、
    /// プールに戻して再利用すると、削除をまたいで参照を持ち続けた呼び出し側が
    /// 気づかないうちに別のキーのバケットへ書き込むことになる。
    /// 確保を抑えたい場合は <see cref="RemoveKey"/> ではなく
    /// <c>map[key].Clear()</c> でバケットを空のまま残せばよい。
    /// </para>
    /// </summary>
    public sealed class MultiMap<TKey, TValue> : IEnumerable<KeyValuePair<TKey, FastList<TValue>>>
    {
        private readonly Dictionary<TKey, FastList<TValue>> _buckets;

        /// <summary>キーの比較方法を指定して構築する。</summary>
        public MultiMap(IEqualityComparer<TKey> comparer = null) =>
            _buckets = new Dictionary<TKey, FastList<TValue>>(comparer);

        /// <summary>異なるキーの数。値の総数ではない。</summary>
        public int KeyCount => _buckets.Count;

        /// <summary>登録されているキーの一覧。</summary>
        public Dictionary<TKey, FastList<TValue>>.KeyCollection Keys => _buckets.Keys;

        /// <summary>
        /// キーに紐づく値のリスト。null は返さないし例外も投げない。
        /// <para>
        /// 未登録のキーではその場でバケットを作って返す。共有の空リストを返す実装だと、
        /// <c>map[key].Add(value)</c> と書いた瞬間にその共有インスタンスが汚れ、
        /// 以後あらゆるキー・あらゆる <see cref="MultiMap{TKey,TValue}"/> が
        /// 「空でない空リスト」を返すようになる。読むだけの用途では
        /// <see cref="TryGetValues"/> を使えばバケットは作られない。
        /// </para>
        /// </summary>
        public FastList<TValue> this[TKey key]
        {
            get
            {
                if (_buckets.TryGetValue(key, out var list)) return list;

                list = new FastList<TValue>();
                _buckets[key] = list;
                return list;
            }
        }

        /// <summary>値を 1 つ追加する。キーが無ければ作る。</summary>
        public void Add(TKey key, in TValue value)
        {
            if (!_buckets.TryGetValue(key, out var list))
            {
                list = new FastList<TValue>();
                _buckets[key] = list;
            }

            list.Add(value);
        }

        /// <summary>まとめて追加する。</summary>
        public void AddRange(TKey key, IEnumerable<TValue> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            foreach (var value in values) Add(key, value);
        }

        /// <summary>キーに紐づくリストを取得する。未登録なら false。</summary>
        public bool TryGetValues(TKey key, out FastList<TValue> values) => _buckets.TryGetValue(key, out values);

        /// <summary>キーが登録されているか。</summary>
        public bool ContainsKey(TKey key) => _buckets.ContainsKey(key);

        /// <summary>キーに紐づく値の数。未登録なら 0。</summary>
        public int CountFor(TKey key) => _buckets.TryGetValue(key, out var list) ? list.Count : 0;

        /// <summary>値を 1 つ取り除く。最後の 1 つが消えるとキーごと消える。</summary>
        public bool Remove(TKey key, in TValue value)
        {
            if (!_buckets.TryGetValue(key, out var list)) return false;
            if (!list.Remove(value)) return false;

            if (list.Count == 0) RemoveKey(key);
            return true;
        }

        /// <summary>キーと、それに紐づく値を全て取り除く。</summary>
        public bool RemoveKey(TKey key) => _buckets.Remove(key);

        /// <summary>全て取り除く。</summary>
        public void Clear() => _buckets.Clear();

        /// <summary>全キーの値を合計した数。キー数に比例するコストがかかる。</summary>
        public int TotalCount()
        {
            var total = 0;
            foreach (var pair in _buckets) total += pair.Value.Count;
            return total;
        }

        /// <summary>(キー, 値リスト) の組を列挙する。</summary>
        public Dictionary<TKey, FastList<TValue>>.Enumerator GetEnumerator() => _buckets.GetEnumerator();

        IEnumerator<KeyValuePair<TKey, FastList<TValue>>> IEnumerable<KeyValuePair<TKey, FastList<TValue>>>.GetEnumerator() =>
            _buckets.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _buckets.GetEnumerator();
    }
}
