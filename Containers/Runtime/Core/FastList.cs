using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Containers
{
    /// <summary>
    /// 内部配列を隠さないリスト。要素を <c>ref</c> で返し、<see cref="AsSpan()"/> を公開し、
    /// 順序を捨てる代わりに O(1) で削除できる。
    /// <para>
    /// 存在理由は <see cref="List{T}"/> が要素を値で返すこと。構造体を入れると
    /// <c>list[i].Position += delta</c> が<b>そもそもコンパイルを通らない</b>。
    /// ここでは通り、しかもコピーを作らずその場で書き換わる。
    /// </para>
    /// </summary>
    public sealed class FastList<T> : IReadOnlyList<T>
    {
        private const int DefaultCapacity = 4;

        private T[] _items;
        private int _count;

        /// <summary>空のリストを作る。最初の追加まで配列は確保しない。</summary>
        public FastList() => _items = Array.Empty<T>();

        /// <summary>容量を先に確保して作る。</summary>
        /// <param name="capacity">最初に確保する要素数。</param>
        public FastList(int capacity)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _items = capacity == 0 ? Array.Empty<T>() : new T[capacity];
        }

        /// <summary>既存の列から作る。</summary>
        /// <param name="source">コピー元。</param>
        public FastList(IEnumerable<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _items = Array.Empty<T>();
            AddRange(source);
        }

        /// <summary>要素数。</summary>
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _count;
        }

        /// <summary>空かどうか。</summary>
        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _count == 0;
        }

        /// <summary>
        /// 内部配列そのもの。有効なのは <c>[0, Count)</c> だけで、実際の長さはそれより大きいことが多い。
        /// 「配列と長さ」を受け取る API に渡すために公開している。
        /// </summary>
        public T[] BackingArray
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _items;
        }

        /// <summary>確保済みの容量。縮める方向にも設定できる（<see cref="Count"/> 未満は不可）。</summary>
        public int Capacity
        {
            get => _items.Length;
            set
            {
                if (value < _count) throw new ArgumentOutOfRangeException(nameof(value));
                if (value == _items.Length) return;

                if (value == 0)
                {
                    _items = Array.Empty<T>();
                    return;
                }

                var grown = new T[value];
                Array.Copy(_items, grown, _count);
                _items = grown;
            }
        }

        /// <summary>要素への参照。代入もその場の書き換えも両方できる。</summary>
        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
                return ref _items[index];
            }
        }

        T IReadOnlyList<T>.this[int index] => _items[index];

        /// <summary>末尾に追加する。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(in T item)
        {
            if (_count == _items.Length) Grow(_count + 1);
            _items[_count++] = item;
        }

        /// <summary>末尾に既定値を足し、その参照を返す。大きな構造体をコピーせず直接埋めたいときに使う。</summary>
        public ref T AddDefault()
        {
            if (_count == _items.Length) Grow(_count + 1);
            _items[_count] = default;
            return ref _items[_count++];
        }

        /// <summary>連続領域からまとめて追加する。</summary>
        /// <param name="source">コピー元。</param>
        public void AddRange(ReadOnlySpan<T> source)
        {
            if (source.Length == 0) return;
            EnsureCapacity(_count + source.Length);
            source.CopyTo(_items.AsSpan(_count));
            _count += source.Length;
        }

        /// <summary>別の <see cref="FastList{T}"/> からまとめて追加する。</summary>
        /// <param name="source">コピー元。</param>
        public void AddRange(FastList<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            AddRange(source.AsSpan());
        }

        /// <summary>任意の列からまとめて追加する。</summary>
        /// <param name="source">コピー元。</param>
        public void AddRange(IEnumerable<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            if (source is ICollection<T> collection)
            {
                EnsureCapacity(_count + collection.Count);
                collection.CopyTo(_items, _count);
                _count += collection.Count;
                return;
            }

            foreach (var item in source) Add(item);
        }

        /// <summary>指定位置に挿入する。以降の要素はずれる。</summary>
        /// <param name="index">挿入位置。</param>
        /// <param name="item">挿入する値。</param>
        public void Insert(int index, in T item)
        {
            if ((uint)index > (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));

            if (_count == _items.Length) Grow(_count + 1);
            if (index < _count) Array.Copy(_items, index, _items, index + 1, _count - index);

            _items[index] = item;
            _count++;
        }

        /// <summary>指定位置を削除し、後ろを詰める。順序は保たれるが O(n)。</summary>
        /// <param name="index">削除位置。</param>
        public void RemoveAt(int index)
        {
            if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));

            _count--;
            if (index < _count) Array.Copy(_items, index + 1, _items, index, _count - index);
            _items[_count] = default;
        }

        /// <summary>
        /// 末尾の要素を穴に移して削除する。O(1) だが<b>残りの並び順が変わる</b>。
        /// </summary>
        /// <param name="index">削除位置。</param>
        public void RemoveAtSwapBack(int index)
        {
            if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));

            _count--;
            _items[index] = _items[_count];
            _items[_count] = default;
        }

        /// <summary>値で探して削除する。順序は保たれる。</summary>
        /// <param name="item">削除する値。</param>
        public bool Remove(in T item)
        {
            var index = IndexOf(item);
            if (index < 0) return false;
            RemoveAt(index);
            return true;
        }

        /// <summary>値で探し、末尾と入れ替えて削除する。順序は変わる。</summary>
        /// <param name="item">削除する値。</param>
        public bool RemoveSwapBack(in T item)
        {
            var index = IndexOf(item);
            if (index < 0) return false;
            RemoveAtSwapBack(index);
            return true;
        }

        /// <summary>条件に合う要素を全て削除する。削除した数を返す。</summary>
        /// <param name="predicate">true を返した要素が削除される。</param>
        public int RemoveAll(Predicate<T> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            var write = 0;
            for (var read = 0; read < _count; read++)
            {
                if (predicate(_items[read])) continue;
                if (write != read) _items[write] = _items[read];
                write++;
            }

            var removed = _count - write;
            Array.Clear(_items, write, removed);
            _count = write;
            return removed;
        }

        /// <summary>連続した範囲をまとめて削除する。</summary>
        /// <param name="index">削除開始位置。</param>
        /// <param name="count">削除する数。</param>
        public void RemoveRange(int index, int count)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (count < 0 || index + count > _count) throw new ArgumentOutOfRangeException(nameof(count));
            if (count == 0) return;

            var tail = _count - index - count;
            if (tail > 0) Array.Copy(_items, index + count, _items, index, tail);

            _count -= count;
            Array.Clear(_items, _count, count);
        }

        /// <summary>
        /// 空にする。参照を確実に手放すため配列も消す —— 消さないと回収されない
        /// オブジェクトが容量ぶん残り続ける。容量そのものは保つ。
        /// </summary>
        public void Clear()
        {
            if (_count == 0) return;
            Array.Clear(_items, 0, _count);
            _count = 0;
        }

        /// <summary>値の位置。無ければ -1。</summary>
        /// <param name="item">探す値。</param>
        public int IndexOf(in T item) => Array.IndexOf(_items, item, 0, _count);

        /// <summary>値が含まれているか。</summary>
        /// <param name="item">探す値。</param>
        public bool Contains(in T item) => IndexOf(item) >= 0;

        /// <summary>要素数を直接設定する。増やせば既定値で埋め、減らせば末尾を捨てる。</summary>
        /// <param name="count">設定後の要素数。</param>
        public void Resize(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

            if (count > _count)
            {
                EnsureCapacity(count);
                Array.Clear(_items, _count, count - _count);
            }
            else if (count < _count)
            {
                Array.Clear(_items, count, _count - count);
            }

            _count = count;
        }

        /// <summary>少なくとも指定の容量を確保する。再確保を先に済ませたいときに使う。</summary>
        /// <param name="capacity">必要な容量。</param>
        public void EnsureCapacity(int capacity)
        {
            if (capacity > _items.Length) Grow(capacity);
        }

        /// <summary>余分な容量を切り詰める。</summary>
        public void TrimExcess()
        {
            if (_count < _items.Length) Capacity = _count;
        }

        /// <summary>既定の比較で整列する。</summary>
        public void Sort() => Sort(Comparer<T>.Default);

        /// <summary>比較子を指定して整列する。</summary>
        /// <param name="comparer">比較方法。</param>
        public void Sort(IComparer<T> comparer) => Array.Sort(_items, 0, _count, comparer);

        /// <summary>比較関数を指定して整列する。</summary>
        /// <param name="comparison">比較方法。</param>
        public void Sort(Comparison<T> comparison)
        {
            if (comparison == null) throw new ArgumentNullException(nameof(comparison));
            if (_count > 1) Array.Sort(_items, 0, _count, Comparer<T>.Create(comparison));
        }

        /// <summary>並びを逆にする。</summary>
        public void Reverse() => Array.Reverse(_items, 0, _count);

        /// <summary>有効範囲だけを指す <see cref="Span{T}"/>。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> AsSpan() => new Span<T>(_items, 0, _count);

        /// <summary>指定範囲を指す <see cref="Span{T}"/>。</summary>
        /// <param name="start">開始位置。</param>
        /// <param name="length">長さ。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> AsSpan(int start, int length) => new Span<T>(_items, start, length);

        /// <summary>中身を新しい配列にコピーする。</summary>
        public T[] ToArray()
        {
            if (_count == 0) return Array.Empty<T>();
            var copy = new T[_count];
            Array.Copy(_items, copy, _count);
            return copy;
        }

        /// <summary>既存の配列にコピーする。</summary>
        /// <param name="destination">コピー先。</param>
        /// <param name="destinationIndex">コピー先の開始位置。</param>
        public void CopyTo(T[] destination, int destinationIndex) =>
            Array.Copy(_items, 0, destination, destinationIndex, _count);

        private void Grow(int required)
        {
            var capacity = _items.Length == 0 ? DefaultCapacity : _items.Length * 2;
            if (capacity < required) capacity = required;
            Capacity = capacity;
        }

        /// <summary>確保無しで走査する構造体列挙子を返す。</summary>
        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>構造体列挙子。<c>foreach</c> しても確保が起きない。</summary>
        public struct Enumerator : IEnumerator<T>
        {
            private readonly FastList<T> _list;
            private int _index;

            internal Enumerator(FastList<T> list)
            {
                _list = list;
                _index = -1;
            }

            /// <summary>今の要素への参照。走査しながら書き換えられる。</summary>
            public ref T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref _list._items[_index];
            }

            T IEnumerator<T>.Current => _list._items[_index];
            object IEnumerator.Current => _list._items[_index];

            /// <summary>次の要素へ進む。</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => ++_index < _list._count;

            /// <summary>先頭に戻す。</summary>
            public void Reset() => _index = -1;

            /// <summary>何もしない。<see cref="IEnumerator{T}"/> のための実装。</summary>
            public void Dispose() { }
        }
    }
}
