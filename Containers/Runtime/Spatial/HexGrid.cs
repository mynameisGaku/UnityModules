using System;
using System.Collections.Generic;
using UnityEngine;

namespace Containers.Spatial
{
    /// <summary>
    /// ヘックスの軸座標。3 つ目の立方体座標は <c>S = -Q - R</c> で決まるので持たない ——
    /// この関係があるおかげで距離や回転の計算が素直な引き算で書ける。
    /// </summary>
    [Serializable]
    public struct Hex : IEquatable<Hex>
    {
        /// <summary>原点。</summary>
        public static readonly Hex Zero = new Hex(0, 0);

        /// <summary>6 方向の隣接オフセット。東から反時計回り。</summary>
        public static readonly Hex[] Directions =
        {
            new Hex(1, 0), new Hex(1, -1), new Hex(0, -1),
            new Hex(-1, 0), new Hex(-1, 1), new Hex(0, 1)
        };

        /// <summary>軸座標の Q 成分。</summary>
        public int Q;

        /// <summary>軸座標の R 成分。</summary>
        public int R;

        /// <summary>軸座標を指定して作る。</summary>
        /// <param name="q">Q 成分。</param>
        /// <param name="r">R 成分。</param>
        public Hex(int q, int r)
        {
            Q = q;
            R = r;
        }

        /// <summary>導出される 3 つ目の立方体座標。</summary>
        public int S => -Q - R;

        /// <summary>指定方向の隣。方向は 6 で折り返す。</summary>
        /// <param name="direction">0〜5 の方向。</param>
        public Hex Neighbour(int direction) => this + Directions[((direction % 6) + 6) % 6];

        /// <summary>2 つのヘックスの間の歩数。立方体距離の半分。</summary>
        /// <param name="a">始点。</param>
        /// <param name="b">終点。</param>
        public static int Distance(Hex a, Hex b)
        {
            var difference = a - b;
            return (Mathf.Abs(difference.Q) + Mathf.Abs(difference.R) + Mathf.Abs(difference.S)) / 2;
        }

        /// <summary>加算。</summary>
        public static Hex operator +(Hex a, Hex b) => new Hex(a.Q + b.Q, a.R + b.R);

        /// <summary>減算。</summary>
        public static Hex operator -(Hex a, Hex b) => new Hex(a.Q - b.Q, a.R - b.R);

        /// <summary>スカラー倍。</summary>
        public static Hex operator *(Hex a, int scale) => new Hex(a.Q * scale, a.R * scale);

        /// <summary>座標が一致するか。</summary>
        /// <param name="other">比較対象。</param>
        public bool Equals(Hex other) => Q == other.Q && R == other.R;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is Hex other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => unchecked((Q * 397) ^ R);

        /// <inheritdoc/>
        public override string ToString() => $"Hex({Q},{R})";

        /// <summary>等値比較。</summary>
        public static bool operator ==(Hex a, Hex b) => a.Equals(b);

        /// <summary>非等値比較。</summary>
        public static bool operator !=(Hex a, Hex b) => !a.Equals(b);
    }

    /// <summary>
    /// ヘックスで敷き詰めたマップ。毎回書くと必ず間違える座標計算をまとめてある ——
    /// 隣接、距離、リング、スパイラル、直線、ワールド座標との相互変換。
    /// <para>既定は尖った頂点が上（pointy-top）。<see cref="FlatTop"/> でもう一方の向きになる。</para>
    /// </summary>
    public sealed class HexGrid<T>
    {
        private readonly Dictionary<Hex, T> _cells;

        /// <summary>ヘックスの大きさと向きを指定して作る。</summary>
        /// <param name="hexSize">中心から頂点までの距離。</param>
        /// <param name="flatTop">平らな辺が上を向く配置にするか。</param>
        /// <param name="capacity">最初に確保するセル数。</param>
        public HexGrid(float hexSize = 1f, bool flatTop = false, int capacity = 0)
        {
            if (hexSize <= 0f) throw new ArgumentOutOfRangeException(nameof(hexSize));

            HexSize = hexSize;
            FlatTop = flatTop;
            _cells = new Dictionary<Hex, T>(capacity);
        }

        /// <summary>中心から頂点までの距離。</summary>
        public float HexSize { get; }

        /// <summary>平らな辺が上を向く配置か。</summary>
        public bool FlatTop { get; }

        /// <summary>登録されているセル数。</summary>
        public int Count => _cells.Count;

        /// <summary>値が入っている座標の一覧。</summary>
        public Dictionary<Hex, T>.KeyCollection Coordinates => _cells.Keys;

        /// <summary>セルの値。</summary>
        public T this[Hex hex]
        {
            get => _cells[hex];
            set => _cells[hex] = value;
        }

        /// <summary>値を読む。無ければ false。</summary>
        /// <param name="hex">座標。</param>
        /// <param name="value">読み取った値。</param>
        public bool TryGet(Hex hex, out T value) => _cells.TryGetValue(hex, out value);

        /// <summary>座標に値が入っているか。</summary>
        /// <param name="hex">座標。</param>
        public bool Contains(Hex hex) => _cells.ContainsKey(hex);

        /// <summary>値を設定する。</summary>
        /// <param name="hex">座標。</param>
        /// <param name="value">値。</param>
        public void Set(Hex hex, T value) => _cells[hex] = value;

        /// <summary>セルを取り除く。</summary>
        /// <param name="hex">座標。</param>
        public bool Remove(Hex hex) => _cells.Remove(hex);

        /// <summary>全て捨てる。</summary>
        public void Clear() => _cells.Clear();

