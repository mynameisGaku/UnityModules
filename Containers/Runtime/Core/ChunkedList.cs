using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Containers
{
    /// <summary>
    /// 倍々に伸びる 1 本の配列ではなく、固定長のチャンクを繋いで持つリスト。
    /// <para>
    /// そこから <see cref="FastList{T}"/> に無い 2 つの性質が出る：伸びるときに既存要素をコピーしない、
    /// そして<b>要素のアドレスが動かない</b>ので、あとから追加しても以前に取った <c>ref</c> が有効なまま。
    /// 代わりに添字アクセスでシフトとマスクが要り、要素は連続していない。
    /// </para>
    /// </summary>
    public sealed class ChunkedList<T> : IReadOnlyList<T>
    {
        private readonly int _chunkShift;
        private readonly int _chunkMask;
        private readonly int _chunkSize;

        private T[][] _chunks;
        private int _chunkCount;
        private int _count;

        /// <summary>チャンクの大きさを 2 の冪の指数で指定する。</summary>
        /// <param name="chunkSizeLog2">10 なら 1 チャンク 1024 要素。</param>
        public ChunkedList(int chunkSizeLog2 = 10)
        {
            if (chunkSizeLog2 < 1 || chunkSizeLog2 > 24) throw new ArgumentOutOfRangeException(nameof(chunkSizeLog2));

            _chunkShift = chunkSizeLog2;
            _chunkSize = 1 << chunkSizeLog2;
            _chunkMask = _chunkSize - 1;
            _chunks = new T[4][];
        }

        /// <summary>要素数。</summary>
        public int Count => _count;

        /// <summary>1 チャンクあたりの要素数。</summary>
        public int ChunkSize => _chunkSize;

        /// <summary>空かどうか。</summary>
        public bool IsEmpty => _count == 0;

        /// <summary>要素への参照。追加でこの参照が無効になることはない。</summary>
        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
                return ref _chunks[index >> _chunkShift][index & _chunkMask];
            }
        }

        T IReadOnlyList<T>.this[int index] => this[index];

        /// <summary>末尾に追加する。</summary>
        /// <param name="item">追加する値。</param>
        public void Add(in T item)
        {
            EnsureChunkFor(_count);
            _chunks[_count >> _chunkShift][_count & _chunkMask] = item;
            _count++;
        }

        /// <summary>末尾に既定値を足し、以後の追加でも無効にならない参照を返す。</summary>
        public ref T AddDefault()
        {
            EnsureChunkFor(_count);
            ref var slot = ref _chunks[_count >> _chunkShift][_count & _chunkMask];
            slot = default;
            _count++;
            return ref slot;
        }

        /// <summary>まとめて追加する。</summary>
        /// <param name="source">コピー元。</param>
        public void AddRange(IEnumerable<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            foreach (var item in source) Add(item);
        }

        /// <summary>末尾を取り出す。空なら例外。</summary>
        public T RemoveLast()
        {
            if (_count == 0) throw new InvalidOperationException("ChunkedList が空。");

            _count--;
            ref var slot = ref _chunks[_count >> _chunkShift][_count & _chunkMask];
            var item = slot;
            slot = default;
            return item;
        }

        /// <summary>末尾を穴に移して削除する。O(1) だが並び順は変わる。</summary>
        /// <param name="index">削除位置。</param>
        public void RemoveAtSwapBack(int index)
        {
            if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));

            // 末尾そのものを消す場合に this[index] = RemoveLast() と書くと、
            // 参照を返す添字が先に評価されるため、RemoveLast が消した参照を
            // そのスロットに書き戻してしまう。
            if (index == _count - 1)
            {
                RemoveLast();
                return;
            }

            this[index] = RemoveLast();
        }

        /// <summary>要素数を 0 にする。確保済みのチャンクは保持したまま。</summary>
        public void Clear()
        {
            for (var i = 0; i < _chunkCount; i++) Array.Clear(_chunks[i], 0, _chunkSize);
            _count = 0;
        }

        /// <summary>生きている要素を持たないチャンクを解放する。</summary>
        public void TrimExcess()
        {
            var needed = (_count + _chunkSize - 1) >> _chunkShift;
            for (var i = needed; i < _chunkCount; i++) _chunks[i] = null;
            _chunkCount = needed;
        }

        /// <summary>
        /// 1 チャンク分の連続した領域。チャンク単位で回すとメモリアクセスが線形になる。
        /// </summary>
        /// <param name="chunkIndex">チャンクの位置。</param>
        public Span<T> GetChunkSpan(int chunkIndex)
        {
            if ((uint)chunkIndex >= (uint)_chunkCount) throw new ArgumentOutOfRangeException(nameof(chunkIndex));

            var start = chunkIndex << _chunkShift;
            var length = Math.Min(_chunkSize, _count - start);
            return length <= 0 ? Span<T>.Empty : new Span<T>(_chunks[chunkIndex], 0, length);
        }

        /// <summary>生きている要素を含むチャンクの数。</summary>
        public int ChunkCount => (_count + _chunkSize - 1) >> _chunkShift;

        private void EnsureChunkFor(int index)
        {
            var chunk = index >> _chunkShift;
            if (chunk < _chunkCount) return;

            if (chunk >= _chunks.Length) Array.Resize(ref _chunks, Math.Max(_chunks.Length * 2, chunk + 1));
            _chunks[chunk] = new T[_chunkSize];
            _chunkCount = chunk + 1;
        }

        /// <summary>先頭から走査する構造体列挙子を返す。</summary>
        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>先頭から走査する構造体列挙子。</summary>
        public struct Enumerator : IEnumerator<T>
        {
            private readonly ChunkedList<T> _list;
            private int _index;

            internal Enumerator(ChunkedList<T> list)
            {
                _list = list;
                _index = -1;
            }

            /// <summary>今の要素への参照。</summary>
            public ref T Current => ref _list[_index];

            T IEnumerator<T>.Current => _list[_index];
            object IEnumerator.Current => _list[_index];

            /// <summary>次へ進む。</summary>
            public bool MoveNext() => ++_index < _list._count;

            /// <summary>先頭に戻す。</summary>
            public void Reset() => _index = -1;

            /// <summary>何もしない。</summary>
            public void Dispose() { }
        }
    }
}
