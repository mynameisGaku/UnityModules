using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Containers.Spatial
{
    /// <summary>
    /// 3 次元の密なセル配列。ワールド座標との変換と境界安全なアクセスを備える。
    /// ボクセル、3D のフローフィールド、占有グリッド、チャンク分割した地形。
    /// <para>
    /// 実体は <c>(z * Height + y) * Width + x</c> で並ぶ 1 本の配列なので、
    /// x を最内周で回すとメモリを線形に舐める。
    /// </para>
    /// </summary>
    public sealed class Grid3D<T> : IEnumerable<T>
    {
        private static readonly Vector3Int[] Neighbours6 =
        {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
        };

        private readonly T[] _cells;

        /// <summary>大きさとワールド座標系での配置を指定して作る。</summary>
        /// <param name="width">X 方向のセル数。</param>
        /// <param name="height">Y 方向のセル数。</param>
        /// <param name="depth">Z 方向のセル数。</param>
        /// <param name="cellSize">1 セルの一辺の長さ。</param>
        /// <param name="origin">セル (0,0,0) の角に対応するワールド座標。</param>
        public Grid3D(int width, int height, int depth, float cellSize = 1f, Vector3 origin = default)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (depth <= 0) throw new ArgumentOutOfRangeException(nameof(depth));
            if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));

            Width = width;
            Height = height;
            Depth = depth;
            CellSize = cellSize;
            Origin = origin;
            _cells = new T[width * height * depth];
        }

        /// <summary>X 方向のセル数。</summary>
        public int Width { get; }

        /// <summary>Y 方向のセル数。</summary>
        public int Height { get; }

        /// <summary>Z 方向のセル数。</summary>
        public int Depth { get; }

        /// <summary>総セル数。</summary>
        public int Count => _cells.Length;

        /// <summary>1 セルの一辺の長さ。</summary>
        public float CellSize { get; }

        /// <summary>セル (0,0,0) の角に対応するワールド座標。</summary>
        public Vector3 Origin { get; }

        /// <summary>平坦な実体配列。</summary>
        public T[] Cells => _cells;

        /// <summary>全セルを指す <see cref="Span{T}"/>。</summary>
        public Span<T> AsSpan() => _cells.AsSpan();

        /// <summary>セルへの参照。範囲外は例外。</summary>
        public ref T this[int x, int y, int z]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (!InBounds(x, y, z))
                    throw new ArgumentOutOfRangeException($"({x},{y},{z}) は {Width}x{Height}x{Depth} の外。");
                return ref _cells[((z * Height) + y) * Width + x];
            }
        }

        /// <summary>セルへの参照。範囲外は例外。</summary>
        public ref T this[Vector3Int cell] => ref this[cell.x, cell.y, cell.z];

        /// <summary>座標がグリッドの内側か。</summary>
        /// <param name="x">X 位置。</param>
        /// <param name="y">Y 位置。</param>
        /// <param name="z">Z 位置。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool InBounds(int x, int y, int z) =>
            (uint)x < (uint)Width && (uint)y < (uint)Height && (uint)z < (uint)Depth;

        /// <summary>座標がグリッドの内側か。</summary>
        /// <param name="cell">セル座標。</param>
        public bool InBounds(Vector3Int cell) => InBounds(cell.x, cell.y, cell.z);

        /// <summary>セルを読む。範囲外なら <paramref name="fallback"/>。</summary>
        /// <param name="x">X 位置。</param>
        /// <param name="y">Y 位置。</param>
        /// <param name="z">Z 位置。</param>
        /// <param name="fallback">範囲外のときに返す値。</param>
        public T GetOrDefault(int x, int y, int z, T fallback = default) =>
            InBounds(x, y, z) ? _cells[((z * Height) + y) * Width + x] : fallback;

        /// <summary>セルを読む。範囲外なら false。</summary>
        /// <param name="x">X 位置。</param>
        /// <param name="y">Y 位置。</param>
        /// <param name="z">Z 位置。</param>
        /// <param name="value">読み取った値。</param>
        public bool TryGet(int x, int y, int z, out T value)
        {
            if (!InBounds(x, y, z))
            {
                value = default;
                return false;
            }

            value = _cells[((z * Height) + y) * Width + x];
            return true;
        }

        /// <summary>セルを書く。範囲外なら例外ではなく false。</summary>
        /// <param name="x">X 位置。</param>
        /// <param name="y">Y 位置。</param>
        /// <param name="z">Z 位置。</param>
        /// <param name="value">書き込む値。</param>
        public bool TrySet(int x, int y, int z, in T value)
        {
            if (!InBounds(x, y, z)) return false;

            _cells[((z * Height) + y) * Width + x] = value;
            return true;
        }

        /// <summary>全セルを同じ値で埋める。</summary>
        /// <param name="value">埋める値。</param>
        public void Fill(T value) => _cells.AsSpan().Fill(value);

        /// <summary>全セルを既定値に戻す。</summary>
        public void Clear() => Array.Clear(_cells, 0, _cells.Length);

        /// <summary>ワールド座標を含むセル。グリッドの外を返すことがある。</summary>
        /// <param name="world">ワールド座標。</param>
        public Vector3Int WorldToCell(Vector3 world)
        {
            var local = world - Origin;
            return new Vector3Int(
                Mathf.FloorToInt(local.x / CellSize),
                Mathf.FloorToInt(local.y / CellSize),
                Mathf.FloorToInt(local.z / CellSize));
        }

        /// <summary>セル中心のワールド座標。</summary>
        /// <param name="x">X 位置。</param>
        /// <param name="y">Y 位置。</param>
        /// <param name="z">Z 位置。</param>
        public Vector3 CellToWorld(int x, int y, int z) =>
            Origin + new Vector3((x + 0.5f) * CellSize, (y + 0.5f) * CellSize, (z + 0.5f) * CellSize);

        /// <summary>セル中心のワールド座標。</summary>
        /// <param name="cell">セル座標。</param>
        public Vector3 CellToWorld(Vector3Int cell) => CellToWorld(cell.x, cell.y, cell.z);

        /// <summary>グリッド全体が占めるワールド空間の箱。</summary>
        public Bounds WorldBounds
        {
            get
            {
                var size = new Vector3(Width, Height, Depth) * CellSize;
                return new Bounds(Origin + size * 0.5f, size);
            }
        }

        /// <summary>グリッド内にある面隣接の近傍を <paramref name="results"/> に追加する。</summary>
        /// <param name="cell">中心のセル。</param>
        /// <param name="results">追記先。</param>
        public void GetNeighbours(Vector3Int cell, FastList<Vector3Int> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            for (var i = 0; i < Neighbours6.Length; i++)
            {
                var neighbour = cell + Neighbours6[i];
                if (InBounds(neighbour)) results.Add(neighbour);
            }
        }

        /// <summary>全セルを走査する。</summary>
        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_cells).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _cells.GetEnumerator();
    }
}
