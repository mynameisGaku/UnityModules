using UnityEngine;

namespace Containers.Spatial
{
    /// <summary>
    /// 決まった領域を再帰的に 4 分割する 2D の空間分割。要素がある場所だけを細かくする。
    /// <para>
    /// <see cref="SpatialHashGrid{T}"/> との使い分けは分布次第。均一グリッドは空白にもメモリを使い、
    /// 1 箇所に固まると 1 セルに全部入って意味を失う。分布が偏る場合や問い合わせの大きさが
    /// まちまちな場合はこちらが有利。ただし挿入が O(log n) になり、移動は削除＋再挿入なので、
    /// <b>毎フレーム動き回る大量の要素にはハッシュグリッドの方が向く</b>。
    /// </para>
    /// </summary>
    public sealed class QuadTree<T>
    {
        private readonly struct Entry
        {
            public readonly T Item;
            public readonly Vector2 Position;

            public Entry(T item, Vector2 position)
            {
                Item = item;
                Position = position;
            }
        }

        private readonly Rect _bounds;
        private readonly int _capacity;
        private readonly int _maxDepth;
        private readonly int _depth;

        private readonly FastList<Entry> _entries;
        private QuadTree<T>[] _children;

        /// <summary>領域と分割の条件を指定して作る。</summary>
        /// <param name="bounds">木が担当する矩形。</param>
        /// <param name="capacity">分割を始める 1 ノードあたりの要素数。</param>
        /// <param name="maxDepth">これ以上は分割しない深さ。</param>
        public QuadTree(Rect bounds, int capacity = 8, int maxDepth = 8) : this(bounds, capacity, maxDepth, 0) { }

        private QuadTree(Rect bounds, int capacity, int maxDepth, int depth)
        {
            if (capacity <= 0) throw new System.ArgumentOutOfRangeException(nameof(capacity));

            _bounds = bounds;
            _capacity = capacity;
            _maxDepth = maxDepth;
            _depth = depth;
            _entries = new FastList<Entry>(capacity);
        }

        /// <summary>このノードが担当する矩形。</summary>
        public Rect Bounds => _bounds;

        /// <summary>まだ分割していないか。</summary>
        public bool IsLeaf => _children == null;

        /// <summary>この部分木に入っている要素数。</summary>
        public int Count { get; private set; }

        /// <summary>要素を追加する。位置が領域の外なら false。</summary>
        /// <param name="item">追加する要素。</param>
        /// <param name="position">その位置。</param>
        public bool Insert(T item, Vector2 position)
        {
            if (!_bounds.Contains(position)) return false;

            InsertCore(item, position);
            return true;
        }

        /// <summary>領域内であることが確定した要素を実際に入れる。</summary>
        private void InsertCore(T item, Vector2 position)
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
        public bool Remove(T item, Vector2 position)
        {
            if (!_bounds.Contains(position)) return false;
            return RemoveCore(item, position);
        }

        private bool RemoveCore(T item, Vector2 position)
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
            // 分割線上の要素がどの子にも見つからないことがある。
            if (!ChildFor(position).RemoveCore(item, position)) return false;

            Count--;
            return true;
        }

        /// <summary>矩形の中にある要素を <paramref name="results"/> に追加する。</summary>
        /// <param name="area">検索する矩形。</param>
        /// <param name="results">追記先。</param>
        public void QueryRect(Rect area, FastList<T> results)
        {
            if (results == null) throw new System.ArgumentNullException(nameof(results));
            if (Count == 0 || !_bounds.Overlaps(area)) return;

            for (var i = 0; i < _entries.Count; i++)
            {
                if (area.Contains(_entries[i].Position)) results.Add(_entries[i].Item);
            }

            if (_children == null) return;
            for (var i = 0; i < _children.Length; i++) _children[i].QueryRect(area, results);
        }

        /// <summary>円の中にある要素を <paramref name="results"/> に追加する。</summary>
        /// <param name="center">円の中心。</param>
        /// <param name="radius">円の半径。</param>
        /// <param name="results">追記先。</param>
        public void QueryCircle(Vector2 center, float radius, FastList<T> results)
        {
            if (results == null) throw new System.ArgumentNullException(nameof(results));
            if (Count == 0) return;

            var area = new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f);
            if (!_bounds.Overlaps(area)) return;

            var radiusSquared = radius * radius;
            for (var i = 0; i < _entries.Count; i++)
            {
                if ((_entries[i].Position - center).sqrMagnitude <= radiusSquared) results.Add(_entries[i].Item);
            }

            if (_children == null) return;
            for (var i = 0; i < _children.Length; i++) _children[i].QueryCircle(center, radius, results);
        }

        /// <summary>全て捨てて未分割の状態に戻す。</summary>
        public void Clear()
        {
            _entries.Clear();
            _children = null;
            Count = 0;
        }

        /// <summary>全ノードの矩形を集める。ギズモで分割の様子を描くのに使う。</summary>
        /// <param name="results">追記先。</param>
        public void CollectBounds(FastList<Rect> results)
        {
            if (results == null) throw new System.ArgumentNullException(nameof(results));

            results.Add(_bounds);
            if (_children == null) return;
            for (var i = 0; i < _children.Length; i++) _children[i].CollectBounds(results);
        }

        private void Subdivide()
        {
            var half = _bounds.size * 0.5f;
            var min = _bounds.min;

            _children = new[]
            {
                Child(new Rect(min.x, min.y, half.x, half.y)),
                Child(new Rect(min.x + half.x, min.y, half.x, half.y)),
                Child(new Rect(min.x, min.y + half.y, half.x, half.y)),
                Child(new Rect(min.x + half.x, min.y + half.y, half.x, half.y))
            };

            // 先に写して空にしてから配り直す。配り先が _entries に触れる可能性がある限り、
            // 走査しながら再配置してはいけない（自己追加で無限ループになる）。
            var pending = _entries.ToArray();
            _entries.Clear();

            for (var i = 0; i < pending.Length; i++)
            {
                // Count は既にこのノードで数えてあるので、子側で二重に数えないよう
                // InsertCore ではなく直接子の InsertCore を呼び、親の Count は触らない。
                ChildFor(pending[i].Position).InsertCore(pending[i].Item, pending[i].Position);
            }
        }

        private QuadTree<T> Child(Rect bounds) => new QuadTree<T>(bounds, _capacity, _maxDepth, _depth + 1);

        /// <summary>
        /// 位置が属する子を、分割線との大小比較だけで決める。
        /// <para>
        /// 子の矩形に <c>Contains</c> を試して回る実装は誤り。子の境界は親の境界から
        /// 2 回の丸めを経て作られるため、親には入るがどの子にも入らない点が
        /// 分割線の近傍に存在しうる。ここでは必ずちょうど 1 つの子が選ばれる。
        /// </para>
        /// </summary>
        private QuadTree<T> ChildFor(Vector2 position)
        {
            var center = _bounds.center;
            var index = (position.x >= center.x ? 1 : 0) | (position.y >= center.y ? 2 : 0);
            return _children[index];
        }
    }
}
