using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Containers
{
    /// <summary>
    /// 伸びる両端キュー。両端への追加・取り出しが償却 O(1) で、添字アクセスも O(1)。
    /// <see cref="RingBuffer{T}"/> が満杯で上書きするのに対し、こちらは伸びる。
    /// <para>幅優先探索のフロンティア、コマンド待ち行列、スライディングウィンドウ。</para>
    /// </summary>
    public sealed class Deque<T> : IReadOnlyList<T>
    {
        private const int DefaultCapacity = 4;

        private T[] _items;
        private int _head;
        private int _count;

        /// <summary>空のキューを作る。</summary>
        public Deque() => _items = Array.Empty<T>();

        /// <summary>容量を先に確保して作る。</summary>
        /// <param name="capacity">最初に確保する要素数。</param>
        public Deque(int capacity)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _items = capacity == 0 ? Array.Empty<T>() : new T[capacity];
        }

        /// <summary>既存の列から作る。先頭から順に末尾へ積む。</summary>
        /// <param name="source">コピー元。</param>
        public Deque(IEnumerable<T> source) : this()
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            foreach (var item in source) PushBack(item);
        }

        /// <summary>要素数。</summary>
        public int Count => _count;

        /// <summary>確保済みの容量。</summary>
        public int Capacity => _items.Length;

        /// <summary>空かどうか。</summary>
        public bool IsEmpty => _count == 0;

        /// <summary>先頭から数えた要素への参照。</summary>
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

        /// <summary>先頭要素への参照。空なら例外。</summary>
        public ref T Front
        {
            get
            {
                if (_count == 0) throw new InvalidOperationException("Deque が空。");
                return ref _items[_head];
            }
        }

        /// <summary>末尾要素への参照。空なら例外。</summary>
        public ref T Back
        {
            get
            {
                if (_count == 0) throw new InvalidOperationException("Deque が空。");
                return ref _items[Wrap(_head + _count - 1)];
            }
        }

        /// <summary>末尾に積む。</summary>
        /// <param name="item">積む値。</param>
        public void PushBack(in T item)
        {
            if (_count == _items.Length) Grow(_count + 1);
            _items[Wrap(_head + _count)] = item;
            _count++;
        }

        /// <summary>先頭に積む。</summary>
        /// <param name="item">積む値。</param>
        public void PushFront(in T item)
        {
            if (_count == _items.Length) Grow(_count + 1);
            _head = Wrap(_head - 1);
            _items[_head] = item;
            _count++;
        }

        /// <summary>先頭から取り出す。空なら例外。</summary>
        public T PopFront()
        {
            if (_count == 0) throw new InvalidOperationException("Deque が空。");

            var item = _items[_head];
            _items[_head] = default;
            _head = Wrap(_head + 1);
            _count--;
            return item;
        }

        /// <summary>末尾から取り出す。空なら例外。</summary>
        public T PopBack()
        {
            if (_count == 0) throw new InvalidOperationException("Deque が空。");

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
            if (_count > 0) Array.Clear(_items, 0, _items.Length);
            _head = 0;
            _count = 0;
        }

        /// <summary>値が含まれているか。</summary>
        /// <param name="item">探す値。</param>
        public bool Contains(in T item)
        {
            var comparer = EqualityComparer<T>.Default;
            for (var i = 0; i < _count; i++)
            {
                if (comparer.Equals(_items[Wrap(_head + i)], item)) return true;
            }

            return false;
        }

        /// <summary>先頭から順に並べた配列を作る。</summary>
        public T[] ToArray()
        {
            if (_count == 0) return Array.Empty<T>();

            var copy = new T[_count];
            var firstRun = Math.Min(_count, _items.Length - _head);
            Array.Copy(_items, _head, copy, 0, firstRun);
            if (firstRun < _count) Array.Copy(_items, 0, copy, firstRun, _count - firstRun);
            return copy;
        }

        /// <summary>少なくとも指定の容量を確保する。</summary>
        /// <param name="capacity">必要な容量。</param>
        public void EnsureCapacity(int capacity)
        {
            if (capacity > _items.Length) Grow(capacity);
        }

        /// <summary>作り直しつつ、先頭が添字 0 に来るよう並べ直す。</summary>
        private void Grow(int required)
        {
            var capacity = _items.Length == 0 ? DefaultCapacity : _items.Length * 2;
            if (capacity < required) capacity = required;

            var grown = new T[capacity];
            if (_count > 0)
            {
                var firstRun = Math.Min(_count, _items.Length - _head);
                Array.Copy(_items, _head, grown, 0, firstRun);
                if (firstRun < _count) Array.Copy(_items, 0, grown, firstRun, _count - firstRun);
            }

            _items = grown;
            _head = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Wrap(int index)
        {
            var length = _items.Length;
            if (length == 0) return 0;
            if (index >= length) return index - length;
            if (index < 0) return index + length;
            return index;
        }

        /// <summary>先頭から末尾へ走査する構造体列挙子を返す。</summary>
        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>先頭から末尾へ走査する構造体列挙子。</summary>
        public struct Enumerator : IEnumerator<T>
        {
            private readonly Deque<T> _deque;
            private int _index;

            internal Enumerator(Deque<T> deque)
            {
                _deque = deque;
                _index = -1;
            }

            /// <summary>今の要素への参照。</summary>
            public ref T Current => ref _deque[_index];

            T IEnumerator<T>.Current => _deque[_index];
            object IEnumerator.Current => _deque[_index];

            /// <summary>次へ進む。</summary>
            public bool MoveNext() => ++_index < _deque._count;

            /// <summary>先頭に戻す。</summary>
            public void Reset() => _index = -1;

            /// <summary>何もしない。</summary>
            public void Dispose() { }
        }
    }
}
