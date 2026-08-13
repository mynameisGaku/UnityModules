using System;
using System.Collections;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace Containers
{
    /// <summary>
    /// 破棄された要素を走査のついでに落としていく、Unity オブジェクトのリスト。
    /// <para>
    /// 防ぐのはこの失敗：稼働中の敵リストに登録し、その敵が死に、次の走査で
    /// <c>MissingReferenceException</c> が出る —— あるいはもっと悪く、半分壊れたオブジェクトの
    /// メソッドを呼んでしまう。ここでは走査中に破棄済みの要素を詰めて飛ばすので、
    /// ループが見るのは常に生きているオブジェクトだけになる。
    /// </para>
    /// </summary>
    public sealed class SafeList<T> : IEnumerable<T> where T : Object
    {
        private readonly FastList<T> _items = new FastList<T>();

        /// <summary>空のリストを作る。</summary>
        public SafeList() { }

        /// <summary>既存の列から作る。null と破棄済みは無視される。</summary>
        /// <param name="source">コピー元。</param>
        public SafeList(IEnumerable<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            foreach (var item in source) Add(item);
        }

        /// <summary>
        /// まだ詰めていない破棄済みを含むエントリ数。
        /// 正確な数が要るなら <see cref="LiveCount"/> を使う。
        /// </summary>
        public int Count => _items.Count;

        /// <summary>生きている要素の数。全件走査するので O(n)。</summary>
        public int LiveCount
        {
            get
            {
                var live = 0;
                for (var i = 0; i < _items.Count; i++)
                {
                    if (_items[i] != null) live++;
                }

                return live;
            }
        }

        /// <summary>位置で要素を取る。破棄済みなら null。</summary>
        public T this[int index]
        {
            get
            {
                var item = _items[index];
                return item == null ? null : item;
            }
        }

        /// <summary>生きているオブジェクトを追加する。null と破棄済みは無視する。</summary>
        /// <param name="item">追加するオブジェクト。</param>
        public bool Add(T item)
        {
            if (item == null) return false;

            _items.Add(item);
            return true;
        }

        /// <summary>まだ入っていなければ追加する。O(n)。</summary>
        /// <param name="item">追加するオブジェクト。</param>
        public bool AddUnique(T item)
        {
            if (item == null || Contains(item)) return false;

            _items.Add(item);
            return true;
        }

        /// <summary>取り除く。破棄済みのオブジェクトでも参照同一性で取り除ける。</summary>
        /// <param name="item">取り除くオブジェクト。</param>
        public bool Remove(T item) => !ReferenceEquals(item, null) && _items.RemoveSwapBack(item);

        /// <summary>生きている要素として含まれているか。</summary>
        /// <param name="item">探すオブジェクト。</param>
        public bool Contains(T item)
        {
            if (item == null) return false;

            for (var i = 0; i < _items.Count; i++)
            {
                if (ReferenceEquals(_items[i], item)) return true;
            }

            return false;
        }

        /// <summary>全て捨てる。</summary>
        public void Clear() => _items.Clear();

        /// <summary>破棄済みのエントリを今すぐ落とす。落とした数を返す。</summary>
        public int Compact()
        {
            var before = _items.Count;
            _items.RemoveAll(item => item == null);
            return before - _items.Count;
        }

        /// <summary>生きているオブジェクトのスナップショット。走査中に増減させたいときに使う。</summary>
        public T[] ToArray()
        {
            using var temp = TempList<T>.Rent();
            for (var i = 0; i < _items.Count; i++)
            {
                if (_items[i] != null) temp.List.Add(_items[i]);
            }

            return temp.List.ToArray();
        }

        /// <summary>生きているオブジェクトから一様にランダムな 1 件。残っていなければ null。</summary>
        public T Random()
        {
            Compact();
            return _items.Count == 0 ? null : _items[UnityEngine.Random.Range(0, _items.Count)];
        }

        /// <summary>
        /// 生きているオブジェクトを走査しつつ、破棄済みを詰めていく。
        /// 走査中の追加には対応していないので、必要なら <see cref="ToArray"/> を回すこと。
        /// </summary>
        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            for (var i = 0; i < _items.Count; i++)
            {
                if (_items[i] != null) yield return _items[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)this).GetEnumerator();

        /// <summary>破棄済みを取り除きながら走査する構造体列挙子。</summary>
        public struct Enumerator
        {
            private readonly SafeList<T> _list;
            private int _index;

            internal Enumerator(SafeList<T> list)
            {
                _list = list;
                _index = -1;
            }

            /// <summary>今のオブジェクト。</summary>
            public T Current => _list._items[_index];

            /// <summary>次の生きているオブジェクトへ進む。</summary>
            public bool MoveNext()
            {
                var items = _list._items;

                while (++_index < items.Count)
                {
                    if (items[_index] != null) return true;

                    // 破棄済み。末尾を詰めて、同じ位置をもう一度見る。
                    items.RemoveAtSwapBack(_index--);
                }

                return false;
            }

            /// <summary>先頭に戻す。</summary>
            public void Reset() => _index = -1;
        }
    }
}
