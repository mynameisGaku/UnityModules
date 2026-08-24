using System;
using System.Linq;
using NUnit.Framework;

namespace GameplayRules.Tests
{
    [TestFixture]
    public sealed class NumericRequirementEvaluatorTests
    {
        [Test]
        public void TryEvaluate_Null_ReturnsExplicitFailure()
        {
            AssertFailure(null, NumericRequirementError.NullRequirements);
        }

        [TestCase(0)]
        [TestCase(NumericRequirementEvaluator.MaximumRequirementCount + 1)]
        public void TryEvaluate_InvalidCount_ReturnsExplicitFailure(int count)
        {
            var requirements = Enumerable.Range(1, count).Select(id => Requirement(id, id, 0d, NumericRequirementComparison.AtLeast)).ToArray();
            AssertFailure(requirements, NumericRequirementError.InvalidRequirementCount);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void TryEvaluate_InvalidIdentifier_ReturnsExplicitFailure(int identifier)
        {
            AssertFailure(new[] { Requirement(identifier, 1d, 1d, NumericRequirementComparison.AtLeast) }, NumericRequirementError.InvalidIdentifier);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void TryEvaluate_NonFiniteActual_ReturnsExplicitFailure(double value)
        {
            AssertFailure(new[] { Requirement(1, value, 1d, NumericRequirementComparison.AtLeast) }, NumericRequirementError.NonFiniteValue);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void TryEvaluate_NonFiniteExpected_ReturnsExplicitFailure(double value)
        {
            AssertFailure(new[] { Requirement(1, 1d, value, NumericRequirementComparison.AtLeast) }, NumericRequirementError.NonFiniteValue);
        }

        [Test]
        public void TryEvaluate_UndefinedComparison_ReturnsExplicitFailure()
        {
            AssertFailure(new[] { Requirement(1, 1d, 1d, (NumericRequirementComparison)99) }, NumericRequirementError.InvalidComparison);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(-0.001d)]
        public void TryEvaluate_InvalidTolerance_ReturnsExplicitFailure(double tolerance)
        {
            AssertFailure(new[] { Requirement(1, 1d, 1d, NumericRequirementComparison.EqualWithinTolerance, tolerance) }, NumericRequirementError.InvalidTolerance);
        }

        [Test]
        public void TryEvaluate_OrderedComparisonRejectsUnusedTolerance()
        {
            AssertFailure(new[] { Requirement(1, 1d, 1d, NumericRequirementComparison.AtLeast, 0.1d) }, NumericRequirementError.InvalidTolerance);
        }

        [Test]
        public void TryEvaluate_DuplicateIdentifier_ReturnsExplicitFailure()
        {
            AssertFailure(new[]
            {
                Requirement(1, 5d, 3d, NumericRequirementComparison.AtLeast),
                Requirement(1, 2d, 4d, NumericRequirementComparison.AtMost)
            }, NumericRequirementError.DuplicateIdentifier);
        }

        [TestCase(5d, 3d, true)]
        [TestCase(3d, 3d, true)]
        [TestCase(2d, 3d, false)]
        public void AtLeast_UsesInclusiveBoundary(double actual, double expected, bool satisfied)
        {
            AssertSingle(actual, expected, NumericRequirementComparison.AtLeast, 0d, satisfied);
        }

        [TestCase(2d, 3d, true)]
        [TestCase(3d, 3d, true)]
        [TestCase(5d, 3d, false)]
        public void AtMost_UsesInclusiveBoundary(double actual, double expected, bool satisfied)
        {
            AssertSingle(actual, expected, NumericRequirementComparison.AtMost, 0d, satisfied);
        }

        [TestCase(4d, 3d, true)]
        [TestCase(3d, 3d, false)]
        [TestCase(2d, 3d, false)]
        public void GreaterThan_UsesStrictBoundary(double actual, double expected, bool satisfied)
        {
            AssertSingle(actual, expected, NumericRequirementComparison.GreaterThan, 0d, satisfied);
        }

        [TestCase(2d, 3d, true)]
        [TestCase(3d, 3d, false)]
        [TestCase(4d, 3d, false)]
        public void LessThan_UsesStrictBoundary(double actual, double expected, bool satisfied)
        {
            AssertSingle(actual, expected, NumericRequirementComparison.LessThan, 0d, satisfied);
        }

        [TestCase(10.1d, 10d, 0.1d, true)]
        [TestCase(9.9d, 10d, 0.1d, true)]
        [TestCase(10.1001d, 10d, 0.1d, false)]
        public void EqualWithinTolerance_UsesInclusiveAbsoluteBoundary(double actual, double expected, double tolerance, bool satisfied)
        {
            AssertSingle(actual, expected, NumericRequirementComparison.EqualWithinTolerance, tolerance, satisfied);
        }

        [TestCase(10.1d, 10d, 0.1d, false)]
        [TestCase(9.9d, 10d, 0.1d, false)]
        [TestCase(10.1001d, 10d, 0.1d, true)]
        public void OutsideTolerance_UsesStrictAbsoluteBoundary(double actual, double expected, double tolerance, bool satisfied)
        {
            AssertSingle(actual, expected, NumericRequirementComparison.OutsideTolerance, tolerance, satisfied);
        }

        [Test]
        public void TryEvaluate_AllSatisfied_ReturnsTrueAndAllLines()
        {
            var evaluation = Evaluate(new[]
            {
                Requirement(10, 5d, 3d, NumericRequirementComparison.AtLeast),
                Requirement(20, 2d, 4d, NumericRequirementComparison.AtMost),
                Requirement(30, 1.005d, 1d, NumericRequirementComparison.EqualWithinTolerance, 0.01d)
            });
            Assert.That(evaluation.AllSatisfied, Is.True);
            Assert.That(evaluation.LineCount, Is.EqualTo(3));
        }

        [Test]
        public void TryEvaluate_MixedResult_ReturnsEveryCondition()
        {
            var evaluation = Evaluate(new[]
            {
                Requirement(10, 5d, 3d, NumericRequirementComparison.AtLeast),
                Requirement(20, 5d, 4d, NumericRequirementComparison.AtMost),
                Requirement(30, 1d, 1d, NumericRequirementComparison.GreaterThan)
            });
            Assert.That(evaluation.AllSatisfied, Is.False);
            AssertLine(evaluation, 0, 10, true, 2d, 2d);
            AssertLine(evaluation, 1, 20, false, 1d, 1d);
            AssertLine(evaluation, 2, 30, false, 0d, 0d);
        }

        [Test]
        public void TryEvaluate_PreservesInputOrder()
        {
            var evaluation = Evaluate(new[]
            {
                Requirement(30, 3d, 0d, NumericRequirementComparison.AtLeast),
                Requirement(10, 1d, 0d, NumericRequirementComparison.AtLeast),
                Requirement(20, 2d, 0d, NumericRequirementComparison.AtLeast)
            });
            AssertLine(evaluation, 0, 30, true, 3d, 3d);
            AssertLine(evaluation, 1, 10, true, 1d, 1d);
            AssertLine(evaluation, 2, 20, true, 2d, 2d);
        }

        [Test]
        public void TryEvaluate_MaximumCount_IsAccepted()
        {
            var requirements = Enumerable.Range(1, NumericRequirementEvaluator.MaximumRequirementCount)
                .Select(id => Requirement(id, id, id, NumericRequirementComparison.AtLeast))
                .ToArray();
            var evaluation = Evaluate(requirements);
            Assert.That(evaluation.AllSatisfied, Is.True);
            Assert.That(evaluation.LineCount, Is.EqualTo(NumericRequirementEvaluator.MaximumRequirementCount));
        }

        [Test]
        public void TryEvaluate_ExtremeOpposites_ReturnResultOutOfRange()
        {
            AssertFailure(new[] { Requirement(1, double.MaxValue, -double.MaxValue, NumericRequirementComparison.GreaterThan) }, NumericRequirementError.ResultOutOfRange);
        }

        [Test]
        public void TryEvaluate_EqualMaximumValues_RemainFinite()
        {
            var evaluation = Evaluate(new[] { Requirement(1, double.MaxValue, double.MaxValue, NumericRequirementComparison.EqualWithinTolerance, 0d) });
            AssertLine(evaluation, 0, 1, true, 0d, 0d);
        }

        [Test]
        public void TryEvaluate_DoesNotMutateInput()
        {
            var requirements = new[] { Requirement(2, 4d, 3d, NumericRequirementComparison.AtLeast), Requirement(1, 2d, 3d, NumericRequirementComparison.AtMost) };
            var before = requirements.ToArray();
            Evaluate(requirements);
            Assert.That(requirements, Is.EqualTo(before));
        }

        [Test]
        public void Evaluation_RemainsImmutableAfterInputMutation()
        {
            var requirements = new[] { Requirement(1, 4d, 3d, NumericRequirementComparison.AtLeast) };
            var evaluation = Evaluate(requirements);
            requirements[0] = Requirement(1, 0d, 99d, NumericRequirementComparison.AtLeast);
            AssertLine(evaluation, 0, 1, true, 1d, 1d);
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void TryGetLine_InvalidIndex_ReturnsFalse(int index)
        {
            var evaluation = Evaluate(new[] { Requirement(1, 4d, 3d, NumericRequirementComparison.AtLeast) });
            Assert.That(evaluation.TryGetLine(index, out var line), Is.False);
            Assert.That(line, Is.EqualTo(default(NumericRequirementLine)));
        }

        [Test]
        public void SameInput_ReturnsBitStableLines()
        {
            var requirements = new[] { Requirement(1, 0.3d, 0.2d, NumericRequirementComparison.GreaterThan) };
            var first = Evaluate(requirements);
            var second = Evaluate(requirements);
            first.TryGetLine(0, out var left);
            second.TryGetLine(0, out var right);
            Assert.That(BitConverter.DoubleToInt64Bits(left.Delta), Is.EqualTo(BitConverter.DoubleToInt64Bits(right.Delta)));
            Assert.That(left, Is.EqualTo(right));
        }

        [Test]
        public void RequirementAndLineEquality_UseAllFields()
        {
            var first = Requirement(1, 3d, 2d, NumericRequirementComparison.AtLeast);
            var second = Requirement(1, 3d, 2d, NumericRequirementComparison.AtLeast);
            var different = Requirement(2, 3d, 2d, NumericRequirementComparison.AtLeast);
            Assert.That(first == second, Is.True);
            Assert.That(first != different, Is.True);
            var evaluation = Evaluate(new[] { first });
            evaluation.TryGetLine(0, out var line);
            Assert.That(line == line, Is.True);
            Assert.That(line.GetHashCode(), Is.EqualTo(line.GetHashCode()));
        }

        [Test]
        public void PublicRuntimeSurface_ContainsExactlySixTypes()
        {
            var actual = typeof(NumericRequirementEvaluator).Assembly.GetExportedTypes().Where(type => string.Equals(type.Namespace, "GameplayRules", StringComparison.Ordinal)).OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
            var expected = new[]
            {
                typeof(NumericRequirement),
                typeof(NumericRequirementComparison),
                typeof(NumericRequirementError),
                typeof(NumericRequirementEvaluation),
                typeof(NumericRequirementEvaluator),
                typeof(NumericRequirementLine)
            }.OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
            Assert.That(actual, Is.EqualTo(expected));
        }

        private static NumericRequirement Requirement(int identifier, double actual, double expected, NumericRequirementComparison comparison, double tolerance = 0d)
            => new NumericRequirement(identifier, actual, expected, comparison, tolerance);

        private static NumericRequirementEvaluation Evaluate(NumericRequirement[] requirements)
        {
            Assert.That(NumericRequirementEvaluator.TryEvaluate(requirements, out var evaluation, out var error), Is.True, error.ToString());
            Assert.That(error, Is.EqualTo(NumericRequirementError.None));
            Assert.That(evaluation, Is.Not.Null);
            return evaluation;
        }

        private static void AssertFailure(NumericRequirement[] requirements, NumericRequirementError expected)
        {
            Assert.That(NumericRequirementEvaluator.TryEvaluate(requirements, out var evaluation, out var error), Is.False);
            Assert.That(evaluation, Is.Null);
            Assert.That(error, Is.EqualTo(expected));
        }

        private static void AssertSingle(double actual, double expected, NumericRequirementComparison comparison, double tolerance, bool satisfied)
        {
            var evaluation = Evaluate(new[] { Requirement(1, actual, expected, comparison, tolerance) });
            Assert.That(evaluation.AllSatisfied, Is.EqualTo(satisfied));
            Assert.That(evaluation.TryGetLine(0, out var line), Is.True);
            Assert.That(line.IsSatisfied, Is.EqualTo(satisfied));
            Assert.That(line.ActualValue, Is.EqualTo(actual));
            Assert.That(line.ExpectedValue, Is.EqualTo(expected));
            Assert.That(line.Comparison, Is.EqualTo(comparison));
            Assert.That(line.Tolerance, Is.EqualTo(tolerance));
        }

        private static void AssertLine(NumericRequirementEvaluation evaluation, int index, int identifier, bool satisfied, double delta, double absoluteDelta)
        {
            Assert.That(evaluation.TryGetLine(index, out var line), Is.True);
            Assert.That(line.Identifier, Is.EqualTo(identifier));
            Assert.That(line.IsSatisfied, Is.EqualTo(satisfied));
            Assert.That(line.Delta, Is.EqualTo(delta));
            Assert.That(line.AbsoluteDelta, Is.EqualTo(absoluteDelta));
        }
    }
}
