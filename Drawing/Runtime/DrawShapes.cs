using System.Collections.Generic;
using UnityEngine;

namespace Drawing
{
    /// <summary>線分 1 本。形はすべてこれの集まりとして表す。</summary>
    internal readonly struct Segment
    {
        public Segment(Vector3 a, Vector3 b)
        {
            A = a;
            B = b;
        }

        public readonly Vector3 A;
        public readonly Vector3 B;
    }

    /// <summary>
    /// 形を線分の並びに変換する。
    /// <para>
    /// 描画にも Unity のライフサイクルにも触れない純粋な計算だけを置いている。
    /// 「球が何本の線になるか」「太さ 0 の線をどう扱うか」といった話は、
    /// 実際に画面を見なくてもここだけで確かめられる。
    /// </para>
    /// </summary>
    internal static class DrawShapes
    {
        /// <summary>円や球の既定の分割数。粗すぎず、線が増えすぎない値。</summary>
        public const int DefaultSegments = 32;

        /// <summary>直方体。</summary>
        public static void Box(List<Segment> into, Vector3 center, Vector3 size, Quaternion rotation)
        {
            var half = size * 0.5f;

            // 毎フレーム呼ぶ用途なので、8 要素の配列を作らず各隅を値として持つ。
            var nnn = TransformCorner(center, half, rotation, -1f, -1f, -1f);
            var pnn = TransformCorner(center, half, rotation, 1f, -1f, -1f);
            var npn = TransformCorner(center, half, rotation, -1f, 1f, -1f);
            var ppn = TransformCorner(center, half, rotation, 1f, 1f, -1f);
            var nnp = TransformCorner(center, half, rotation, -1f, -1f, 1f);
            var pnp = TransformCorner(center, half, rotation, 1f, -1f, 1f);
            var npp = TransformCorner(center, half, rotation, -1f, 1f, 1f);
            var ppp = TransformCorner(center, half, rotation, 1f, 1f, 1f);

            AddEdge(into, nnn, pnn);
            AddEdge(into, pnn, ppn);
            AddEdge(into, ppn, npn);
            AddEdge(into, npn, nnn);

            AddEdge(into, nnp, pnp);
            AddEdge(into, pnp, ppp);
            AddEdge(into, ppp, npp);
            AddEdge(into, npp, nnp);

            AddEdge(into, nnn, nnp);
            AddEdge(into, pnn, pnp);
            AddEdge(into, npn, npp);
            AddEdge(into, ppn, ppp);
        }

        /// <summary>円。<paramref name="normal"/> に垂直な面に描く。</summary>
        public static void Circle(List<Segment> into, Vector3 center, float radius, Vector3 normal, int segments = DefaultSegments)
        {
            Basis(normal, out var a, out var b);
            Arc(into, center, a, b, radius, 0f, 360f, segments);
        }

        /// <summary>球。直交する 3 つの円で表す。中身を塗らないので、奥のものが隠れない。</summary>
        public static void Sphere(List<Segment> into, Vector3 center, float radius, int segments = DefaultSegments)
        {
            Circle(into, center, radius, Vector3.right, segments);
            Circle(into, center, radius, Vector3.up, segments);
            Circle(into, center, radius, Vector3.forward, segments);
        }

        /// <summary>
        /// カプセル。<see cref="CapsuleCollider"/> の形を確かめるためのもの。
        /// <paramref name="start"/> と <paramref name="end"/> は<b>半球の中心</b>で、両端の先端ではない。
        /// </summary>
        public static void Capsule(List<Segment> into, Vector3 start, Vector3 end, float radius, int segments = 16)
        {
            var axis = end - start;

            // 潰れたカプセルは球と同じ。0 除算を避けるためにも先に分けておく。
            if (axis.sqrMagnitude < 1e-8f)
            {
                Sphere(into, start, radius, segments * 2);
                return;
            }

            axis.Normalize();
            Basis(axis, out var a, out var b);

            Circle(into, start, radius, axis, segments * 2);
            Circle(into, end, radius, axis, segments * 2);

            // 側面。4 本あれば、どの向きから見ても輪郭が読める。
            into.Add(new Segment(start + a * radius, end + a * radius));
            into.Add(new Segment(start - a * radius, end - a * radius));
            into.Add(new Segment(start + b * radius, end + b * radius));
            into.Add(new Segment(start - b * radius, end - b * radius));

            // 半球。軸を含む 2 平面それぞれに半円を描く。
            Arc(into, start, a, -axis, radius, 0f, 180f, segments);
            Arc(into, start, b, -axis, radius, 0f, 180f, segments);
            Arc(into, end, a, axis, radius, 0f, 180f, segments);
            Arc(into, end, b, axis, radius, 0f, 180f, segments);
        }

