using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Drawing.Tests
{
    /// <summary>
    /// 形を線分に変換する部分。画面を見なくても、何本の線がどこに出るかはここで確定できる。
    /// </summary>
    public sealed class DrawShapesTests
    {
        private readonly List<Segment> _segments = new List<Segment>();

        [SetUp]
        public void SetUp() => _segments.Clear();

        [Test]
        public void Box_HasTwelveEdgesAndSpansTheGivenSize()
        {
            DrawShapes.Box(_segments, Vector3.zero, Vector3.one * 2f, Quaternion.identity);

            Assert.AreEqual(12, _segments.Count, "直方体の辺は 12 本");

            var min = Vector3.positiveInfinity;
            var max = Vector3.negativeInfinity;

            foreach (var segment in _segments)
            {
                min = Vector3.Min(min, Vector3.Min(segment.A, segment.B));
                max = Vector3.Max(max, Vector3.Max(segment.A, segment.B));
            }

            Assert.That(min.x, Is.EqualTo(-1f).Within(1e-4f));
            Assert.That(max.y, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Box_FollowsTheRotation()
        {
            DrawShapes.Box(_segments, Vector3.zero, new Vector3(2f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));

            // x 方向に伸びていた辺が z 方向へ回る。
            var max = Vector3.negativeInfinity;
            foreach (var segment in _segments) max = Vector3.Max(max, Vector3.Max(segment.A, segment.B));

            Assert.That(max.z, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(max.x, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void Box_DoesNotAllocateAfterTheDestinationListIsPrepared()
        {
            var prepared = new List<Segment>(12);
            DrawShapes.Box(prepared, Vector3.zero, Vector3.one, Quaternion.identity);
            prepared.Clear();

            var before = GC.GetAllocatedBytesForCurrentThread();
            DrawShapes.Box(prepared, Vector3.zero, Vector3.one, Quaternion.identity);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, allocated, "毎フレーム呼ぶ箱の計算で一時配列を作らない");
        }

        [Test]
        public void Circle_ProducesOneSegmentPerDivisionAndCloses()
        {
            DrawShapes.Circle(_segments, Vector3.zero, 1f, Vector3.up, segments: 8);

            Assert.AreEqual(8, _segments.Count);
            Assert.That(Vector3.Distance(_segments[0].A, _segments[_segments.Count - 1].B), Is.LessThan(1e-4f),
                "一周して始点へ戻る");

            foreach (var segment in _segments)
            {
                Assert.That(segment.A.magnitude, Is.EqualTo(1f).Within(1e-4f), "半径どおりの位置に並ぶ");
                Assert.That(segment.A.y, Is.EqualTo(0f).Within(1e-4f), "法線に垂直な面に乗る");
            }
        }

        [Test]
        public void Sphere_IsThreeCircles()
        {
            DrawShapes.Sphere(_segments, Vector3.zero, 2f, segments: 12);

            Assert.AreEqual(36, _segments.Count);

            foreach (var segment in _segments)
            {
                Assert.That(segment.A.magnitude, Is.EqualTo(2f).Within(1e-4f));
            }
        }

        [Test]
        public void Capsule_FallsBackToASphereWhenBothEndsCoincide()
        {
            // 軸の長さが 0 だと向きが決まらない。0 除算で NaN を撒くより、球として描くほうが役に立つ。
            DrawShapes.Capsule(_segments, Vector3.one, Vector3.one, 1f, segments: 8);

            Assert.AreEqual(3 * 16, _segments.Count);

            foreach (var segment in _segments)
            {
                Assert.IsFalse(float.IsNaN(segment.A.x), "NaN が混ざらない");
            }
        }

        [Test]
        public void Capsule_HasCapsSidesAndArcs()
        {
            DrawShapes.Capsule(_segments, Vector3.zero, Vector3.up * 2f, 0.5f, segments: 8);

            // 端の円 2 つ（8*2 本ずつ）＋ 側面 4 本 ＋ 半円 4 つ（8 本ずつ）。
            Assert.AreEqual(16 * 2 + 4 + 8 * 4, _segments.Count);
        }

        [Test]
        public void Arrow_IsOneShaftPlusTheHeadSpokes()
        {
            DrawShapes.Arrow(_segments, Vector3.zero, Vector3.forward * 3f, headSize: 0.5f, headSpokes: 4);

            Assert.AreEqual(5, _segments.Count);
            Assert.AreEqual(Vector3.zero, _segments[0].A);
            Assert.AreEqual(Vector3.forward * 3f, _segments[0].B);
        }

        [Test]
        public void Arrow_SkipsTheHeadWhenItWouldBeDegenerate()
        {
            DrawShapes.Arrow(_segments, Vector3.zero, Vector3.zero, headSize: 0.5f);
            Assert.AreEqual(1, _segments.Count, "始点と終点が同じなら向きが決まらないので傘を描かない");

            _segments.Clear();
            DrawShapes.Arrow(_segments, Vector3.zero, Vector3.forward, headSize: 0f);
            Assert.AreEqual(1, _segments.Count, "傘の大きさが 0 なら線だけ");
        }

        [Test]
        public void Path_ConnectsConsecutivePointsAndOptionallyCloses()
        {
            var points = new[] { Vector3.zero, Vector3.right, Vector3.right + Vector3.forward };

            DrawShapes.Path(_segments, points, closed: false);
            Assert.AreEqual(2, _segments.Count);

            _segments.Clear();
            DrawShapes.Path(_segments, points, closed: true);
            Assert.AreEqual(3, _segments.Count);
            Assert.AreEqual(points[0], _segments[2].B, "閉じると末尾から先頭へ戻る");
        }

        [Test]
        public void Path_IgnoresInputThatCannotFormALine()
        {
            DrawShapes.Path(_segments, null, closed: true);
            DrawShapes.Path(_segments, new[] { Vector3.zero }, closed: true);

            Assert.IsEmpty(_segments);
        }

        [Test]
        public void Cross_IsThreeAxisAlignedLines()
        {
            DrawShapes.Cross(_segments, Vector3.one, 2f);

            Assert.AreEqual(3, _segments.Count);
            Assert.That(Vector3.Distance(_segments[0].A, new Vector3(0f, 1f, 1f)), Is.LessThan(1e-4f));
        }

        [TestCase(0f, 1f, 0f)]
        [TestCase(0f, -1f, 0f)]
        [TestCase(1f, 0f, 0f)]
        [TestCase(0.3f, 0.4f, 0.5f)]
        public void Basis_IsOrthonormalForAnyNormalIncludingTheVerticalOnes(float x, float y, float z)
        {
            // 外積の相手に固定の軸を使うと、法線がそれと平行なときに長さ 0 になる。
            // 真上・真下でも成り立つことを見ておく。
            var normal = new Vector3(x, y, z).normalized;

            DrawShapes.Basis(normal, out var a, out var b);

            Assert.That(a.magnitude, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(b.magnitude, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(Vector3.Dot(a, normal), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(Vector3.Dot(b, normal), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(Vector3.Dot(a, b), Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void Basis_SurvivesAZeroLengthNormal()
        {
            DrawShapes.Basis(Vector3.zero, out var a, out var b);

            Assert.That(a.magnitude, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(b.magnitude, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Arc_StartsAndEndsWhereTheAnglesSay()
        {
            DrawShapes.Arc(_segments, Vector3.zero, Vector3.right, Vector3.forward, 1f, 0f, 90f, segments: 4);

            Assert.AreEqual(4, _segments.Count);
            Assert.That(Vector3.Distance(_segments[0].A, Vector3.right), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(_segments[3].B, Vector3.forward), Is.LessThan(1e-4f));
        }

        [Test]
        public void Arc_NeverProducesZeroSegments()
        {
            DrawShapes.Arc(_segments, Vector3.zero, Vector3.right, Vector3.up, 1f, 0f, 360f, segments: 0);

            Assert.AreEqual(1, _segments.Count, "分割数が 0 でも 1 本にはする");
        }
    }
}
