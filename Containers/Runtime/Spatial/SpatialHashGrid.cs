using System;
using System.Collections.Generic;
using UnityEngine;

namespace Containers.Spatial
{
    /// <summary>
    /// 空間を等間隔のセルに切り、ハッシュでバケットに振り分けることで、
    /// 動き回る大量のオブジェクトに対する近傍問い合わせをほぼ定数時間にする。
    /// <para>
    /// 「自分の近くに何があるか」を頻繁に訊くゲームループでは、たいていこれが最大の改善になる。
    /// 総当たりは O(n²) だし、<c>Physics.OverlapSphere</c> を全員ぶん毎フレーム呼ぶのは
    /// 確保が発生するうえ、物理エンジンが特化していない問いにブロードフェーズ全体のコストを払う。
    /// セルで分ければ、1 回の問い合わせは半径が実際に触れる数個のセルだけを見れば済む。
    /// </para>
    /// <para>
    /// 調整する値はセルサイズだけ。<b>典型的な問い合わせ半径と同じくらい</b>にする。
    /// 小さすぎると 1 回で多数のセルを歩き、大きすぎると 1 セルに候補が溜まりすぎる。
    /// </para>
    /// </summary>
    public sealed class SpatialHashGrid<T>
    {
        private readonly struct CellKey : IEquatable<CellKey>
        {
            public readonly int X, Y, Z;

            public CellKey(int x, int y, int z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public bool Equals(CellKey other) => X == other.X && Y == other.Y && Z == other.Z;
            public override bool Equals(object obj) => obj is CellKey other && Equals(other);

            public override int GetHashCode()
            {
                // 大きな奇素数を掛けることで、隣接セル同士が同じバケットに固まらないようにする。
                unchecked
                {
                    return X * 73856093 ^ Y * 19349663 ^ Z * 83492791;
                }
            }
        }

        private readonly Dictionary<CellKey, FastList<T>> _cells;
        private readonly Dictionary<T, CellKey> _placement;
        private readonly Stack<FastList<T>> _listPool = new Stack<FastList<T>>();
        private readonly IEqualityComparer<T> _comparer;
        private readonly float _cellSize;
        private readonly float _inverseCellSize;

        /// <summary>セルサイズを指定して作る。</summary>
        /// <param name="cellSize">典型的な問い合わせ半径と同程度にする。正の値が必要。</param>
        /// <param name="comparer">要素の比較方法。</param>
        public SpatialHashGrid(float cellSize, IEqualityComparer<T> comparer = null)
        {
            if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));

            _cellSize = cellSize;
            _inverseCellSize = 1f / cellSize;
            _comparer = comparer ?? EqualityComparer<T>.Default;
            _cells = new Dictionary<CellKey, FastList<T>>();
            _placement = new Dictionary<T, CellKey>(_comparer);
        }

        /// <summary>1 セルの一辺の長さ。</summary>
        public float CellSize => _cellSize;

        /// <summary>登録されている要素数。</summary>
        public int Count => _placement.Count;

        /// <summary>要素が 1 つ以上入っているセルの数。</summary>
        public int OccupiedCellCount => _cells.Count;

        /// <summary>要素を追加する。既に入っていれば移動になる。</summary>
        /// <param name="item">登録する要素。</param>
        /// <param name="position">ワールド座標。</param>
        public void Insert(T item, Vector3 position)
        {
            var key = ToCell(position);

            if (_placement.TryGetValue(item, out var existing))
            {
                if (existing.Equals(key)) return;
                RemoveFromCell(existing, item);
            }

            AddToCell(key, item);
            _placement[item] = key;
        }

        /// <summary>
        /// 位置を更新する。同じセルに留まる限り安いので（そしてそれが大半なので）、
        /// 全エージェントに毎フレーム呼んで構わない。
        /// </summary>
        /// <param name="item">更新する要素。</param>
        /// <param name="position">新しいワールド座標。</param>
        public void Update(T item, Vector3 position) => Insert(item, position);

        /// <summary>要素を取り除く。</summary>
        /// <param name="item">取り除く要素。</param>
        public bool Remove(T item)
        {
            if (!_placement.TryGetValue(item, out var key)) return false;

            RemoveFromCell(key, item);
            _placement.Remove(item);
            return true;
        }

        /// <summary>要素が登録されているか。</summary>
        /// <param name="item">探す要素。</param>
        public bool Contains(T item) => _placement.ContainsKey(item);

        /// <summary>全て捨てる。内部リストはプールに戻る。</summary>
        public void Clear()
        {
            foreach (var pair in _cells) Release(pair.Value);
            _cells.Clear();
            _placement.Clear();
        }

        /// <summary>
        /// 球に重なるセルの中身を全て <paramref name="results"/> に追加する。
        /// <para>
        /// 結果は<b>セル単位で正確、距離では不正確</b> —— 触れたセルの要素は、
        /// 半径から少しはみ出していても入る。厳密な距離が要るなら後段で絞ること。
        /// ここはブロードフェーズ。
        /// </para>
        /// </summary>
        /// <param name="center">球の中心。</param>
        /// <param name="radius">球の半径。</param>
        /// <param name="results">追記先。</param>
        public void QueryRadius(Vector3 center, float radius, FastList<T> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            var min = ToCell(center - new Vector3(radius, radius, radius));
            var max = ToCell(center + new Vector3(radius, radius, radius));

            for (var x = min.X; x <= max.X; x++)
            for (var y = min.Y; y <= max.Y; y++)
            for (var z = min.Z; z <= max.Z; z++)
            {
                if (_cells.TryGetValue(new CellKey(x, y, z), out var bucket)) results.AddRange(bucket);
            }
        }