        // ── 形 ──────────────────────────────────────────────────────────────

        /// <summary>中心からちょうど <paramref name="radius"/> 歩のヘックスを並べる。</summary>
        /// <param name="center">中心。</param>
        /// <param name="radius">半径（歩数）。</param>
        /// <param name="results">追記先。</param>
        public static void Ring(Hex center, int radius, FastList<Hex> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));

            if (radius == 0)
            {
                results.Add(center);
                return;
            }

            // 方向 4 へ radius 歩進んだ地点から始め、6 つの辺を順に歩く。
            var current = center + Hex.Directions[4] * radius;

            for (var side = 0; side < 6; side++)
            {
                for (var step = 0; step < radius; step++)
                {
                    results.Add(current);
                    current = current.Neighbour(side);
                }
            }
        }

        /// <summary><paramref name="radius"/> 歩以内の全ヘックスを、中心から外へリング順に並べる。</summary>
        /// <param name="center">中心。</param>
        /// <param name="radius">半径（歩数）。</param>
        /// <param name="results">追記先。</param>
        public static void Spiral(Hex center, int radius, FastList<Hex> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            for (var ring = 0; ring <= radius; ring++) Ring(center, ring, results);
        }

        /// <summary>このグリッドに実際に存在する隣接セルを並べる。</summary>
        /// <param name="hex">中心。</param>
        /// <param name="results">追記先。</param>
        public void GetNeighbours(Hex hex, FastList<Hex> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            for (var i = 0; i < 6; i++)
            {
                var neighbour = hex.Neighbour(i);
                if (_cells.ContainsKey(neighbour)) results.Add(neighbour);
            }
        }

        /// <summary>2 点を結ぶ直線が通るヘックスを並べる。</summary>
        /// <param name="a">始点。</param>
        /// <param name="b">終点。</param>
        /// <param name="results">追記先。</param>
        public static void Line(Hex a, Hex b, FastList<Hex> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            var steps = Hex.Distance(a, b);
            if (steps == 0)
            {
                results.Add(a);
                return;
            }

            for (var i = 0; i <= steps; i++)
            {
                var t = (float)i / steps;
                results.Add(Round(Mathf.Lerp(a.Q, b.Q, t), Mathf.Lerp(a.R, b.R, t)));
            }
        }

        // ── ワールド座標 ────────────────────────────────────────────────────

        /// <summary>ヘックス中心のワールド座標。XZ 平面。</summary>
        /// <param name="hex">座標。</param>
        public Vector3 HexToWorld(Hex hex)
        {
            float x, z;

            if (FlatTop)
            {
                x = HexSize * (1.5f * hex.Q);
                z = HexSize * (Mathf.Sqrt(3f) * (hex.R + hex.Q * 0.5f));
            }
            else
            {
                x = HexSize * (Mathf.Sqrt(3f) * (hex.Q + hex.R * 0.5f));
                z = HexSize * (1.5f * hex.R);
            }

            return new Vector3(x, 0f, z);
        }

        /// <summary>ワールド座標を含むヘックス。XZ 平面。</summary>
        /// <param name="world">ワールド座標。</param>
        public Hex WorldToHex(Vector3 world)
        {
            float q, r;

            if (FlatTop)
            {
                q = 2f / 3f * world.x / HexSize;
                r = (-1f / 3f * world.x + Mathf.Sqrt(3f) / 3f * world.z) / HexSize;
            }
            else
            {
                q = (Mathf.Sqrt(3f) / 3f * world.x - 1f / 3f * world.z) / HexSize;
                r = 2f / 3f * world.z / HexSize;
            }

            return Round(q, r);
        }

        /// <summary>ヘックスの 6 頂点。メッシュ生成やギズモ描画に使う。</summary>
        /// <param name="hex">座標。</param>
        /// <param name="corners">6 要素以上の書き込み先。</param>
        public void Corners(Hex hex, Span<Vector3> corners)
        {
            if (corners.Length < 6) throw new ArgumentException("6 頂点ぶんの領域が必要。", nameof(corners));

            var center = HexToWorld(hex);
            for (var i = 0; i < 6; i++)
            {
                var angle = Mathf.Deg2Rad * (FlatTop ? 60f * i : 60f * i - 30f);
                corners[i] = center + new Vector3(HexSize * Mathf.Cos(angle), 0f, HexSize * Mathf.Sin(angle));
            }
        }

        /// <summary>
        /// 小数の軸座標を最も近いヘックスに丸める。
        /// 3 つの座標の整合が崩れないよう、立方体座標で丸めてから戻している。
        /// </summary>
        /// <param name="q">Q 成分。</param>
        /// <param name="r">R 成分。</param>
        public static Hex Round(float q, float r)
        {
            var s = -q - r;

            var roundedQ = Mathf.RoundToInt(q);
            var roundedR = Mathf.RoundToInt(r);
            var roundedS = Mathf.RoundToInt(s);

            var deltaQ = Mathf.Abs(roundedQ - q);
            var deltaR = Mathf.Abs(roundedR - r);
            var deltaS = Mathf.Abs(roundedS - s);

            // 最もずれた成分を捨て、残り 2 つから再計算する。
            if (deltaQ > deltaR && deltaQ > deltaS) roundedQ = -roundedR - roundedS;
            else if (deltaR > deltaS) roundedR = -roundedQ - roundedS;

            return new Hex(roundedQ, roundedR);
        }

        /// <summary>(座標, 値) の組を走査する構造体列挙子を返す。</summary>
        public Dictionary<Hex, T>.Enumerator GetEnumerator() => _cells.GetEnumerator();
    }
}
