using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Containers
{
    /// <summary>
    /// 固定容量の環状バッファ。満杯のまま追加すると、伸びる代わりに反対端の要素を上書きする。
    /// <para>
    /// これは「直近 N 件だけ欲しい」という形にちょうど合う。入力バッファ、フレームタイムのグラフ、
    /// ロールバック用のスナップショット、直近のログ。伸び続けるリストで代用すると、
    /// 古い要素を捨てる処理を自前で書くことになる。
    /// </para>
    /// <para>添字 0 は常に最も古い要素（＝先頭）。</para>
    /// </summary>
    public sealed class RingBuffer<T> : IReadOnlyList<T>
    {
        private readonly T[] _items;
        private int _head;   // 先頭要素の位置
        private int _count;

        /// <summary>容量を指定して作る。容量は後から変えられない。</summary>
        /// <param name="capacity">保持できる要素数。</param>
        public RingBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _items = new T[capacity];
        }

        /// <summary>今入っている要素数。</summary>
        public int Count => _count;

        /// <summary>保持できる最大数。</summary>
        public int Capacity => _items.Length;

        /// <summary>空かどうか。</summary>
        public bool IsEmpty => _count == 0;

        /// <summary>満杯かどうか。満杯での追加は反対端を押し出す。</summary>
        public bool IsFull => _count == _items.Length;

        /// <summary>古い順の要素。<c>[0]</c> が最古、<c>[Count - 1]</c> が最新。</summary>
        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
                return ref _items[Wrap(_head + index)];
            }
        }

        T IReadOnlyList<T>.this[int index] => this[index];

        /// <summary>最も古い要素への参照。空なら例外。</summary>
        public ref T Front
        {
            get
            {
                if (_count == 0) throw new InvalidOperationException("RingBuffer が空。");
                return ref _items[_head];
            }
        }

        /// <summary>最も新しい要素への参照。空なら例外。</summary>
        public ref T Back
        {
            get
            {
                if (_count == 0) throw new InvalidOperationException("RingBuffer が空。");
                return ref _items[Wrap(_head + _count - 1)];
            }
        }

        /// <summary>末尾に追加する。満杯なら最古の要素が押し出される。</summary>
        /// <param name="item">追加する値。</param>
        public void PushBack(in T item)
        {
            if (IsFull)
            {
                _items[_head] = item;
                _head = Wrap(_head + 1);
                return;
            }

            _items[Wrap(_head + _count)] = item;
            _count++;
        }

        /// <summary>先頭に追加する。満杯なら最新の要素が押し出される。</summary>
        /// <param name="item">追加する値。</param>
        public void PushFront(in T item)
        {
            _head = Wrap(_head - 1);
            _items[_head] = item;
            if (!IsFull) _count++;
        }

        /// <summary>
        /// 末尾に追加し、押し出された要素を受け取る。
        /// 捨てる要素が何かを解放する必要があるときに使う。
        /// </summary>
        /// <param name="item">追加する値。</param>
        /// <param name="evicted">押し出された値。押し出しが無ければ既定値。</param>
        public bool PushBack(in T item, out T evicted)
        {
            if (IsFull)
            {
                evicted = _items[_head];
                _items[_head] = item;
                _head = Wrap(_head + 1);
                return true;
            }

            evicted = default;
            PushBack(item);
            return false;
        }

        /// <summary>先頭から取り出す。空なら例外。</summary>
        public T PopFront()
        {
            if (_count == 0) throw new InvalidOperationException("RingBuffer が空。");

            var item = _items[_head];
            _items[_head] = default;
            _head = Wrap(_head + 1);
            _count--;
            return item;
        }

        /// <summary>末尾から取り出す。空なら例外。</summary>
        public T PopBack()
        {
            if (_count == 0) throw new InvalidOperationException("RingBuffer が空。");

            var index = Wrap(_head + _count - 1);
            var item = _items[index];
            _items[index] = default;
            _count--;
            return item;
        }

        /// <summary>先頭から取り出す。空なら false。</summary>
        /// <param name="item">取り出した値。</param>
        public bool TryPopFront(out T item)
        {
            if (_count == 0)
            {
                item = default;
                return false;
            }

            item = PopFront();
            return true;
        }

        /// <summary>末尾から取り出す。空なら false。</summary>
        /// <param name="item">取り出した値。</param>
        public bool TryPopBack(out T item)
        {
            if (_count == 0)
            {
                item = default;
                return false;
            }

            item = PopBack();
            return true;
        }

        /// <summary>先頭を覗く。空なら false。</summary>
        /// <param name="item">先頭の値。</param>
        public bool TryPeekFront(out T item)
        {
            item = _count == 0 ? default : _items[_head];
            return _count > 0;
        }

        /// <summary>末尾を覗く。空なら false。</summary>
        /// <param name="item">末尾の値。</param>
        public bool TryPeekBack(out T item)
        {
            item = _count == 0 ? default : _items[Wrap(_head + _count - 1)];
            return _count > 0;
        }

        /// <summary>空にする。</summary>
        public void Clear()
        {
            Array.Clear(_items, 0, _items.Length);
            _head = 0;
            _count = 0;
        }

        /// <summary>値の位置（古い順の添字）。無ければ -1。</summary>
        /// <param name="item">探す値。</param>
        public int IndexOf(in T item)
        {
            var comparer = EqualityComparer<T>.Default;
            for (var i = 0; i < _count; i++)
            {
                if (comparer.Equals(_items[Wrap(_head + i)], item)) return i;
            }

            return -1;
        }

        /// <summary>値が含まれているか。</summary>
        /// <param name="item">探す値。</param>
        public bool Contains(in T item) => IndexOf(item) >= 0;

        /// <summary>古い順に並べた配列を作る。</summary>
        public T[] ToArray()
        {
            if (_count == 0) return Array.Empty<T>();

            var copy = new T[_count];
            CopyTo(copy, 0);
            return copy;
        }

        /// <summary>古い順に既存の配列へコピーする。折り返しは 2 回のコピーに分けて処理する。</summary>
        /// <param name="destination">コピー先。</param>
        /// <param name="destinationIndex">コピー先の開始位置。</param>
        public void CopyTo(T[] destination, int destinationIndex)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            var firstRun = Math.Min(_count, _items.Length - _head);
            Array.Copy(_items, _head, destination, destinationIndex, firstRun);
            if (firstRun < _count) Array.Copy(_items, 0, destination, destinationIndex + firstRun, _count - firstRun);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Wrap(int index)
        {
            var length = _items.Length;
            if (index >= length) return index - length;
            if (index < 0) return index + length;
            return index;
        }

        /// <summary>古い順に走査する構造体列挙子を返す。</summary>
        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>古い順に走査する構造体列挙子。確保は起きない。</summary>
        public struct Enumerator : IEnumerator<T>
        {
            private readonly RingBuffer<T> _buffer;
            private int _index;

            internal Enumerator(RingBuffer<T> buffer)
            {
                _buffer = buffer;
                _index = -1;
            }

            /// <summary>今の要素への参照。</summary>
            public ref T Current => ref _buffer[_index];

            T IEnumerator<T>.Current => _buffer[_index];
            object IEnumerator.Current => _buffer[_index];

            /// <summary>次の要素へ進む。</summary>
            public bool MoveNext() => ++_index < _buffer._count;

            /// <summary>先頭に戻す。</summary>
            public void Reset() => _index = -1;

            /// <summary>何もしない。</summary>
            public void Dispose() { }
        }
    }
}
