using NUnit.Framework;

namespace InputFiltering.Tests
{
    public sealed class InputVectorExponentialSmootherTests
    {
        [TestCase(0d)]
        [TestCase(-0.1d)]
        [TestCase(1.0000001d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void TryCreate_InvalidFactor_Fails(double factor)
        {
            Assert.That(InputVectorExponentialSmoother.TryCreate(factor, 0d, 0d, out var smoother, out var error), Is.False);
            Assert.That(smoother, Is.Null);
            Assert.That(error, Is.EqualTo(InputVectorExponentialSmootherError.InvalidConfiguration));
        }

        [TestCase(0.000001d)]
        [TestCase(0.5d)]
        [TestCase(1d)]
        public void TryCreate_ValidFactorAndInitialState_Succeeds(double factor)
        {
            Assert.That(InputVectorExponentialSmoother.TryCreate(factor, -0.25d, 0.75d, out var smoother, out var error), Is.True);
            Assert.That(smoother.SmoothingFactor, Is.EqualTo(factor));
            Assert.That(smoother.CurrentHorizontal, Is.EqualTo(-0.25d));
            Assert.That(smoother.CurrentVertical, Is.EqualTo(0.75d));
            Assert.That(error, Is.EqualTo(InputVectorExponentialSmootherError.None));
        }

        [TestCase(double.NaN, 0d, InputVectorExponentialSmootherError.NonFiniteInput)]
        [TestCase(0d, double.NegativeInfinity, InputVectorExponentialSmootherError.NonFiniteInput)]
        [TestCase(1.000001d, 0d, InputVectorExponentialSmootherError.InputOutOfRange)]
        [TestCase(0d, -1.000001d, InputVectorExponentialSmootherError.InputOutOfRange)]
        public void TryCreate_InvalidInitialState_Fails(double horizontal, double vertical, InputVectorExponentialSmootherError expected)
        {
            Assert.That(InputVectorExponentialSmoother.TryCreate(0.5d, horizontal, vertical, out var smoother, out var error), Is.False);
            Assert.That(smoother, Is.Null);
            Assert.That(error, Is.EqualTo(expected));
        }

        [Test]
        public void Process_RepeatedHalfFactor_FollowsGoldenSequence()
        {
            var smoother = Create(0.5d, 0d, 0d);
            AssertSuccess(smoother.Process(1d, 0d), 0.5d, 0d, 0.5d, 0.5d, false);
            AssertSuccess(smoother.Process(1d, 0d), 0.75d, 0d, 0.25d, 0.25d, false);
            AssertSuccess(smoother.Process(1d, 0d), 0.875d, 0d, 0.125d, 0.125d, false);
        }

        [Test]
        public void Process_Diagonal_PreservesTargetDirectionFromZero()
        {
            AssertSuccess(Create(0.5d, 0d, 0d).Process(0.6d, 0.8d), 0.3d, 0.4d, 0.5d, 0.5d, false);
        }

        [Test]
        public void Process_FactorOne_ReachesTargetExactly()
        {
            AssertSuccess(Create(1d, -1d, 1d).Process(0.5d, -0.5d), 0.5d, -0.5d, System.Math.Sqrt(4.5d), 0d, true);
        }

        [Test]
        public void Process_CurrentTarget_ReturnsReachedWithoutChange()
        {
            AssertSuccess(Create(0.25d, 0.2d, -0.3d).Process(0.2d, -0.3d), 0.2d, -0.3d, 0d, 0d, true);
        }

        [TestCase(double.NaN, 0d, InputVectorExponentialSmootherError.NonFiniteInput)]
        [TestCase(0d, double.PositiveInfinity, InputVectorExponentialSmootherError.NonFiniteInput)]
        [TestCase(-1.1d, 0d, InputVectorExponentialSmootherError.InputOutOfRange)]
        [TestCase(0d, 1.1d, InputVectorExponentialSmootherError.InputOutOfRange)]
        public void Process_InvalidTarget_PreservesState(double horizontal, double vertical, InputVectorExponentialSmootherError expected)
        {
            var smoother = Create(0.5d, 0.25d, -0.25d);
            var result = smoother.Process(horizontal, vertical);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(expected));
            Assert.That(smoother.CurrentHorizontal, Is.EqualTo(0.25d));
            Assert.That(smoother.CurrentVertical, Is.EqualTo(-0.25d));
        }

        [Test]
        public void TryReset_ValidState_ReconstructsFollowingSequence()
        {
            var first = Create(0.5d, 0d, 0d);
            first.Process(1d, 0.5d);
            var second = Create(0.5d, -1d, -1d);
            Assert.That(second.TryReset(first.CurrentHorizontal, first.CurrentVertical, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputVectorExponentialSmootherError.None));
            Assert.That(second.Process(-1d, 1d), Is.EqualTo(first.Process(-1d, 1d)));
        }

        [TestCase(double.NaN, 0d, InputVectorExponentialSmootherError.NonFiniteInput)]
        [TestCase(0d, 2d, InputVectorExponentialSmootherError.InputOutOfRange)]
        public void TryReset_InvalidState_PreservesCurrent(double horizontal, double vertical, InputVectorExponentialSmootherError expected)
        {
            var smoother = Create(0.5d, 0.25d, -0.25d);
            Assert.That(smoother.TryReset(horizontal, vertical, out var error), Is.False);
            Assert.That(error, Is.EqualTo(expected));
            Assert.That(smoother.CurrentHorizontal, Is.EqualTo(0.25d));
            Assert.That(smoother.CurrentVertical, Is.EqualTo(-0.25d));
        }

        [Test]
        public void Process_SubnormalProgress_IsObservableWithoutImplicitSnap()
        {
            var result = Create(0.5d, 0d, 0d).Process(double.Epsilon, 0d);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Horizontal, Is.Zero);
            Assert.That(result.AppliedDeltaMagnitude, Is.Zero);
            Assert.That(result.RemainingDeltaMagnitude, Is.EqualTo(double.Epsilon));
            Assert.That(result.ReachedTarget, Is.False);
        }

        [Test]
        public void ResultEquality_IncludesResidualAndSuccessState()
        {
            var a = Create(0.5d, 0d, 0d).Process(1d, 0d);
            var b = Create(0.5d, 0d, 0d).Process(1d, 0d);
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a == b, Is.True);
            Assert.That(a, Is.Not.EqualTo(default(InputVectorExponentialResult)));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        private static InputVectorExponentialSmoother Create(double factor, double horizontal, double vertical)
        {
            Assert.That(InputVectorExponentialSmoother.TryCreate(factor, horizontal, vertical, out var smoother, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputVectorExponentialSmootherError.None));
            return smoother;
        }

        private static void AssertSuccess(InputVectorExponentialResult result, double horizontal, double vertical, double applied, double remaining, bool reached)
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Error, Is.EqualTo(InputVectorExponentialSmootherError.None));
            Assert.That(result.Horizontal, Is.EqualTo(horizontal).Within(1e-12d));
            Assert.That(result.Vertical, Is.EqualTo(vertical).Within(1e-12d));
            Assert.That(result.AppliedDeltaMagnitude, Is.EqualTo(applied).Within(1e-12d));
            Assert.That(result.RemainingDeltaMagnitude, Is.EqualTo(remaining).Within(1e-12d));
            Assert.That(result.ReachedTarget, Is.EqualTo(reached));
        }
    }
}
