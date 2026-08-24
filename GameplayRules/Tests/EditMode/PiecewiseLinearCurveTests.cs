using System;
using System.Linq;
using NUnit.Framework;

namespace GameplayMath.Tests
{
    [TestFixture]
    public sealed class PiecewiseLinearCurveTests
    {
        [Test]
        public void Constructor_CreatesEmptyCurve()
        {
            Assert.That(new PiecewiseLinearCurve().PointCount, Is.Zero);
        }

        [Test]
        public void Add_StoresPointAndReportsCounts()
        {
            var curve = new PiecewiseLinearCurve();
            var result = curve.Add(10d, 100d);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Changed, Is.True);
            Assert.That(result.Error, Is.EqualTo(CurveError.None));
            Assert.That(result.AffectedX, Is.EqualTo(10d));
            Assert.That(result.PreviousY, Is.Zero);
            Assert.That(result.CurrentY, Is.EqualTo(100d));
            Assert.That(result.PreviousPointCount, Is.Zero);
            Assert.That(result.CurrentPointCount, Is.EqualTo(1));
        }

        [Test]
        public void Add_MaintainsXOrder()
        {
            var curve = CreateCurve(20d, 0d, 10d);

            AssertPoint(curve, 0, 0d, 0d);
            AssertPoint(curve, 1, 10d, 100d);
            AssertPoint(curve, 2, 20d, 50d);
        }

        [Test]
        public void Add_InsertionOrderProducesSameEvaluation()
        {
            var ascending = CreateCurve(0d, 10d, 20d);
            var descending = CreateCurve(20d, 10d, 0d);

            foreach (var query in new[] { -5d, 0d, 2.5d, 5d, 10d, 15d, 20d, 30d })
                Assert.That(descending.Evaluate(query), Is.EqualTo(ascending.Evaluate(query)));
        }

        [Test]
        public void Add_DuplicateXIsRejectedWithoutMutation()
        {
            var curve = new PiecewiseLinearCurve();
            curve.Add(1d, 2d);

            var result = curve.Add(1d, 99d);

            AssertFailure(result, CurveError.DuplicateX, 1);
            AssertPoint(curve, 0, 1d, 2d);
        }

        [Test]
        public void Add_PositiveAndNegativeZeroAreDuplicateX()
        {
            var curve = new PiecewiseLinearCurve();
            curve.Add(+0d, 1d);

            var result = curve.Add(-0d, 2d);

            AssertFailure(result, CurveError.DuplicateX, 1);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void Add_InvalidXIsRejected(double x)
        {
            AssertFailure(new PiecewiseLinearCurve().Add(x, 1d), CurveError.InvalidX, 0);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void Add_InvalidYIsRejected(double y)
        {
            AssertFailure(new PiecewiseLinearCurve().Add(1d, y), CurveError.InvalidY, 0);
        }

        [Test]
        public void Add_CapacityIsFixedAtThirtyTwo()
        {
            var curve = new PiecewiseLinearCurve();
            for (var index = 0; index < PiecewiseLinearCurve.MaximumPointCount; index++) Assert.That(curve.Add(index, index).Succeeded, Is.True);

            AssertFailure(curve.Add(100d, 100d), CurveError.CapacityReached, 32);
        }

        [Test]
        public void Update_ChangesOnlyY()
        {
            var curve = CreateCurve(0d, 10d, 20d);

            var result = curve.Update(10d, 80d);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Changed, Is.True);
            Assert.That(result.PreviousY, Is.EqualTo(100d));
            Assert.That(result.CurrentY, Is.EqualTo(80d));
            AssertPoint(curve, 1, 10d, 80d);
        }

        [Test]
        public void Update_SameYIsSuccessfulNoChange()
        {
            var curve = CreateCurve(0d, 10d, 20d);

            var result = curve.Update(10d, 100d);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Changed, Is.False);
            Assert.That(result.PreviousPointCount, Is.EqualTo(3));
            Assert.That(result.CurrentPointCount, Is.EqualTo(3));
        }

