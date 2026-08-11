using System;
using UnityEngine;

namespace Containers.Spatial
{
    /// <summary>
    /// 軸並行の箱を階層化する動的な BVH —— 物理エンジンがブロードフェーズに使う構造。
    /// <para>
    /// <see cref="Octree{T}"/> や <see cref="QuadTree{T}"/> が点を持つのに対し、こちらは体積を持つ。
    /// そのため大きさのあるオブジェクトが、触れる全セルに重複登録されるのではなく
    /// <b>1 つのノードにだけ入る</b>。自前のトリガー領域、RTS の範囲選択、
    /// <c>Physics</c> を通したくない弾丸処理など、大きさのある対象へのレイキャストや
    /// 重なり判定に向く。
    /// </para>
    /// <para>
    /// 挿入時に箱を少し膨らませて登録するので、小さな移動では作り直しが起きない ——
    /// <see cref="MoveProxy"/> は、実際の箱が膨らませた箱からはみ出したときだけ再構築する。
    /// </para>
    /// </summary>
    public sealed class DynamicAabbTree<T>
    {
        private struct Node
        {
            public Bounds Box;
            public T Item;
            public int Parent;
            public int Left;
            public int Right;

            /// <summary>-1 は空きリストの要素であることを表す。</summary>
            public int Height;

            public bool IsLeaf => Left == Nil;
        }

        private const int Nil = -1;

        private Node[] _nodes;
        private int _root = Nil;
        private int _nodeCount;
        private int _freeList;

        private readonly float _margin;

