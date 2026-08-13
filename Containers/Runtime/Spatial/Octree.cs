using System;
using UnityEngine;

namespace Containers.Spatial
{
    /// <summary>
    /// <see cref="QuadTree{T}"/> の 3D 版。決まった体積を再帰的に 8 分割し、
    /// 要素がある場所だけを細かくする。
    /// <para>
    /// 静的、あるいはゆっくり動く集合に対する領域問い合わせに向く ——
    /// カリング候補の絞り込み、地点の検索、スポーン位置の判定。
    /// 毎フレーム動き回る大量の要素なら <see cref="SpatialHashGrid{T}"/> の方が安い。
    /// </para>
    /// </summary>
    public sealed class Octree<T>
    {
        private readonly struct Entry
        {
            public readonly T Item;
            public readonly Vector3 Position;

            public Entry(T item, Vector3 position)
            {
                Item = item;
                Position = position;
            }
        }

        private readonly Bounds _bounds;
        private readonly int _capacity;
        private readonly int _maxDepth;
        private readonly int _depth;

        private readonly FastList<Entry> _entries;
        private Octree<T>[] _children;

        /// <summary>領域と分割の条件を指定して作る。</summary>
        /// <param name="bounds">木が担当する箱。</param>
        /// <param name="capacity">分割を始める 1 ノードあたりの要素数。</param>
        /// <param name="maxDepth">これ以上は分割しない深さ。</param>
        public Octree(Bounds bounds, int capacity = 8, int maxDepth = 8) : this(bounds, capacity, maxDepth, 0) { }

        private Octree(Bounds bounds, int capacity, int maxDepth, int depth)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));

            _bounds = bounds;
            _capacity = capacity;
            _maxDepth = maxDepth;
            _depth = depth;
            _entries = new FastList<Entry>(capacity);
        }

        /// <summary>このノードが担当する箱。</summary>
        public Bounds Bounds => _bounds;

        /// <summary>まだ分割していないか。</summary>
        public bool IsLeaf => _children == null;

        /// <summary>この部分木に入っている要素数。</summary>
        public int Count { get; private set; }

        /// <summary>要素を追加する。位置が領域の外なら false。</summary>
        /// <param name="item">追加する要素。</param>
        /// <param name="position">その位置。</param>
        public bool Insert(T item, Vector3 position)
        {
            if (!_bounds.Contains(position)) return false;

            InsertCore(item, position);
            return true;
        }

        /// <summary>領域内であることが確定した要素を実際に入れる。</summary>
        private void InsertCore(T item, Vector3 position)
        {
            Count++;

            if (_children == null)
            {
                if (_entries.Count < _capacity || _depth >= _maxDepth)
                {
                    _entries.Add(new Entry(item, position));
                    return;
                }

                Subdivide();
            }

            ChildFor(position).InsertCore(item, position);
        }

        /// <summary>要素を取り除く。位置も一緒に渡す必要がある。</summary>
        /// <param name="item">取り除く要素。</param>
        /// <param name="position">登録したときの位置。</param>
        public bool Remove(T item, Vector3 position)
        {
            if (!_bounds.Contains(position)) return false;
            return RemoveCore(item, position);
        }

        private bool RemoveCore(T item, Vector3 position)
        {
            if (_children == null)
            {
                for (var i = 0; i < _entries.Count; i++)
                {
                    if (!Equals(_entries[i].Item, item)) continue;

                    _entries.RemoveAtSwapBack(i);
                    Count--;
                    return true;
                }

                return false;
            }

            // 挿入と同じ規則で子を選ぶ。全ての子を舐めると、
            // 分割面上の要素がどの子にも見つからないことがある。
            if (!ChildFor(position).RemoveCore(item, position)) return false;

            Count--;
            return true;
        }

        /// <summary>箱の中にある要素を <paramref name="results"/> に追加する。</summary>
        /// <param name="area">検索する箱。</param>
        /// <param name="results">追記先。</param>
        public void QueryBounds(Bounds area, FastList<T> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (Count == 0 || !_bounds.Intersects(area)) return;

            for (var i = 0; i < _entries.Count; i++)
            {
                if (area.Contains(_entries[i].Position)) results.Add(_entries[i].Item);
            }

            if (_children == null) return;
            for (var i = 0; i < _children.Length; i++) _children[i].QueryBounds(area, results);
        }

        /// <summary>球の中にある要素を <paramref name="results"/> に追加する。</summary>
        /// <param name="center">球の中心。</param>
        /// <param name="radius">球の半径。</param>
        /// <param name="results">追記先。</param>
        public void QuerySphere(Vector3 center, float radius, FastList<T> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (Count == 0) return;

            // 先に安い判定で落とす。ノードの箱と球の外接箱の交差。
            var area = new Bounds(center, Vector3.one * (radius * 2f));
            if (!_bounds.Intersects(area)) return;

            var radiusSquared = radius * radius;
            for (var i = 0; i < _entries.Count; i++)
            {
                if ((_entries[i].Position - center).sqrMagnitude <= radiusSquared) results.Add(_entries[i].Item);
            }

            if (_children == null) return;
            for (var i = 0; i < _children.Length; i++) _children[i].QuerySphere(center, radius, results);
        }

        /// <summary>全て捨てて未分割の状態に戻す。</summary>
        public void Clear()
        {
            _entries.Clear();
            _children = null;
            Count = 0;
        }

        /// <summary>全ノードの箱を集める。ギズモ描画用。</summary>
        /// <param name="results">追記先。</param>
        public void CollectBounds(FastList<Bounds> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            results.Add(_bounds);
            if (_children == null) return;
            for (var i = 0; i < _children.Length; i++) _children[i].CollectBounds(results);
        }

        private void Subdivide()
        {
            var quarter = _bounds.extents * 0.5f;
            var childSize = _bounds.extents;
            var center = _bounds.center;

            _children = new Octree<T>[8];
            var index = 0;

            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var z = -1; z <= 1; z += 2)
            {
                var childCenter = center + new Vector3(quarter.x * x, quarter.y * y, quarter.z * z);
                _children[index++] = new Octree<T>(new Bounds(childCenter, childSize), _capacity, _maxDepth, _depth + 1);
            }

            // 先に写して空にしてから配り直す。走査しながら再配置すると、
            // 配り先が _entries に追加した場合に無限ループになる。
            var pending = _entries.ToArray();
            _entries.Clear();

            for (var i = 0; i < pending.Length; i++)
            {
                ChildFor(pending[i].Position).InsertCore(pending[i].Item, pending[i].Position);
            }
        }

        /// <summary>
        /// 位置が属する子を、分割面との大小比較だけで決める。
        /// <para>
        /// 子の <c>Bounds</c> に <c>Contains</c> を試して回る実装は誤り。子の最大座標は
        /// <c>(center + quarter) + quarter</c>、親は <c>center + extents</c> と丸めの回数が違うため、
        /// 親には入るがどの子にも入らない薄い隙間が分割面ごとにできる。
        /// </para>
        /// <para>
        /// 子は x が 4、y が 2、z が 1 の重みで並べてあるので、添字はビット演算で決まる。
        /// </para>
        /// </summary>
        private Octree<T> ChildFor(Vector3 position)
        {
            var center = _bounds.center;
            var index = (position.x >= center.x ? 4 : 0)
                      | (position.y >= center.y ? 2 : 0)
                      | (position.z >= center.z ? 1 : 0);
            return _children[index];
        }
    }
}
