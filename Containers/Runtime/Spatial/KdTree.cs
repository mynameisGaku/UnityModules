using System;
using UnityEngine;

namespace Containers.Spatial
{
    /// <summary>
    /// 動かない点集合に対する、平衡した 3 次元 k-d 木。1 度作って何度も引く用途に向く。
    /// <para>
    /// カバーポイント、巡回ノード、スポーン地点、色の量子化のように<b>点が動かない</b>なら、
    /// 「最も近い」「近い k 個」をどの動的構造よりも速く答えられる。中央値で分割して作るため
    /// 木が完全に釣り合うからで、代わりに点を 1 つ足すには作り直しが要る。
    /// </para>
    /// </summary>
    public sealed class KdTree<T>
    {
        private struct Node
        {
            public Vector3 Position;
            public T Item;
            public int Left;
            public int Right;
            public int Axis;
        }

        private const int Nil = -1;

        private Node[] _nodes;
        private int _root = Nil;
        private int _count;

        /// <summary>登録されている点の数。</summary>
        public int Count => _count;

        /// <summary>木を作る。以前の中身は破棄される。</summary>
        /// <param name="items">登録する要素。</param>
        /// <param name="positionOf">要素から位置を取り出す関数。</param>
        public void Build(ReadOnlySpan<T> items, Func<T, Vector3> positionOf)
        {
            if (positionOf == null) throw new ArgumentNullException(nameof(positionOf));

            _count = items.Length;
            _nodes = new Node[Mathf.Max(_count, 1)];

            for (var i = 0; i < _count; i++)
            {
                _nodes[i] = new Node
                {
                    Position = positionOf(items[i]),
                    Item = items[i],
                    Left = Nil,
                    Right = Nil
                };
            }

            var indices = new int[_count];
            for (var i = 0; i < _count; i++) indices[i] = i;

            _root = BuildRecursive(indices, 0, _count, 0);
        }

        /// <summary>最も近い点を探す。木が空なら false。</summary>
        /// <param name="target">基準の位置。</param>
        /// <param name="nearest">見つかった要素。</param>
        /// <param name="distance">そこまでの距離。</param>
        public bool TryFindNearest(Vector3 target, out T nearest, out float distance)
        {
            nearest = default;
            distance = 0f;

            if (_root == Nil)
            {
                return false;
            }

            var bestIndex = Nil;
            var bestSquared = float.PositiveInfinity;
            SearchNearest(_root, target, ref bestIndex, ref bestSquared);

            if (bestIndex == Nil) return false;

            nearest = _nodes[bestIndex].Item;
            distance = Mathf.Sqrt(bestSquared);
            return true;
        }

        /// <summary>半径内の点を全て <paramref name="results"/> に追加する。</summary>
        /// <param name="target">基準の位置。</param>
        /// <param name="radius">検索半径。</param>
        /// <param name="results">追記先。</param>
        public void QueryRadius(Vector3 target, float radius, FastList<T> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (_root == Nil) return;

            SearchRadius(_root, target, radius * radius, results);
        }

        private int BuildRecursive(int[] indices, int start, int length, int depth)
        {
            if (length <= 0) return Nil;

            var axis = depth % 3;
            var middle = start + length / 2;

            // 部分整列。全体を並べる必要はなく、中央値がその位置に来ればよい。
            SelectMedian(indices, start, start + length - 1, middle, axis);

            var nodeIndex = indices[middle];
            _nodes[nodeIndex].Axis = axis;

            var leftLength = middle - start;
            var rightLength = start + length - middle - 1;

            _nodes[nodeIndex].Left = BuildRecursive(indices, start, leftLength, depth + 1);
            _nodes[nodeIndex].Right = BuildRecursive(indices, middle + 1, rightLength, depth + 1);
            return nodeIndex;
        }

        /// <summary>クイックセレクト。<paramref name="target"/> の位置に中央値が来るまで分割する。</summary>
        private void SelectMedian(int[] indices, int low, int high, int target, int axis)
        {
            while (low < high)
            {
                var pivot = Partition(indices, low, high, axis);

                if (pivot == target) return;
                if (target < pivot) high = pivot - 1;
                else low = pivot + 1;
            }
        }

        private int Partition(int[] indices, int low, int high, int axis)
        {
            var pivotValue = Component(_nodes[indices[high]].Position, axis);
            var store = low;

            for (var i = low; i < high; i++)
            {
                if (Component(_nodes[indices[i]].Position, axis) >= pivotValue) continue;

                (indices[store], indices[i]) = (indices[i], indices[store]);
                store++;
            }

            (indices[store], indices[high]) = (indices[high], indices[store]);
            return store;
        }

        private void SearchNearest(int nodeIndex, Vector3 target, ref int bestIndex, ref float bestSquared)
        {
            while (true)
            {
                if (nodeIndex == Nil) return;

                ref var node = ref _nodes[nodeIndex];

                var distanceSquared = (node.Position - target).sqrMagnitude;
                if (distanceSquared < bestSquared)
                {
                    bestSquared = distanceSquared;
                    bestIndex = nodeIndex;
                }

                var delta = Component(target, node.Axis) - Component(node.Position, node.Axis);
                var near = delta < 0f ? node.Left : node.Right;
                var far = delta < 0f ? node.Right : node.Left;

                SearchNearest(near, target, ref bestIndex, ref bestSquared);

                // 分割面の向こうに、今の最良より近いものが有り得るときだけ渡る。
                if (delta * delta >= bestSquared) return;

                nodeIndex = far;
            }
        }

        private void SearchRadius(int nodeIndex, Vector3 target, float radiusSquared, FastList<T> results)
        {
            while (true)
            {
                if (nodeIndex == Nil) return;

                ref var node = ref _nodes[nodeIndex];

                if ((node.Position - target).sqrMagnitude <= radiusSquared) results.Add(node.Item);

                var delta = Component(target, node.Axis) - Component(node.Position, node.Axis);
                var near = delta < 0f ? node.Left : node.Right;
                var far = delta < 0f ? node.Right : node.Left;

                SearchRadius(near, target, radiusSquared, results);

                if (delta * delta > radiusSquared) return;

                nodeIndex = far;
            }
        }

        private static float Component(Vector3 vector, int axis) =>
            axis == 0 ? vector.x : axis == 1 ? vector.y : vector.z;
    }
}