        /// <summary>容量と膨らませ量を指定して作る。</summary>
        /// <param name="capacity">最初に確保するノード数。足りなくなれば自動で伸びる。</param>
        /// <param name="margin">各ボックスを膨らませる量。精度を落とす代わりに再挿入が減る。</param>
        public DynamicAabbTree(int capacity = 16, float margin = 0.1f)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));

            _margin = Mathf.Max(0f, margin);
            _nodes = new Node[capacity];
            InitialiseFreeList(0);
            _freeList = 0;
        }

        /// <summary>登録されている要素数。</summary>
        public int Count { get; private set; }

        /// <summary>要素を登録し、以後の移動・削除に使うプロキシ ID を返す。</summary>
        /// <param name="item">登録する要素。</param>
        /// <param name="box">その占有領域。</param>
        public int Add(T item, Bounds box)
        {
            var proxy = AllocateNode();

            _nodes[proxy].Box = Fatten(box);
            _nodes[proxy].Item = item;
            _nodes[proxy].Height = 0;
            _nodes[proxy].Left = Nil;
            _nodes[proxy].Right = Nil;

            InsertLeaf(proxy);
            Count++;
            return proxy;
        }

        /// <summary>
        /// プロキシの箱を更新する。膨らませた箱の内側に収まっている限り何もしない ——
        /// 小さな移動ではこちらが大半になる。
        /// </summary>
        /// <param name="proxy">対象のプロキシ ID。</param>
        /// <param name="box">新しい占有領域。</param>
        public bool MoveProxy(int proxy, Bounds box)
        {
            if (!IsValidLeaf(proxy)) throw new ArgumentOutOfRangeException(nameof(proxy));
            if (ContainsBounds(_nodes[proxy].Box, box)) return false;

            var item = _nodes[proxy].Item;
            RemoveLeaf(proxy);

            _nodes[proxy].Box = Fatten(box);
            _nodes[proxy].Item = item;
            InsertLeaf(proxy);
            return true;
        }

        /// <summary>要素を取り除く。</summary>
        /// <param name="proxy">対象のプロキシ ID。</param>
        public void Remove(int proxy)
        {
            if (!IsValidLeaf(proxy)) throw new ArgumentOutOfRangeException(nameof(proxy));

            RemoveLeaf(proxy);
            FreeNode(proxy);
            Count--;
        }

        /// <summary>全て捨てる。発行済みのプロキシ ID は無効になる。</summary>
        public void Clear()
        {
            _root = Nil;
            Count = 0;
            _nodeCount = 0;
            InitialiseFreeList(0);
            _freeList = 0;
        }

        /// <summary>箱と重なる要素を全て <paramref name="results"/> に追加する。</summary>
        /// <param name="area">検索する箱。</param>
        /// <param name="results">追記先。</param>
        public void Query(Bounds area, FastList<T> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (_root == Nil) return;

            using var stack = TempList<int>.Rent();
            stack.List.Add(_root);

            while (stack.List.Count > 0)
            {
                var index = stack.List[stack.List.Count - 1];
                stack.List.RemoveAt(stack.List.Count - 1);

                if (index == Nil || !_nodes[index].Box.Intersects(area)) continue;

                if (_nodes[index].IsLeaf)
                {
                    results.Add(_nodes[index].Item);
                    continue;
                }

                stack.List.Add(_nodes[index].Left);
                stack.List.Add(_nodes[index].Right);
            }
        }

        /// <summary>レイが通る箱の要素を全て集める。並び順は不定。</summary>
        /// <param name="ray">飛ばすレイ。</param>
        /// <param name="maxDistance">レイの最大距離。</param>
        /// <param name="results">追記先。</param>
        public void QueryRay(Ray ray, float maxDistance, FastList<T> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (_root == Nil) return;

            using var stack = TempList<int>.Rent();
            stack.List.Add(_root);

            while (stack.List.Count > 0)
            {
                var index = stack.List[stack.List.Count - 1];
                stack.List.RemoveAt(stack.List.Count - 1);

                if (index == Nil) continue;
                if (!_nodes[index].Box.IntersectRay(ray, out var distance) || distance > maxDistance) continue;

                if (_nodes[index].IsLeaf)
                {
                    results.Add(_nodes[index].Item);
                    continue;
                }

                stack.List.Add(_nodes[index].Left);
                stack.List.Add(_nodes[index].Right);
            }
        }

        // ── 木の維持 ────────────────────────────────────────────────────────

        private void InsertLeaf(int leaf)
        {
            if (_root == Nil)
            {
                _root = leaf;
                _nodes[leaf].Parent = Nil;
                return;
            }

            // 表面積の増加が小さい方の子へ降りていく。
            var index = _root;
            while (!_nodes[index].IsLeaf)
            {
                var left = _nodes[index].Left;
                var right = _nodes[index].Right;

                var leftCost = SurfaceArea(Union(_nodes[left].Box, _nodes[leaf].Box)) - SurfaceArea(_nodes[left].Box);
                var rightCost = SurfaceArea(Union(_nodes[right].Box, _nodes[leaf].Box)) - SurfaceArea(_nodes[right].Box);

                index = leftCost < rightCost ? left : right;
            }

            var sibling = index;
            var oldParent = _nodes[sibling].Parent;
            var newParent = AllocateNode();

            _nodes[newParent].Parent = oldParent;
            _nodes[newParent].Box = Union(_nodes[leaf].Box, _nodes[sibling].Box);
            _nodes[newParent].Height = _nodes[sibling].Height + 1;
            _nodes[newParent].Left = sibling;
            _nodes[newParent].Right = leaf;

            _nodes[sibling].Parent = newParent;
            _nodes[leaf].Parent = newParent;

            if (oldParent == Nil) _root = newParent;
            else if (_nodes[oldParent].Left == sibling) _nodes[oldParent].Left = newParent;
            else _nodes[oldParent].Right = newParent;

            RefitAncestors(_nodes[leaf].Parent);
        }

        private void RemoveLeaf(int leaf)
        {
            if (leaf == _root)
            {
                _root = Nil;
                return;
            }

            var parent = _nodes[leaf].Parent;
            var grandParent = _nodes[parent].Parent;
            var sibling = _nodes[parent].Left == leaf ? _nodes[parent].Right : _nodes[parent].Left;

            if (grandParent == Nil)
            {
                _root = sibling;
                _nodes[sibling].Parent = Nil;
            }
            else
            {
                if (_nodes[grandParent].Left == parent) _nodes[grandParent].Left = sibling;
                else _nodes[grandParent].Right = sibling;

                _nodes[sibling].Parent = grandParent;
                RefitAncestors(grandParent);
            }

            FreeNode(parent);
        }

        private void RefitAncestors(int index)
        {
            while (index != Nil)
            {
                var left = _nodes[index].Left;
                var right = _nodes[index].Right;

                _nodes[index].Box = Union(_nodes[left].Box, _nodes[right].Box);
                _nodes[index].Height = 1 + Mathf.Max(_nodes[left].Height, _nodes[right].Height);

                index = _nodes[index].Parent;
            }
        }

        // ── ノードの確保 ────────────────────────────────────────────────────

        private int AllocateNode()
        {
            if (_freeList == Nil)
            {
                var oldLength = _nodes.Length;
                Array.Resize(ref _nodes, oldLength * 2);
                InitialiseFreeList(oldLength);
                _freeList = oldLength;
            }

            var index = _freeList;
            _freeList = _nodes[index].Parent;

            _nodes[index].Parent = Nil;
            _nodes[index].Left = Nil;
            _nodes[index].Right = Nil;
            _nodes[index].Height = 0;
            _nodeCount++;
            return index;
        }

        private void FreeNode(int index)
        {
            _nodes[index].Parent = _freeList;
            _nodes[index].Height = -1;
            _nodes[index].Item = default;
            _freeList = index;
            _nodeCount--;
        }

        private void InitialiseFreeList(int from)
        {
            for (var i = from; i < _nodes.Length; i++)
            {
                _nodes[i].Parent = i + 1 < _nodes.Length ? i + 1 : Nil;
                _nodes[i].Height = -1;
                _nodes[i].Left = Nil;
                _nodes[i].Right = Nil;

                // 保持していた値も捨てる。Clear がここを通るので、
                // 消し忘れるとスロットが再利用されるまで全ペイロードが参照され続ける。
                _nodes[i].Item = default;
            }
        }

        private bool IsValidLeaf(int proxy) =>
            (uint)proxy < (uint)_nodes.Length && _nodes[proxy].Height >= 0 && _nodes[proxy].IsLeaf;

        private Bounds Fatten(Bounds box)
        {
            box.Expand(_margin * 2f);
            return box;
        }

        private static Bounds Union(Bounds a, Bounds b)
        {
            var union = a;
            union.Encapsulate(b);
            return union;
        }

        private static bool ContainsBounds(Bounds outer, Bounds inner) =>
            outer.Contains(inner.min) && outer.Contains(inner.max);

        private static float SurfaceArea(Bounds box)
        {
            var size = box.size;
            return 2f * (size.x * size.y + size.y * size.z + size.z * size.x);
        }
    }
}
