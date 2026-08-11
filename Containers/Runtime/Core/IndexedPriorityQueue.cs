using System;
using System.Collections.Generic;

namespace Containers
{
    /// <summary>
    /// 既に入っている要素の優先度を下げられる最小ヒープ。
    /// <para>
    /// 素の <see cref="PriorityQueue{TElement,TPriority}"/> にはこれができないため、
    /// Dijkstra や A* は同じノードを重複して積み、取り出すときに古い方を捨てる書き方になる。
    /// それでも動くがキューが辺の数まで膨らむ。ここでは各要素が 1 つだけ存在し、
    /// <see cref="TryDecreasePriority"/> が O(log n) でヒープを直す。
    /// </para>
    /// </summary>
    public sealed class IndexedPriorityQueue<TElement, TPriority>
    {
        private const int DefaultCapacity = 8;

        private (TElement Element, TPriority Priority)[] _nodes;
        private readonly Dictionary<TElement, int> _positions;
        private readonly IComparer<TPriority> _comparer;
        private int _count;

        /// <summary>容量と比較方法を指定して作る。</summary>
        /// <param name="capacity">最初に確保する要素数。</param>
        /// <param name="comparer">優先度の比較方法。</param>
        /// <param name="elementComparer">要素の同一性の判定方法。</param>
        public IndexedPriorityQueue(
            int capacity = 0,
            IComparer<TPriority> comparer = null,
            IEqualityComparer<TElement> elementComparer = null)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));

            _nodes = capacity == 0
                ? Array.Empty<(TElement, TPriority)>()
                : new (TElement, TPriority)[capacity];
            _positions = new Dictionary<TElement, int>(elementComparer);
            _comparer = comparer ?? Comparer<TPriority>.Default;
        }

        /// <summary>要素数。</summary>
        public int Count => _count;

        /// <summary>要素が入っているか。</summary>
        /// <param name="element">探す要素。</param>
        public bool Contains(TElement element) => _positions.ContainsKey(element);

        /// <summary>未登録なら積み、登録済みなら優先度を下げる。経路探索で最もよく使う形。</summary>
        /// <param name="element">対象の要素。</param>
        /// <param name="priority">設定したい優先度。</param>
        public void EnqueueOrDecrease(TElement element, TPriority priority)
        {
            if (TryDecreasePriority(element, priority)) return;
            if (_positions.ContainsKey(element)) return;   // 既にあるが、今の方が良い優先度

            Enqueue(element, priority);
        }

        /// <summary>優先度を付けて積む。既に入っていれば例外。</summary>
        /// <param name="element">積む要素。</param>
        /// <param name="priority">優先度。</param>
        public void Enqueue(TElement element, TPriority priority)
        {
            if (_positions.ContainsKey(element))
                throw new ArgumentException($"要素 {element} は既にキューに入っている。", nameof(element));

            if (_count == _nodes.Length) Grow(_count + 1);

            _nodes[_count] = (element, priority);
            SiftUp(_count++);
        }

        /// <summary>
        /// 優先度を下げる。要素が無い場合や、今より良くならない場合は
        /// 何も変えずに false を返す。
        /// </summary>
        /// <param name="element">対象の要素。</param>
        /// <param name="priority">新しい優先度。</param>
        public bool TryDecreasePriority(TElement element, TPriority priority)
        {
            if (!_positions.TryGetValue(element, out var index)) return false;
            if (_comparer.Compare(priority, _nodes[index].Priority) >= 0) return false;

            _nodes[index].Priority = priority;
            SiftUp(index);
            return true;
        }

        /// <summary>優先度を上下どちらにも設定し直し、ヒープを正しい向きに直す。</summary>
        /// <param name="element">対象の要素。</param>
        /// <param name="priority">新しい優先度。</param>
        public bool TryUpdatePriority(TElement element, TPriority priority)
        {
            if (!_positions.TryGetValue(element, out var index)) return false;

            var order = _comparer.Compare(priority, _nodes[index].Priority);
            _nodes[index].Priority = priority;

            if (order < 0) SiftUp(index);
            else if (order > 0) SiftDown(index);

            return true;
        }

        /// <summary>優先度が最小の要素を取り出す。空なら例外。</summary>
        public TElement Dequeue()
        {
            if (!TryDequeue(out var element, out _)) throw new InvalidOperationException("キューが空。");
            return element;
        }

        /// <summary>優先度が最小の要素を取り出す。空なら false。</summary>
        /// <param name="element">取り出した要素。</param>
        /// <param name="priority">その優先度。</param>
        public bool TryDequeue(out TElement element, out TPriority priority)
        {
            if (_count == 0)
            {
                element = default;
                priority = default;
                return false;
            }

            (element, priority) = _nodes[0];
            _positions.Remove(element);

            _count--;
            if (_count > 0)
            {
                _nodes[0] = _nodes[_count];
                _positions[_nodes[0].Element] = 0;
            }

            _nodes[_count] = default;
            if (_count > 1) SiftDown(0);
            return true;
        }

        /// <summary>優先度が最小の要素を覗く。空なら false。</summary>
        /// <param name="element">先頭の要素。</param>
        /// <param name="priority">その優先度。</param>
        public bool TryPeek(out TElement element, out TPriority priority)
        {
            if (_count == 0)
            {
                element = default;
                priority = default;
                return false;
            }

            (element, priority) = _nodes[0];
            return true;
        }

        /// <summary>要素の今の優先度を読む。</summary>
        /// <param name="element">対象の要素。</param>
        /// <param name="priority">今の優先度。</param>
        public bool TryGetPriority(TElement element, out TPriority priority)
        {
            if (!_positions.TryGetValue(element, out var index))
            {
                priority = default;
                return false;
            }

            priority = _nodes[index].Priority;
            return true;
        }

        /// <summary>空にする。</summary>
        public void Clear()
        {
            Array.Clear(_nodes, 0, _count);
            _positions.Clear();
            _count = 0;
        }

        private void SiftUp(int index)
        {
            var node = _nodes[index];

            while (index > 0)
            {
                var parent = (index - 1) >> 1;
                if (_comparer.Compare(node.Priority, _nodes[parent].Priority) >= 0) break;

                _nodes[index] = _nodes[parent];
                _positions[_nodes[index].Element] = index;
                index = parent;
            }

            _nodes[index] = node;
            _positions[node.Element] = index;
        }

        private void SiftDown(int index)
        {
            var node = _nodes[index];
            var half = _count >> 1;

            while (index < half)
            {
                var child = (index << 1) + 1;
                var right = child + 1;

                if (right < _count && _comparer.Compare(_nodes[right].Priority, _nodes[child].Priority) < 0)
                {
                    child = right;
                }

                if (_comparer.Compare(_nodes[child].Priority, node.Priority) >= 0) break;

                _nodes[index] = _nodes[child];
                _positions[_nodes[index].Element] = index;
                index = child;
            }

            _nodes[index] = node;
            _positions[node.Element] = index;
        }

        private void Grow(int required)
        {
            var capacity = _nodes.Length == 0 ? DefaultCapacity : _nodes.Length * 2;
            if (capacity < required) capacity = required;

            var grown = new (TElement, TPriority)[capacity];
            Array.Copy(_nodes, grown, _count);
            _nodes = grown;
        }
    }
}
