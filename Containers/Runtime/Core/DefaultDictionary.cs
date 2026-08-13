using System;
using System.Collections;
using System.Collections.Generic;

namespace Containers
{
    /// <summary>
    /// 未登録のキーを読むと例外ではなく値が生成される辞書。
    /// <c>TryGetValue</c> → <c>new</c> → <c>Add</c> という定型 3 行が、添字 1 回に潰れる。
    /// <code>
    /// var byTeam = new DefaultDictionary&lt;Team, FastList&lt;Unit&gt;&gt;();
    /// byTeam[unit.Team].Add(unit);   // null 判定も事前登録も要らない
    /// </code>
    /// </summary>
    public sealed class DefaultDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        private readonly Dictionary<TKey, TValue> _map;
        private readonly Func<TKey, TValue> _factory;

        /// <summary>未登録キーに対して <c>new TValue()</c> を使う版。</summary>
        /// <param name="comparer">キーの比較方法。</param>
        public DefaultDictionary(IEqualityComparer<TKey> comparer = null)
            : this(_ => Activator.CreateInstance<TValue>(), comparer) { }

        /// <summary>生成方法を指定して作る。</summary>
        /// <param name="factory">未登録キーから値を作る関数。</param>
        /// <param name="comparer">キーの比較方法。</param>
        public DefaultDictionary(Func<TKey, TValue> factory, IEqualityComparer<TKey> comparer = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _map = new Dictionary<TKey, TValue>(comparer);
        }

        /// <summary>要素数。</summary>
        public int Count => _map.Count;

        /// <summary>キーの一覧。</summary>
        public Dictionary<TKey, TValue>.KeyCollection Keys => _map.Keys;

        /// <summary>値の一覧。</summary>
        public Dictionary<TKey, TValue>.ValueCollection Values => _map.Values;

        /// <summary>未登録のキーを読むと、値が生成・登録されてから返る。</summary>
        public TValue this[TKey key]
        {
            get
            {
                if (_map.TryGetValue(key, out var value)) return value;

                value = _factory(key);
                _map[key] = value;
                return value;
            }
            set => _map[key] = value;
        }

        /// <summary>生成せずに読む。「無い」を「無い」のまま扱いたいときに使う。</summary>
        /// <param name="key">キー。</param>
        /// <param name="value">読み取った値。</param>
        public bool TryGetValue(TKey key, out TValue value) => _map.TryGetValue(key, out value);

        /// <summary>キーが登録済みか。</summary>
        /// <param name="key">キー。</param>
        public bool ContainsKey(TKey key) => _map.ContainsKey(key);

        /// <summary>エントリを削除する。</summary>
        /// <param name="key">キー。</param>
        public bool Remove(TKey key) => _map.Remove(key);

        /// <summary>全て捨てる。</summary>
        public void Clear() => _map.Clear();

        /// <summary>実体の辞書。具体型を要求する API に渡すとき用。</summary>
        public Dictionary<TKey, TValue> AsDictionary() => _map;

        /// <summary>(キー, 値) を走査する構造体列挙子を返す。</summary>
        public Dictionary<TKey, TValue>.Enumerator GetEnumerator() => _map.GetEnumerator();

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => _map.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _map.GetEnumerator();
    }
}
