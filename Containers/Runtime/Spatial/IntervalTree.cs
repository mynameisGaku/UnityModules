using System;
using System.Collections.Generic;

namespace Containers.Spatial
{
    /// <summary>
    /// 数値の区間に値を対応づけ、「この点を覆っているものは何か」「この範囲と重なるものは何か」に
    /// 全件走査せず答える。
    /// <para>
    /// タイムライン上のイベント、攻撃の判定受付、スコア帯ごとの難易度、バフの持続、字幕のキュー。
    /// 中心分割型の区間木で、各ノードは自分の中点をまたぐ区間を持つ。問い合わせは 1 本の経路を
    /// 降りながら、可能性のある区間だけを確認する。
    /// </para>
    /// <para>構築は一括方式 —— <see cref="Add"/> したあと <see cref="Build"/>、それから問い合わせる。</para>
    /// </summary>
    public sealed class IntervalTree<TValue>
    {
        /// <summary>1 つの区間とそれに紐づく値。</summary>
        public readonly struct Interval
        {
            /// <summary>区間の始まり。</summary>
            public readonly float Start;

            /// <summary>区間の終わり。</summary>
            public readonly float End;

            /// <summary>紐づく値。</summary>
            public readonly TValue Value;

            /// <summary>区間と値を指定して作る。</summary>
            /// <param name="start">始まり。</param>
            /// <param name="end">終わり。</param>
            /// <param name="value">値。</param>
            public Interval(float start, float end, TValue value)
            {
                Start = start;
                End = end;
                Value = value;
            }

            /// <summary>点を含むか。両端を含む。</summary>
            /// <param name="point">判定する点。</param>
            public bool Contains(float point) => point >= Start && point <= End;

            /// <summary>範囲と重なるか。</summary>
            /// <param name="start">範囲の始まり。</param>
            /// <param name="end">範囲の終わり。</param>
            public bool Overlaps(float start, float end) => Start <= end && End >= start;
        }

        private sealed class Node
        {
            public float Center;
            public Node Left;
            public Node Right;

            /// <summary><see cref="Center"/> をまたぐ区間。始まりの昇順。</summary>
            public Interval[] ByStart;

            /// <summary>同じ区間を、終わりの降順で並べたもの。</summary>
            public Interval[] ByEnd;
        }

        private readonly List<Interval> _pending = new List<Interval>();
        private Node _root;

        /// <summary>登録されている区間の数。</summary>
        public int Count => _pending.Count;

        /// <summary>検索構造が最新かどうか。</summary>
        public bool IsBuilt { get; private set; }

        /// <summary>区間を登録する。問い合わせの前に <see cref="Build"/> が必要。</summary>
        /// <param name="start">始まり。</param>
        /// <param name="end">終わり。逆でも自動的に入れ替える。</param>
        /// <param name="value">紐づける値。</param>
        public void Add(float start, float end, TValue value)
        {
            if (end < start) (start, end) = (end, start);

            _pending.Add(new Interval(start, end, value));
            IsBuilt = false;
        }

        /// <summary>検索構造を作る。追加のたびに呼び直しても構わない。</summary>
        public void Build()
        {
            _root = BuildNode(_pending.ToArray());
            IsBuilt = true;
        }

        /// <summary>全て捨てる。</summary>
        public void Clear()
        {
            _pending.Clear();
            _root = null;
            IsBuilt = false;
        }

        /// <summary>点を含む区間の値を全て集める。</summary>
        /// <param name="point">判定する点。</param>
        /// <param name="results">追記先。</param>
        public void Query(float point, FastList<TValue> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            EnsureBuilt();

            QueryPoint(_root, point, results);
        }

        /// <summary><c>[start, end]</c> と重なる区間の値を全て集める。</summary>
        /// <param name="start">範囲の始まり。</param>
        /// <param name="end">範囲の終わり。</param>
        /// <param name="results">追記先。</param>
        public void QueryRange(float start, float end, FastList<TValue> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (end < start) (start, end) = (end, start);
            EnsureBuilt();

            QueryOverlap(_root, start, end, results);
        }

        private void EnsureBuilt()
        {
            if (!IsBuilt) Build();
        }

        private static Node BuildNode(Interval[] intervals)
        {
            if (intervals == null || intervals.Length == 0) return null;

            // 全端点の中央値で分割し、左右がおおむね釣り合うようにする。
            var points = new float[intervals.Length * 2];
            for (var i = 0; i < intervals.Length; i++)
            {
                points[i * 2] = intervals[i].Start;
                points[i * 2 + 1] = intervals[i].End;
            }

            Array.Sort(points);
            var center = points[points.Length / 2];

            var left = new List<Interval>();
            var right = new List<Interval>();
            var crossing = new List<Interval>();

            for (var i = 0; i < intervals.Length; i++)
            {
                var interval = intervals[i];
                if (interval.End < center) left.Add(interval);
                else if (interval.Start > center) right.Add(interval);
                else crossing.Add(interval);
            }

            // 分割が退化すると無限に再帰するので、その場合は全部このノードに置く。
            if (crossing.Count == 0)
            {
                crossing.AddRange(left);
                crossing.AddRange(right);
                left.Clear();
                right.Clear();
            }

            var byStart = crossing.ToArray();
            var byEnd = crossing.ToArray();
            Array.Sort(byStart, (a, b) => a.Start.CompareTo(b.Start));
            Array.Sort(byEnd, (a, b) => b.End.CompareTo(a.End));

            return new Node
            {
                Center = center,
                ByStart = byStart,
                ByEnd = byEnd,
                Left = BuildNode(left.ToArray()),
                Right = BuildNode(right.ToArray())
            };
        }

        private static void QueryPoint(Node node, float point, FastList<TValue> results)
        {
            while (node != null)
            {
                if (point < node.Center)
                {
                    // 点より手前で始まる区間しか、その点を含み得ない。
                    var byStart = node.ByStart;
                    for (var i = 0; i < byStart.Length && byStart[i].Start <= point; i++)
                    {
                        if (byStart[i].Contains(point)) results.Add(byStart[i].Value);
                    }

                    node = node.Left;
                }
                else
                {
                    var byEnd = node.ByEnd;
                    for (var i = 0; i < byEnd.Length && byEnd[i].End >= point; i++)
                    {
                        if (byEnd[i].Contains(point)) results.Add(byEnd[i].Value);
                    }

                    node = node.Right;
                }
            }
        }

        private static void QueryOverlap(Node node, float start, float end, FastList<TValue> results)
        {
            if (node == null) return;

            var byStart = node.ByStart;
            for (var i = 0; i < byStart.Length && byStart[i].Start <= end; i++)
            {
                if (byStart[i].Overlaps(start, end)) results.Add(byStart[i].Value);
            }

            if (start < node.Center) QueryOverlap(node.Left, start, end, results);
            if (end > node.Center) QueryOverlap(node.Right, start, end, results);
        }
    }
}
