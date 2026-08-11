using System;
using System.Collections.Generic;

namespace Containers
{
    /// <summary>
    /// 最小ヒープによる優先度付きキュー。優先度が小さいものから出る。
    /// <para>
    /// これが必要な理由ははっきりしている：<c>System.Collections.Generic.PriorityQueue</c> は
    /// .NET 6 以降の型で、このプロジェクトが対象とする netstandard2.1 には<b>存在しない</b>。
    /// API を BCL 版に合わせてあるので、将来ランタイムが上がったらこのファイルを消すだけで移行できる。
    /// </para>
    /// </summary>
    public sealed class PriorityQueue<TElement, TPriority>
    {
        private const int DefaultCapacity = 4;

        private (TElement Element, TPriority Priority)[] _nodes;
        private readonly IComparer<TPriority> _comparer;
        private int _count;

        /// <summary>既定の比較で空のキューを作る。</summary>
        public PriorityQueue() : this(0, null) { }

        /// <summary>容量を指定して作る。</summary>
        /// <param name="capacity">最初に確保する要素数。</param>
        public PriorityQueue(int capacity) : this(capacity, null) { }

        /// <summary>比較子を指定して作る。</summary>
        /// <param name="comparer">優先度の比較方法。</param>
        public PriorityQueue(IComparer<TPriority> comparer) : this(0, comparer) { }

        /// <summary>容量と比較子を指定して作る。</summary>
        /// <param name="capacity">最初に確保する要素数。</param>
        /// <param name="comparer">優先度の比較方法。null なら既定。</param>
        public PriorityQueue(int capacity, IComparer<TPriority> comparer)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));

            _nodes = capacity == 0
                ? Array.Empty<(TElement, TPriority)>()
                : new (TElement, TPriority)[capacity];
            _comparer = comparer ?? Comparer<TPriority>.Default;
        }

        /// <summary>要素数。</summary>
        public int Count => _count;

        /// <summary>優先度の比較方法。</summary>
        public IComparer<TPriority> Comparer => _comparer;

        /// <summary>優先度を付けて積む。</summary>
        /// <param name="element">積む要素。</param>
        /// <param name="priority">優先度。小さいほど先に出る。</param>
        public void Enqueue(TElement element, TPriority priority)
        {
            if (_count == _nodes.Length) Grow(_count + 1);

            _nodes[_count] = (element, priority);
            SiftUp(_count++);
        }

        /// <summary>優先度が最小の要素を取り出す。空なら例外。</summary>
        public TElement Dequeue()
        {
            if (_count == 0) throw new InvalidOperationException("PriorityQueue が空。");

            var root = _nodes[0].Element;
            RemoveRoot();
            return root;
        }

        /// <summary>優先度が最小の要素を取り出さずに見る。空なら例外。</summary>
        public TElement Peek()
        {
            if (_count == 0) throw new InvalidOperationException("PriorityQueue が空。");
            return _nodes[0].Element;
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
            RemoveRoot();
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

        /// <summary>
        /// 積んでから取り出すのを 1 回で行う。ヒープの整列が 1 度で済むぶん、
        /// <see cref="Enqueue"/> と <see cref="Dequeue"/> を続けて呼ぶより速い。
        /// </summary>
        /// <param name="element">積む要素。</param>
        /// <param name="priority">その優先度。</param>
        public TElement EnqueueDequeue(TElement element, TPriority priority)
        {
            if (_count == 0 || _comparer.Compare(priority, _nodes[0].Priority) <= 0) return element;

            var root = _nodes[0].Element;
            _nodes[0] = (element, priority);
            SiftDown(0);
            return root;
        }

        /// <summary>まとめて積む。</summary>
        /// <param name="items">(要素, 優先度) の並び。</param>
        public void EnqueueRange(IEnumerable<(TElement Element, TPriority Priority)> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            foreach (var (element, priority) in items) Enqueue(element, priority);
        }

        /// <summary>空にする。</summary>
        public void Clear()
        {
            if (_count == 0) return;
            Array.Clear(_nodes, 0, _count);
            _count = 0;
        }

        /// <summary>少なくとも指定の容量を確保する。</summary>
        /// <param name="capacity">必要な容量。</param>
        public void EnsureCapacity(int capacity)
        {
            if (capacity > _nodes.Length) Grow(capacity);
        }

        /// <summary>ヒープ順（＝整列済みではない）に並んだ中身。デバッグ表示用。</summary>
        public IEnumerable<(TElement Element, TPriority Priority)> UnorderedItems
        {
            get
            {
                for (var i = 0; i < _count; i++) yield return _nodes[i];
            }
        }

        private void RemoveRoot()
        {
            _count--;
            if (_count > 0) _nodes[0] = _nodes[_count];
            _nodes[_count] = default;
            if (_count > 1) SiftDown(0);
        }

        private void SiftUp(int index)
        {
            var node = _nodes[index];

            while (index > 0)
            {
                var parent = (index - 1) >> 1;
                if (_comparer.Compare(node.Priority, _nodes[parent].Priority) >= 0) break;

                _nodes[index] = _nodes[parent];
                index = parent;
            }

            _nodes[index] = node;
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
                index = child;
            }

            _nodes[index] = node;
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
