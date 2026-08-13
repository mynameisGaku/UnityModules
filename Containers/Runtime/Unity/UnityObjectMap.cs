using System;
using System.Collections;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace Containers
{
    /// <summary>
    /// <see cref="UnityEngine.Object"/> をキーにできる辞書。破棄済みのキーを正しく扱う。
    /// <para>
    /// このパッケージで最も Unity 固有のコンテナで、実在する罠のために存在する。
    /// 破棄済みの Unity オブジェクトは多重定義された <c>==</c> で <c>null</c> と等しくなるが、
    /// マネージド参照としては生きていてハッシュ値も返す。素の
    /// <see cref="Dictionary{TKey,TValue}"/> に入れると、<c>key == null</c> が真なので
    /// <b>誰も引けないのに永久に回収されないエントリ</b>ができ、値の側のオブジェクトごと道連れにする。
    /// GameObject をキーにした長命なキャッシュは、まさにこの形で漏れる。
    /// </para>
    /// <para>
    /// ここでは破棄済みキーを常に「無い」として扱い、溜まってきたら自動で掃除する。
    /// 明示的に掃除したいときは <see cref="Compact"/> を呼ぶ。
    /// </para>
    /// </summary>
    public sealed class UnityObjectMap<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
        where TKey : Object
    {
        private readonly Dictionary<TKey, TValue> _map;
        private readonly List<TKey> _sweepBuffer = new List<TKey>();

        private int _operationsSinceSweep;

        /// <summary>掃除の最中か。<see cref="Orphaned"/> ハンドラからの再入を止めるために見る。</summary>
        private bool _sweeping;

        /// <summary>自動掃除を挟むまでの読み書き回数。小さくするほど綺麗だが処理が増える。</summary>
        public int SweepInterval { get; set; } = 128;

        /// <summary>キーが破棄されたために捨てられた値を通知する。</summary>
        public event Action<TValue> Orphaned;

        /// <summary>容量を指定して作る。</summary>
        /// <param name="capacity">最初に確保するエントリ数。</param>
        public UnityObjectMap(int capacity = 0) => _map = new Dictionary<TKey, TValue>(capacity);

        /// <summary>
        /// まだ掃除していない破棄済みキーを含むエントリ数。
        /// 正確な数が要るなら <see cref="LiveCount"/> を使う。
        /// </summary>
        public int Count => _map.Count;

        /// <summary>キーが生きているエントリの数。全件走査するので O(n)。</summary>
        public int LiveCount
        {
            get
            {
                var live = 0;
                foreach (var pair in _map)
                {
                    if (pair.Key != null) live++;
                }

                return live;
            }
        }

        /// <summary>値の読み書き。破棄済みキーでの読み取りは例外になる。</summary>
        public TValue this[TKey key]
        {
            get => TryGetValue(key, out var value)
                ? value
                : throw new KeyNotFoundException("キーが存在しないか、既に破棄されている。");
            set => Set(key, value);
        }

        /// <summary>値を設定する。既にあれば上書きする。</summary>
        /// <param name="key">キー。破棄済みや null は不可。</param>
        /// <param name="value">値。</param>
        public void Set(TKey key, TValue value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key), "キーが null か破棄済み。");

            _map[key] = value;
            Tick();
        }

        /// <summary>値を追加する。既にあれば例外。</summary>
        /// <param name="key">キー。破棄済みや null は不可。</param>
        /// <param name="value">値。</param>
        public void Add(TKey key, TValue value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key), "キーが null か破棄済み。");

            _map.Add(key, value);
            Tick();
        }

        /// <summary>値を読む。破棄済みキーは古い値を返さず「無い」と答える。</summary>
        /// <param name="key">キー。</param>
        /// <param name="value">見つかった値。</param>
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (key == null)
            {
                value = default;
                return false;
            }

            Tick();
            return _map.TryGetValue(key, out value);
        }

        /// <summary>生きているキーとして登録されているか。</summary>
        /// <param name="key">キー。</param>
        public bool ContainsKey(TKey key) => key != null && _map.ContainsKey(key);

        /// <summary>エントリを削除する。破棄済みのキーでも削除できるよう参照同一性で判定する。</summary>
        /// <param name="key">キー。</param>
        public bool Remove(TKey key) => !ReferenceEquals(key, null) && _map.Remove(key);

        /// <summary>無ければ作って登録し、その値を返す。</summary>
        /// <param name="key">キー。</param>
        /// <param name="factory">値の生成関数。</param>
        public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (TryGetValue(key, out var value)) return value;

            value = factory(key);
            Set(key, value);
            return value;
        }

        /// <summary>全て捨てる。</summary>
        public void Clear()
        {
            _map.Clear();
            _operationsSinceSweep = 0;
        }

        /// <summary>
        /// キーが破棄されたエントリを全て落とす。落とした数を返す。
        /// シーンのアンロード直後や、大量に despawn した後に明示的に呼ぶ価値がある。
        /// </summary>
        public int Compact()
        {
            // Orphaned ハンドラがこのマップに触ると Tick 経由で Compact に再入する。
            // 掃除中は入らせない。入れてしまうと、外側が走査している _sweepBuffer を
            // 内側が Clear して「コレクションが変更された」で落ちる。
            if (_sweeping) return 0;

            _sweeping = true;

            // カウンタは走査の前に戻す。後ろに置くと、ハンドラから戻ってきた
            // 次の操作がまた閾値を超えたままになる。
            _operationsSinceSweep = 0;

            try
            {
                _sweepBuffer.Clear();

                foreach (var pair in _map)
                {
                    if (pair.Key == null) _sweepBuffer.Add(pair.Key);
                }

                var removed = 0;
                for (var i = 0; i < _sweepBuffer.Count; i++)
                {
                    var deadKey = _sweepBuffer[i];
                    if (!_map.TryGetValue(deadKey, out var value)) continue;

                    // 通知より先に外す。ハンドラから見たときには既に居ないのが正しく、
                    // 二重通知も防げる。
                    _map.Remove(deadKey);
                    removed++;
                    Orphaned?.Invoke(value);
                }

                _sweepBuffer.Clear();
                return removed;
            }
            finally
            {
                _sweeping = false;
            }
        }

        private void Tick()
        {
            if (_sweeping) return;
            if (++_operationsSinceSweep < SweepInterval) return;

            Compact();
        }

        /// <summary>生きているエントリだけを走査する構造体列挙子を返す。</summary>
        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        {
            foreach (var pair in _map)
            {
                if (pair.Key != null) yield return pair;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<KeyValuePair<TKey, TValue>>)this).GetEnumerator();

        /// <summary>破棄済みキーを飛ばして走査する構造体列挙子。</summary>
        public struct Enumerator
        {
            private Dictionary<TKey, TValue>.Enumerator _inner;

            internal Enumerator(UnityObjectMap<TKey, TValue> map) => _inner = map._map.GetEnumerator();

            /// <summary>今のエントリ。</summary>
            public KeyValuePair<TKey, TValue> Current => _inner.Current;

            /// <summary>次の生きているエントリへ進む。</summary>
            public bool MoveNext()
            {
                while (_inner.MoveNext())
                {
                    if (_inner.Current.Key != null) return true;
                }

                return false;
            }
        }
    }
}
