using System;

namespace Containers
{
    /// <summary>
    /// 最大 8 要素を構造体の中に直接持つリスト。配列もヒープ確保も使わない。
    /// 他の構造体のフィールド、Job に渡すデータ、そして
    /// 「近くのものをいくつか集める」という頻出の問い合わせに向く。
    /// <para>
    /// C# 12 の <c>InlineArray</c> は C# 9 では使えず、<c>fixed</c> バッファは unsafe が要るので、
    /// スロットは手で並べてある。8 個を超えるなら、そもそも配列の方が正しい。
    /// </para>
    /// </summary>
    public struct FixedList8<T>
    {
        /// <summary>保持できる最大数。</summary>
        public const int Capacity = 8;

        private T _e0, _e1, _e2, _e3, _e4, _e5, _e6, _e7;
        private int _count;

        /// <summary>今入っている数。</summary>
        public int Count => _count;

        /// <summary>空かどうか。</summary>
        public bool IsEmpty => _count == 0;

        /// <summary>満杯かどうか。</summary>
        public bool IsFull => _count == Capacity;

        /// <summary>
        /// 要素の読み書き。値渡しなのは C# の制約で、
        /// <b>構造体は自分のフィールドへの <c>ref</c> を返せない</b>（CS8170）ため。
        /// 配列を土台にした他のコンテナと違い、読むとコピーが起きる。
        /// </summary>
        public T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));

                switch (index)
                {
                    case 0: return _e0;
                    case 1: return _e1;
                    case 2: return _e2;
                    case 3: return _e3;
                    case 4: return _e4;
                    case 5: return _e5;
                    case 6: return _e6;
                    default: return _e7;
                }
            }
            set
            {
                if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));

                switch (index)
                {
                    case 0: _e0 = value; break;
                    case 1: _e1 = value; break;
                    case 2: _e2 = value; break;
                    case 3: _e3 = value; break;
                    case 4: _e4 = value; break;
                    case 5: _e5 = value; break;
                    case 6: _e6 = value; break;
                    default: _e7 = value; break;
                }
            }
        }

        /// <summary>末尾に追加する。満杯なら例外。</summary>
        /// <param name="item">追加する値。</param>
        public void Add(in T item)
        {
            if (_count == Capacity) throw new InvalidOperationException("FixedList8 が満杯。");

            _count++;
            this[_count - 1] = item;
        }

        /// <summary>空きがあれば追加する。例外を投げずに入ったかどうかを返す。</summary>
        /// <param name="item">追加する値。</param>
        public bool TryAdd(in T item)
        {
            if (_count == Capacity) return false;

            _count++;
            this[_count - 1] = item;
            return true;
        }

        /// <summary>末尾を穴に移して削除する。O(1) だが並び順は変わる。</summary>
        /// <param name="index">削除位置。</param>
        public void RemoveAtSwapBack(int index)
        {
            if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));

            this[index] = this[_count - 1];
            this[_count - 1] = default;
            _count--;
        }

        /// <summary>削除して後ろを詰める。並び順は保たれる。</summary>
        /// <param name="index">削除位置。</param>
        public void RemoveAt(int index)
        {
            if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));

            for (var i = index; i < _count - 1; i++) this[i] = this[i + 1];
            this[_count - 1] = default;
            _count--;
        }

        /// <summary>値の位置。無ければ -1。</summary>
        /// <param name="item">探す値。</param>
        public int IndexOf(in T item)
        {
            var comparer = System.Collections.Generic.EqualityComparer<T>.Default;
            for (var i = 0; i < _count; i++)
            {
                if (comparer.Equals(this[i], item)) return i;
            }

            return -1;
        }

        /// <summary>値が含まれているか。</summary>
        /// <param name="item">探す値。</param>
        public bool Contains(in T item) => IndexOf(item) >= 0;

        /// <summary>空にする。</summary>
        public void Clear()
        {
            _e0 = _e1 = _e2 = _e3 = _e4 = _e5 = _e6 = _e7 = default;
            _count = 0;
        }

        /// <summary>中身を配列にコピーする。</summary>
        public T[] ToArray()
        {
            var copy = new T[_count];
            for (var i = 0; i < _count; i++) copy[i] = this[i];
            return copy;
        }

        /// <summary>先頭から走査する構造体列挙子を返す。</summary>
        public Enumerator GetEnumerator() => new Enumerator(this);

        /// <summary>
        /// 走査用の構造体列挙子。開始時点の中身をコピーして持つので、
        /// 走査中に元を書き換えても影響しない。
        /// </summary>
        public struct Enumerator
        {
            private FixedList8<T> _list;
            private int _index;

            internal Enumerator(FixedList8<T> list)
            {
                _list = list;
                _index = -1;
            }

            /// <summary>今の要素。</summary>
            public T Current => _list[_index];

            /// <summary>次へ進む。</summary>
            public bool MoveNext() => ++_index < _list._count;

            /// <summary>先頭に戻す。</summary>
            public void Reset() => _index = -1;
        }
    }
}