        /// <summary>
        /// <see cref="QueryRadius"/> に厳密な距離判定を加えた版。
        /// 各候補の現在位置は <paramref name="positionOf"/> で読む。
        /// </summary>
        /// <param name="center">球の中心。</param>
        /// <param name="radius">球の半径。</param>
        /// <param name="positionOf">要素から位置を取り出す関数。</param>
        /// <param name="results">追記先。</param>
        public void QueryRadiusExact(Vector3 center, float radius, Func<T, Vector3> positionOf, FastList<T> results)
        {
            if (positionOf == null) throw new ArgumentNullException(nameof(positionOf));
            if (results == null) throw new ArgumentNullException(nameof(results));

            using var candidates = TempList<T>.Rent();
            QueryRadius(center, radius, candidates.List);

            var radiusSquared = radius * radius;
            for (var i = 0; i < candidates.List.Count; i++)
            {
                var candidate = candidates.List[i];
                if ((positionOf(candidate) - center).sqrMagnitude <= radiusSquared) results.Add(candidate);
            }
        }

        /// <summary>軸並行の箱に重なるセルの中身を全て追加する。</summary>
        /// <param name="bounds">検索する箱。</param>
        /// <param name="results">追記先。</param>
        public void QueryBounds(Bounds bounds, FastList<T> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            var min = ToCell(bounds.min);
            var max = ToCell(bounds.max);

            for (var x = min.X; x <= max.X; x++)
            for (var y = min.Y; y <= max.Y; y++)
            for (var z = min.Z; z <= max.Z; z++)
            {
                if (_cells.TryGetValue(new CellKey(x, y, z), out var bucket)) results.AddRange(bucket);
            }
        }

        /// <summary>
        /// <paramref name="maxRadius"/> の範囲で最も近い要素を探す。
        /// セルのリングを内側から広げていくので、近くで見つかれば早く打ち切れる。
        /// </summary>
        /// <param name="center">探索の中心。</param>
        /// <param name="maxRadius">探索する最大半径。</param>
        /// <param name="positionOf">要素から位置を取り出す関数。</param>
        /// <param name="nearest">見つかった要素。</param>
        public bool TryFindNearest(Vector3 center, float maxRadius, Func<T, Vector3> positionOf, out T nearest)
        {
            if (positionOf == null) throw new ArgumentNullException(nameof(positionOf));

            nearest = default;
            var bestSquared = maxRadius * maxRadius;
            var found = false;

            var maxRings = Mathf.CeilToInt(maxRadius * _inverseCellSize);
            var origin = ToCell(center);

            using var scratch = TempList<T>.Rent();

            for (var ring = 0; ring <= maxRings; ring++)
            {
                scratch.List.Clear();
                CollectRing(origin, ring, scratch.List);

                for (var i = 0; i < scratch.List.Count; i++)
                {
                    var candidate = scratch.List[i];
                    var distanceSquared = (positionOf(candidate) - center).sqrMagnitude;
                    if (distanceSquared > bestSquared) continue;

                    bestSquared = distanceSquared;
                    nearest = candidate;
                    found = true;
                }

                // 走査済みのリング内に収まる距離で見つかっていれば、外側のリングにこれより近いものは無い。
                var scannedDistance = ring * _cellSize;
                if (found && bestSquared <= scannedDistance * scannedDistance) break;
            }

            return found;
        }

        private void CollectRing(CellKey origin, int ring, FastList<T> results)
        {
            if (ring == 0)
            {
                if (_cells.TryGetValue(origin, out var bucket)) results.AddRange(bucket);
                return;
            }

            for (var x = -ring; x <= ring; x++)
            for (var y = -ring; y <= ring; y++)
            for (var z = -ring; z <= ring; z++)
            {
                // 立方体の殻だけを見る。内側は前のリングで既に走査済み。
                var onShell = Mathf.Abs(x) == ring || Mathf.Abs(y) == ring || Mathf.Abs(z) == ring;
                if (!onShell) continue;

                var key = new CellKey(origin.X + x, origin.Y + y, origin.Z + z);
                if (_cells.TryGetValue(key, out var bucket)) results.AddRange(bucket);
            }
        }

        private CellKey ToCell(Vector3 position) => new CellKey(
            Mathf.FloorToInt(position.x * _inverseCellSize),
            Mathf.FloorToInt(position.y * _inverseCellSize),
            Mathf.FloorToInt(position.z * _inverseCellSize));

        private void AddToCell(CellKey key, T item)
        {
            if (!_cells.TryGetValue(key, out var bucket))
            {
                bucket = _listPool.Count > 0 ? _listPool.Pop() : new FastList<T>();
                _cells[key] = bucket;
            }

            bucket.Add(item);
        }

        private void RemoveFromCell(CellKey key, T item)
        {
            if (!_cells.TryGetValue(key, out var bucket)) return;

            // 比較は _placement と同じ比較子で行う。FastList.RemoveSwapBack は
            // 既定の等値比較を使うため、独自比較子を渡された場合に
            // 別の要素を消すか、何も消さずにセルへ死んだ参照を残す。
            for (var i = 0; i < bucket.Count; i++)
            {
                if (!_comparer.Equals(bucket[i], item)) continue;

                bucket.RemoveAtSwapBack(i);
                break;
            }

            if (bucket.Count > 0) return;

            _cells.Remove(key);
            Release(bucket);
        }

        private void Release(FastList<T> bucket)
        {
            bucket.Clear();
            if (_listPool.Count < 128) _listPool.Push(bucket);
        }
    }
}
