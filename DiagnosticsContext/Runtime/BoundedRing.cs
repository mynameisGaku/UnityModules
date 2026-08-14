using System;

namespace DiagnosticsContext
{
    /// <summary>古い項目から追い出す固定件数の時系列領域。</summary>
    /// <typeparam name="T">保持する時系列項目。</typeparam>
    internal sealed class BoundedRing<T>
    {
        /// <summary>項目を格納する固定長array。</summary>
        private readonly T[] _items;

        /// <summary>現在最も古い項目のindex。</summary>
        private int _startIndex;

        /// <summary>現在保持している項目数。</summary>
        private int _count;

        /// <summary>固定容量を持つ空の時系列領域を作る。</summary>
        /// <param name="capacity">1以上の最大項目数。</param>
        internal BoundedRing(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _items = new T[capacity];
        }

        /// <summary>現在保持している項目数。</summary>
        internal int Count => _count;

        /// <summary>末尾へ項目を追加し、満杯なら最古項目を追い出す。</summary>
        /// <param name="item">新しい時系列項目。</param>
        /// <returns>古い項目を1件追い出した場合はtrue。</returns>
        internal bool Add(T item)
        {
            if (_count < _items.Length)
            {
                _items[(_startIndex + _count) % _items.Length] = item;
                _count++;
                return false;
            }

            _items[_startIndex] = item;
            _startIndex = (_startIndex + 1) % _items.Length;
            return true;
        }

        /// <summary>最古から最新の順で独立したsnapshotを返す。</summary>
        /// <returns>追加後の変更を受けないarray。</returns>
        internal T[] Snapshot()
        {
            var snapshot = new T[_count];
            for (var index = 0; index < _count; index++) snapshot[index] = _items[(_startIndex + index) % _items.Length];
            return snapshot;
        }

        /// <summary>保持項目をすべて破棄して空に戻す。</summary>
        internal void Clear()
        {
            Array.Clear(_items, 0, _items.Length);
            _startIndex = 0;
            _count = 0;
        }
    }
}