        [Test]
        public void Update_MissingOrInvalidPointDoesNotMutate()
        {
            var curve = CreateCurve(0d, 10d, 20d);

            AssertFailure(curve.Update(15d, 1d), CurveError.PointNotFound, 3);
            AssertFailure(curve.Update(double.NaN, 1d), CurveError.InvalidX, 3);
            AssertFailure(curve.Update(10d, double.NaN), CurveError.InvalidY, 3);
            AssertPoint(curve, 1, 10d, 100d);
        }

        [Test]
        public void Remove_CompactsSortedPoints()
        {
            var curve = CreateCurve(0d, 10d, 20d);

            var result = curve.Remove(10d);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.PreviousY, Is.EqualTo(100d));
            Assert.That(result.CurrentY, Is.Zero);
            Assert.That(result.PreviousPointCount, Is.EqualTo(3));
            Assert.That(result.CurrentPointCount, Is.EqualTo(2));
            AssertPoint(curve, 1, 20d, 50d);
        }

        [Test]
        public void Remove_MissingOrInvalidPointDoesNotMutate()
        {
            var curve = CreateCurve(0d, 10d, 20d);

            AssertFailure(curve.Remove(15d), CurveError.PointNotFound, 3);
            AssertFailure(curve.Remove(double.PositiveInfinity), CurveError.InvalidX, 3);
        }

        [Test]
        public void Clear_RemovesAllAndSecondClearIsNoChange()
        {
            var curve = CreateCurve(0d, 10d, 20d);

            var first = curve.Clear();
            var second = curve.Clear();

            Assert.That(first.Succeeded, Is.True);
            Assert.That(first.Changed, Is.True);
            Assert.That(first.PreviousPointCount, Is.EqualTo(3));
            Assert.That(first.CurrentPointCount, Is.Zero);
            Assert.That(second.Succeeded, Is.True);
            Assert.That(second.Changed, Is.False);
        }

        [Test]
        public void TryGetPointAt_ReportsBounds()
        {
            var curve = CreateCurve(0d, 10d, 20d);

            Assert.That(curve.TryGetPointAt(1, out var point, out var success), Is.True);
            Assert.That(point.X, Is.EqualTo(10d));
            Assert.That(point.Y, Is.EqualTo(100d));
            Assert.That(success, Is.EqualTo(CurveError.None));
            Assert.That(curve.TryGetPointAt(-1, out _, out var below), Is.False);
            Assert.That(below, Is.EqualTo(CurveError.IndexOutOfRange));
            Assert.That(curve.TryGetPointAt(3, out _, out var above), Is.False);
            Assert.That(above, Is.EqualTo(CurveError.IndexOutOfRange));
        }

        [Test]
        public void TryGetPoint_ReportsInvalidAndMissingX()
        {
            var curve = CreateCurve(0d, 10d, 20d);

            Assert.That(curve.TryGetPoint(20d, out var point, out var success), Is.True);
            Assert.That(point.Y, Is.EqualTo(50d));
            Assert.That(success, Is.EqualTo(CurveError.None));
            Assert.That(curve.TryGetPoint(15d, out _, out var missing), Is.False);
            Assert.That(missing, Is.EqualTo(CurveError.PointNotFound));
            Assert.That(curve.TryGetPoint(double.NaN, out _, out var invalid), Is.False);
            Assert.That(invalid, Is.EqualTo(CurveError.InvalidX));
        }

        [Test]
        public void Evaluate_EmptyCurveReturnsExplicitError()
        {
            var result = new PiecewiseLinearCurve().Evaluate(5d);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(CurveError.EmptyCurve));
            Assert.That(result.Query, Is.EqualTo(5d));
            Assert.That(result.LowerIndex, Is.EqualTo(-1));
            Assert.That(result.UpperIndex, Is.EqualTo(-1));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void Evaluate_InvalidQueryReturnsExplicitError(double query)
        {
            var result = CreateCurve(0d, 10d, 20d).Evaluate(query);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(CurveError.InvalidQuery));
            Assert.That(result.Query, Is.Zero);
        }

        [Test]
        public void Evaluate_ExactPointReturnsSameBounds()
        {
            var result = CreateCurve(0d, 10d, 20d).Evaluate(10d);

            AssertEvaluation(result, 10d, 100d, 1, 1, 10d, 100d, 10d, 100d, 0d, false);
        }

        [Test]
        public void Evaluate_InterpolatesFirstSegment()
        {
            var result = CreateCurve(0d, 10d, 20d).Evaluate(5d);

            AssertEvaluation(result, 5d, 50d, 0, 1, 0d, 0d, 10d, 100d, 0.5d, false);
        }

        [Test]
        public void Evaluate_InterpolatesSecondSegment()
        {
            var result = CreateCurve(0d, 10d, 20d).Evaluate(15d);

            AssertEvaluation(result, 15d, 75d, 1, 2, 10d, 100d, 20d, 50d, 0.5d, false);
        }

        [Test]
        public void Evaluate_BelowRangeClampsFirstPoint()
        {
            var result = CreateCurve(0d, 10d, 20d).Evaluate(-5d);

            AssertEvaluation(result, -5d, 0d, 0, 0, 0d, 0d, 0d, 0d, 0d, true);
        }

        [Test]
        public void Evaluate_AboveRangeClampsLastPoint()
        {
            var result = CreateCurve(0d, 10d, 20d).Evaluate(30d);

            AssertEvaluation(result, 30d, 50d, 2, 2, 20d, 50d, 20d, 50d, 0d, true);
        }

        [Test]
        public void Evaluate_SinglePointClampsOnlyOutsideExactX()
        {
            var curve = new PiecewiseLinearCurve();
            curve.Add(4d, 9d);

            Assert.That(curve.Evaluate(4d).Clamped, Is.False);
            Assert.That(curve.Evaluate(-1d).Clamped, Is.True);
            Assert.That(curve.Evaluate(10d).Clamped, Is.True);
            Assert.That(curve.Evaluate(10d).Value, Is.EqualTo(9d));
        }

        [Test]
        public void Evaluate_OppositeExtremeXUsesScaledRatio()
        {
            var curve = new PiecewiseLinearCurve();
            curve.Add(-double.MaxValue, 0d);
            curve.Add(double.MaxValue, 100d);

            var result = curve.Evaluate(0d);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Interpolation, Is.EqualTo(0.5d));
            Assert.That(result.Value, Is.EqualTo(50d));
        }

        [Test]
        public void Evaluate_OppositeExtremeYUsesStableConvexCombination()
        {
            var curve = new PiecewiseLinearCurve();
            curve.Add(0d, -double.MaxValue);
            curve.Add(2d, double.MaxValue);

            var result = curve.Evaluate(1d);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Value, Is.Zero);
        }

        [Test]
        public void Update_ChangesFutureInterpolation()
        {
            var curve = CreateCurve(0d, 10d, 20d);
            Assert.That(curve.Evaluate(15d).Value, Is.EqualTo(75d));

            curve.Update(20d, 0d);

            Assert.That(curve.Evaluate(15d).Value, Is.EqualTo(50d));
        }

        [Test]
        public void Remove_ReconnectsAdjacentSegment()
        {
            var curve = CreateCurve(0d, 10d, 20d);
            curve.Remove(10d);

            var result = curve.Evaluate(10d);

            Assert.That(result.Value, Is.EqualTo(25d));
            Assert.That(result.LowerIndex, Is.Zero);
            Assert.That(result.UpperIndex, Is.EqualTo(1));
        }

        [Test]
        public void Evaluate_DoesNotMutateCurve()
        {
            var curve = CreateCurve(0d, 10d, 20d);

            var first = curve.Evaluate(5d);
            var second = curve.Evaluate(5d);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(curve.PointCount, Is.EqualTo(3));
        }

        [Test]
        public void PublicRuntimeSurface_ContainsOnlyFiveTypes()
        {
            var exported = typeof(PiecewiseLinearCurve).Assembly.GetExportedTypes().OrderBy(type => type.FullName).Select(type => type.FullName).ToArray();

            Assert.That(exported, Is.EqualTo(new[]
            {
                "GameplayMath.CurveChangeResult",
                "GameplayMath.CurveError",
                "GameplayMath.CurveEvaluationResult",
                "GameplayMath.CurvePoint",
                "GameplayMath.PiecewiseLinearCurve"
            }));
        }

        [Test]
        public void ValueResults_ImplementStableEquality()
        {
            var firstCurve = CreateCurve(0d, 10d, 20d);
            var secondCurve = CreateCurve(0d, 10d, 20d);
            var firstEvaluation = firstCurve.Evaluate(5d);
            var secondEvaluation = secondCurve.Evaluate(5d);
            var firstChange = firstCurve.Update(10d, 80d);
            var secondChange = secondCurve.Update(10d, 80d);
            firstCurve.TryGetPoint(0d, out var firstPoint, out _);
            secondCurve.TryGetPoint(0d, out var secondPoint, out _);

            Assert.That(firstEvaluation, Is.EqualTo(secondEvaluation));
            Assert.That(firstEvaluation.GetHashCode(), Is.EqualTo(secondEvaluation.GetHashCode()));
            Assert.That(firstChange, Is.EqualTo(secondChange));
            Assert.That(firstPoint, Is.EqualTo(secondPoint));
        }

        private static PiecewiseLinearCurve CreateCurve(params double[] order)
        {
            var curve = new PiecewiseLinearCurve();
            foreach (var x in order)
            {
                var y = x == 0d ? 0d : x == 10d ? 100d : 50d;
                Assert.That(curve.Add(x, y).Succeeded, Is.True);
            }
            return curve;
        }

        private static void AssertPoint(PiecewiseLinearCurve curve, int index, double x, double y)
        {
            Assert.That(curve.TryGetPointAt(index, out var point, out var error), Is.True);
            Assert.That(error, Is.EqualTo(CurveError.None));
            Assert.That(point.X, Is.EqualTo(x));
            Assert.That(point.Y, Is.EqualTo(y));
        }

        private static void AssertFailure(CurveChangeResult result, CurveError error, int pointCount)
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Changed, Is.False);
            Assert.That(result.Error, Is.EqualTo(error));
            Assert.That(result.PreviousPointCount, Is.EqualTo(pointCount));
            Assert.That(result.CurrentPointCount, Is.EqualTo(pointCount));
        }

        private static void AssertEvaluation(CurveEvaluationResult result, double query, double value, int lowerIndex, int upperIndex, double lowerX, double lowerY, double upperX, double upperY, double interpolation, bool clamped)
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Error, Is.EqualTo(CurveError.None));
            Assert.That(result.Query, Is.EqualTo(query));
            Assert.That(result.Value, Is.EqualTo(value).Within(1e-12d));
            Assert.That(result.LowerIndex, Is.EqualTo(lowerIndex));
            Assert.That(result.UpperIndex, Is.EqualTo(upperIndex));
            Assert.That(result.LowerPoint.X, Is.EqualTo(lowerX));
            Assert.That(result.LowerPoint.Y, Is.EqualTo(lowerY));
            Assert.That(result.UpperPoint.X, Is.EqualTo(upperX));
            Assert.That(result.UpperPoint.Y, Is.EqualTo(upperY));
            Assert.That(result.Interpolation, Is.EqualTo(interpolation).Within(1e-12d));
            Assert.That(result.Clamped, Is.EqualTo(clamped));
        }
    }
}
