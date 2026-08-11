using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Containers.Spatial
{
    /// <summary>
    /// 2 次元の密なセル配列と、その周りに必ず書かれることになる雑務 ——
    /// ワールド座標との変換、境界を越えない安全なアクセス、近傍の走査。
    /// <para>
    /// ボードゲーム、タイルマップ、タワーディフェンス、ローグライク、フローフィールド、影響マップ。
    /// 実体は 1 本の平坦な配列なので、走査はメモリを線形に舐める。
    /// </para>
    /// </summary>
    public sealed class Grid2D<T> : IEnumerable<T>
    {
        private static readonly Vector2Int[] Neighbours4 =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        private static readonly Vector2Int[] Neighbours8 =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };

        private readonly T[] _cells;

        /// <summary>大きさとワールド座標系での配置を指定して作る。</summary>
        /// <param name="width">横のセル数。</param>
        /// <param name="height">縦のセル数。</param>
        /// <param name="cellSize">1 セルの一辺の長さ。</param>
        /// <param name="origin">セル (0,0) の角に対応するワールド座標。</param>
        public Grid2D(int width, int height, float cellSize = 1f, Vector3 origin = default)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));

            Width = width;
            Height = height;
            CellSize = cellSize;
            Origin = origin;
            _cells = new T[width * height];
        }

        /// <summary>横のセル数。</summary>
        public int Width { get; }

        /// <summary>縦のセル数。</summary>
        public int Height { get; }

        /// <summary>総セル数。</summary>
        public int Count => _cells.Length;

        /// <summary>1 セルの一辺の長さ。</summary>
        public float CellSize { get; }

        /// <summary>セル (0,0) の角に対応するワールド座標。</summary>
        public Vector3 Origin { get; }

        /// <summary>平坦な実体配列。行優先で <c>index = y * Width + x</c>。</summary>
        public T[] Cells => _cells;

        /// <summary>全セルを指す <see cref="Span{T}"/>。</summary>
        public Span<T> AsSpan() => _cells.AsSpan();

        /// <summary>セルへの参照。範囲外は例外。</summary>
        public ref T this[int x, int y]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (!InBounds(x, y)) throw new ArgumentOutOfRangeException($"({x},{y}) は {Width}x{Height} の外。");
                return ref _cells[y * Width + x];
            }
        }

        /// <summary>セルへの参照。範囲外は例外。</summary>
        public ref T this[Vector2Int cell] => ref this[cell.x, cell.y];

        /// <summary>座標がグリッドの内側か。</summary>
        /// <param name="x">横位置。</param>
        /// <param name="y">縦位置。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

        /// <summary>座標がグリッドの内側か。</summary>
        /// <param name="cell">セル座標。</param>
        public bool InBounds(Vector2Int cell) => InBounds(cell.x, cell.y);

        /// <summary>セルを読む。範囲外なら <paramref name="fallback"/>。</summary>
        /// <param name="x">横位置。</param>
        /// <param name="y">縦位置。</param>
        /// <param name="fallback">範囲外のときに返す値。</param>
        public T GetOrDefault(int x, int y, T fallback = default) =>
            InBounds(x, y) ? _cells[y * Width + x] : fallback;

        /// <summary>セルを読む。範囲外なら false。</summary>
        /// <param name="x">横位置。</param>
        /// <param name="y">縦位置。</param>
        /// <param name="value">読み取った値。</param>
        public bool TryGet(int x, int y, out T value)
        {
            if (!InBounds(x, y))
            {
                value = default;
                return false;
            }

            value = _cells[y * Width + x];
            return true;
        }

        /// <summary>セルを書く。範囲外なら例外ではなく false。</summary>
        /// <param name="x">横位置。</param>
        /// <param name="y">縦位置。</param>
        /// <param name="value">書き込む値。</param>
        public bool TrySet(int x, int y, in T value)
        {
            if (!InBounds(x, y)) return false;

            _cells[y * Width + x] = value;
            return true;
        }

        /// <summary>全セルを同じ値で埋める。</summary>
        /// <param name="value">埋める値。</param>
        public void Fill(T value) => _cells.AsSpan().Fill(value);

        /// <summary>全セルを既定値に戻す。</summary>
        public void Clear() => Array.Clear(_cells, 0, _cells.Length);

        // ── 座標変換 ────────────────────────────────────────────────────────

        /// <summary>
        /// ワールド座標を含むセル。グリッドの外を返すことがあるので
        /// <see cref="InBounds(Vector2Int)"/> で確認すること。XZ 平面として扱う。
        /// </summary>
        /// <param name="world">ワールド座標。</param>
        public Vector2Int WorldToCell(Vector3 world)
        {
            var local = world - Origin;
            return new Vector2Int(Mathf.FloorToInt(local.x / CellSize), Mathf.FloorToInt(local.z / CellSize));
        }

        /// <summary><see cref="WorldToCell"/> の XY 平面版。2D ゲーム向け。</summary>
        /// <param name="world">ワールド座標。</param>
        public Vector2Int WorldToCellXY(Vector3 world)
        {
            var local = world - Origin;
            return new Vector2Int(Mathf.FloorToInt(local.x / CellSize), Mathf.FloorToInt(local.y / CellSize));
        }

        /// <summary>セル中心のワールド座標。XZ 平面。</summary>
        /// <param name="x">横位置。</param>
        /// <param name="y">縦位置。</param>
        public Vector3 CellToWorld(int x, int y) =>
            Origin + new Vector3((x + 0.5f) * CellSize, 0f, (y + 0.5f) * CellSize);

        /// <summary>セル中心のワールド座標。XZ 平面。</summary>
        /// <param name="cell">セル座標。</param>
        public Vector3 CellToWorld(Vector2Int cell) => CellToWorld(cell.x, cell.y);

        /// <summary>セル中心のワールド座標。XY 平面。</summary>
        /// <param name="x">横位置。</param>
        /// <param name="y">縦位置。</param>
        public Vector3 CellToWorldXY(int x, int y) =>
            Origin + new Vector3((x + 0.5f) * CellSize, (y + 0.5f) * CellSize, 0f);

        // ── 近傍と領域 ──────────────────────────────────────────────────────

        /// <summary>グリッド内にある近傍セルを <paramref name="results"/> に追加する。</summary>
        /// <param name="cell">中心のセル。</param>
        /// <param name="results">追記先。</param>
        /// <param name="includeDiagonals">斜めも含めるか。</param>
        public void GetNeighbours(Vector2Int cell, FastList<Vector2Int> results, bool includeDiagonals = false)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            var offsets = includeDiagonals ? Neighbours8 : Neighbours4;
            for (var i = 0; i < offsets.Length; i++)
            {
                var neighbour = cell + offsets[i];
                if (InBounds(neighbour)) results.Add(neighbour);
            }
        }

        /// <summary>確保なしで近傍を走査する。</summary>
        /// <param name="cell">中心のセル。</param>
        /// <param name="includeDiagonals">斜めも含めるか。</param>
        public NeighbourEnumerator Neighbours(Vector2Int cell, bool includeDiagonals = false) =>
            new NeighbourEnumerator(this, cell, includeDiagonals);

        /// <summary>矩形内のセル座標を、グリッドで切り取ったうえで列挙する。</summary>
        /// <param name="region">対象の矩形。</param>
        /// <param name="results">追記先。</param>
        public void GetRegion(RectInt region, FastList<Vector2Int> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            var xMin = Mathf.Max(region.xMin, 0);
            var xMax = Mathf.Min(region.xMax, Width);
            var yMin = Mathf.Max(region.yMin, 0);
            var yMax = Mathf.Min(region.yMax, Height);

            for (var y = yMin; y < yMax; y++)
            for (var x = xMin; x < xMax; x++)
            {
                results.Add(new Vector2Int(x, y));
            }
        }

        /// <summary>マンハッタン距離。4 方向移動のときに正しい距離。</summary>
        /// <param name="a">始点。</param>
        /// <param name="b">終点。</param>
        public static int ManhattanDistance(Vector2Int a, Vector2Int b) =>
            Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        /// <summary>チェビシェフ距離。斜めが直進と同じコストのときに正しい距離。</summary>
        /// <param name="a">始点。</param>
        /// <param name="b">終点。</param>
        public static int ChebyshevDistance(Vector2Int a, Vector2Int b) =>
            Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        /// <summary>全セルを行優先で走査する。</summary>
        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_cells).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _cells.GetEnumerator();

        /// <summary>グリッド内の近傍セルを走査する構造体列挙子。</summary>
        public struct NeighbourEnumerator
        {
            private readonly Grid2D<T> _grid;
            private readonly Vector2Int _cell;
            private readonly Vector2Int[] _offsets;
            private int _index;

            internal NeighbourEnumerator(Grid2D<T> grid, Vector2Int cell, bool includeDiagonals)
            {
                _grid = grid;
                _cell = cell;
                _offsets = includeDiagonals ? Neighbours8 : Neighbours4;
                _index = -1;
                Current = default;
            }

            /// <summary>今の近傍セル。</summary>
            public Vector2Int Current { get; private set; }

            /// <summary><c>foreach</c> できるよう自分自身を返す。</summary>
            public NeighbourEnumerator GetEnumerator() => this;

            /// <summary>次のグリッド内の近傍へ進む。</summary>
            public bool MoveNext()
            {
                while (++_index < _offsets.Length)
                {
                    var candidate = _cell + _offsets[_index];
                    if (!_grid.InBounds(candidate)) continue;

                    Current = candidate;
                    return true;
                }

                return false;
            }
        }
    }
}