        /// <summary>矢印。線 1 本と、先端の傘。</summary>
        public static void Arrow(List<Segment> into, Vector3 from, Vector3 to, float headSize, int headSpokes = 4)
        {
            into.Add(new Segment(from, to));

            var direction = to - from;
            if (direction.sqrMagnitude < 1e-8f || headSize <= 0f || headSpokes <= 0) return;

            direction.Normalize();
            Basis(direction, out var a, out var b);

            var back = to - direction * headSize;
            var spread = headSize * 0.5f;

            for (var i = 0; i < headSpokes; i++)
            {
                var angle = 360f / headSpokes * i * Mathf.Deg2Rad;
                var side = a * Mathf.Cos(angle) + b * Mathf.Sin(angle);

                into.Add(new Segment(to, back + side * spread));
            }
        }

        /// <summary>点の位置を示す小さな十字。</summary>
        public static void Cross(List<Segment> into, Vector3 center, float size)
        {
            var half = size * 0.5f;

            into.Add(new Segment(center - Vector3.right * half, center + Vector3.right * half));
            into.Add(new Segment(center - Vector3.up * half, center + Vector3.up * half));
            into.Add(new Segment(center - Vector3.forward * half, center + Vector3.forward * half));
        }

        /// <summary>折れ線。<paramref name="closed"/> なら末尾と先頭もつなぐ。</summary>
        public static void Path(List<Segment> into, IReadOnlyList<Vector3> points, bool closed)
        {
            if (points == null || points.Count < 2) return;

            for (var i = 0; i < points.Count - 1; i++)
            {
                into.Add(new Segment(points[i], points[i + 1]));
            }

            if (closed) into.Add(new Segment(points[points.Count - 1], points[0]));
        }

        /// <summary>
        /// 円弧。<paramref name="axisA"/> が角度 0 の向き、<paramref name="axisB"/> が 90 度の向き。
        /// </summary>
        public static void Arc(
            List<Segment> into,
            Vector3 center,
            Vector3 axisA,
            Vector3 axisB,
            float radius,
            float startDegrees,
            float sweepDegrees,
            int segments)
        {
            if (segments < 1) segments = 1;

            var previous = center + (axisA * Mathf.Cos(startDegrees * Mathf.Deg2Rad)
                + axisB * Mathf.Sin(startDegrees * Mathf.Deg2Rad)) * radius;

            for (var i = 1; i <= segments; i++)
            {
                var degrees = startDegrees + sweepDegrees * i / segments;
                var radians = degrees * Mathf.Deg2Rad;
                var current = center + (axisA * Mathf.Cos(radians) + axisB * Mathf.Sin(radians)) * radius;

                into.Add(new Segment(previous, current));
                previous = current;
            }
        }

        /// <summary>
        /// <paramref name="normal"/> に垂直な単位ベクトルを 2 本作る。
        /// <para>
        /// 外積の相手に固定のベクトルを使うと、法線がそれと平行なときに長さ 0 になって向きが定まらない。
        /// 平行に近い場合だけ別の軸に切り替えている。
        /// </para>
        /// </summary>
        public static void Basis(Vector3 normal, out Vector3 a, out Vector3 b)
        {
            normal = normal.sqrMagnitude < 1e-8f ? Vector3.up : normal.normalized;

            var reference = Mathf.Abs(normal.y) > 0.99f ? Vector3.right : Vector3.up;

            a = Vector3.Cross(normal, reference).normalized;
            b = Vector3.Cross(normal, a);
        }

        /// <summary>中心から見た符号を指定して、回転済みの隅を求める。</summary>
        private static Vector3 TransformCorner(Vector3 center, Vector3 half, Quaternion rotation, float x, float y, float z)
        {
            return center + rotation * new Vector3(half.x * x, half.y * y, half.z * z);
        }

        /// <summary>2 点を辺として追加する。</summary>
        private static void AddEdge(List<Segment> into, Vector3 from, Vector3 to) => into.Add(new Segment(from, to));
    }
}
