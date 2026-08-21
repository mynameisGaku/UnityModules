using System;
using NUnit.Framework;

namespace InputSmoothing.Tests
{
    public sealed class InputVectorDirectionLimiterTests
    {
        private const double Tolerance = 1e-12d;

        [TestCase(-0.000001d)]
        [TestCase(3.141592653589794d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void TryCreate_InvalidMaximumTurn_Fails(double maximumTurn)
        {
            Assert.That(InputVectorDirectionLimiter.TryCreate(maximumTurn, 0d, 0d, out var limiter, out var error), Is.False);
            Assert.That(limiter, Is.Null);
            Assert.That(error, Is.EqualTo(InputVectorDirectionLimiterError.InvalidConfiguration));
        }

        [TestCase(0d)]
        [TestCase(0.7853981633974483d)]
        [TestCase(3.1415926535897931d)]
        public void TryCreate_ValidBoundaryConfiguration_PreservesState(double maximumTurn)
        {
            Assert.That(InputVectorDirectionLimiter.TryCreate(maximumTurn, 0.6d, 0.8d, out var limiter, out var error), Is.True);
            Assert.That(limiter.MaximumTurnRadians, Is.EqualTo(maximumTurn));
            Assert.That(limiter.CurrentHorizontal, Is.EqualTo(0.6d));
            Assert.That(limiter.CurrentVertical, Is.EqualTo(0.8d));
            Assert.That(error, Is.EqualTo(InputVectorDirectionLimiterError.None));
        }

        [TestCase(double.NaN, 0d, InputVectorDirectionLimiterError.NonFiniteInput)]
        [TestCase(0d, double.NegativeInfinity, InputVectorDirectionLimiterError.NonFiniteInput)]
        [TestCase(1.000001d, 0d, InputVectorDirectionLimiterError.InputOutOfRange)]
        [TestCase(0.8d, 0.8d, InputVectorDirectionLimiterError.InputOutsideUnitCircle)]
        public void TryCreate_InvalidInitialState_Fails(double horizontal, double vertical, InputVectorDirectionLimiterError expected)
        {
            Assert.That(InputVectorDirectionLimiter.TryCreate(Math.PI / 4d, horizontal, vertical, out var limiter, out var error), Is.False);
            Assert.That(limiter, Is.Null);
            Assert.That(error, Is.EqualTo(expected));
        }

        [Test]
        public void Process_FromZero_AdoptsTargetDirectionAndMagnitudeImmediately()
        {
            var result = Create(Math.PI / 8d, 0d, 0d).Process(0.6d, 0.8d);
            AssertResult(result, 0.6d, 0.8d, 1d, 0d, 0d, false, true);
        }

        [Test]
        public void Process_ZeroTarget_ClearsMagnitudeWithoutInventingDirection()
        {
            var limiter = Create(Math.PI / 8d, 0.6d, 0.8d);
            AssertResult(limiter.Process(0d, 0d), 0d, 0d, 0d, 0d, 0d, true, true);
            Assert.That(limiter.CurrentHorizontal, Is.Zero);
            Assert.That(limiter.CurrentVertical, Is.Zero);
        }

        [Test]
        public void Process_QuarterTurn_AppliesConfiguredDirectionStep()
        {
            var result = Create(Math.PI / 4d, 1d, 0d).Process(0d, 1d);
            var component = Math.Sqrt(0.5d);
            AssertResult(result, component, component, 1d, Math.PI / 4d, Math.PI / 4d, true, false);
        }

        [Test]
        public void Process_RepeatedQuarterTurns_ReachesTargetExactly()
        {
            var limiter = Create(Math.PI / 4d, 1d, 0d);
            Assert.That(limiter.Process(0d, 1d).ReachedTargetDirection, Is.False);
            var second = limiter.Process(0d, 1d);
            Assert.That(second.Succeeded, Is.True);
            Assert.That(second.Horizontal, Is.EqualTo(0d));
            Assert.That(second.Vertical, Is.EqualTo(1d));
            Assert.That(second.ReachedTargetDirection, Is.True);
            Assert.That(second.RemainingTurnRadians, Is.Zero);
        }

        [Test]
        public void Process_ClockwiseTarget_UsesNegativeShortestTurn()
        {
            var result = Create(Math.PI / 4d, 1d, 0d).Process(0d, -1d);
            var component = Math.Sqrt(0.5d);
            AssertResult(result, component, -component, 1d, Math.PI / 4d, Math.PI / 4d, true, false);
        }

        [Test]
        public void Process_ExactOpposite_UsesDeterministicCounterClockwiseTieBreak()
        {
            var result = Create(Math.PI / 2d, 1d, 0d).Process(-1d, 0d);
            AssertResult(result, 0d, 1d, 1d, Math.PI / 2d, Math.PI / 2d, true, false);
        }

        [Test]
        public void Process_ZeroMaximumTurn_RetainsDirectionButAppliesTargetMagnitude()
        {
            var result = Create(0d, 1d, 0d).Process(0d, 0.25d);
            AssertResult(result, 0.25d, 0d, 0.25d, 0d, Math.PI / 2d, true, false);
        }

        [Test]
        public void Process_LimitedTurn_AppliesTargetMagnitudeOnFirstStep()
        {
            var result = Create(Math.PI / 4d, 1d, 0d).Process(0d, 0.25d);
            Assert.That(Math.Sqrt(result.Horizontal * result.Horizontal + result.Vertical * result.Vertical), Is.EqualTo(0.25d).Within(Tolerance));
            Assert.That(result.TargetMagnitude, Is.EqualTo(0.25d));
        }

        [Test]
        public void Process_PiMaximum_ReachesAnyTargetExactly()
        {
            var result = Create(Math.PI, 1d, 0d).Process(-0.5d, 0d);
            AssertResult(result, -0.5d, 0d, 0.5d, Math.PI, 0d, true, true);
        }

        [Test]
        public void Process_SameDirection_ChangesMagnitudeWithoutTurn()
        {
            var result = Create(Math.PI / 8d, 0.25d, 0d).Process(0.75d, 0d);
            AssertResult(result, 0.75d, 0d, 0.75d, 0d, 0d, true, true);
        }

        [TestCase(double.NaN, 0d, InputVectorDirectionLimiterError.NonFiniteInput)]
        [TestCase(0d, double.PositiveInfinity, InputVectorDirectionLimiterError.NonFiniteInput)]
        [TestCase(-1.1d, 0d, InputVectorDirectionLimiterError.InputOutOfRange)]
        [TestCase(0.75d, 0.75d, InputVectorDirectionLimiterError.InputOutsideUnitCircle)]
        public void Process_InvalidTarget_DoesNotMutateState(double horizontal, double vertical, InputVectorDirectionLimiterError expected)
        {
            var limiter = Create(Math.PI / 4d, 0.6d, 0.8d);
            var result = limiter.Process(horizontal, vertical);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(expected));
            Assert.That(limiter.CurrentHorizontal, Is.EqualTo(0.6d));
            Assert.That(limiter.CurrentVertical, Is.EqualTo(0.8d));
        }

        [Test]
        public void TryReset_ValidState_ReconstructsProcessor()
        {
            var limiter = Create(Math.PI / 4d, 1d, 0d);
            Assert.That(limiter.TryReset(0d, -0.5d, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputVectorDirectionLimiterError.None));
            Assert.That(limiter.CurrentHorizontal, Is.Zero);
            Assert.That(limiter.CurrentVertical, Is.EqualTo(-0.5d));
        }

        [Test]
        public void TryReset_InvalidState_DoesNotMutateProcessor()
        {
            var limiter = Create(Math.PI / 4d, 0.6d, 0.8d);
            Assert.That(limiter.TryReset(0.8d, 0.8d, out var error), Is.False);
            Assert.That(error, Is.EqualTo(InputVectorDirectionLimiterError.InputOutsideUnitCircle));
            Assert.That(limiter.CurrentHorizontal, Is.EqualTo(0.6d));
            Assert.That(limiter.CurrentVertical, Is.EqualTo(0.8d));
        }

        [Test]
        public void Process_SubnormalTargetFromZero_PreservesExplicitValue()
        {
            var result = Create(Math.PI / 4d, 0d, 0d).Process(double.Epsilon, 0d);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Horizontal, Is.EqualTo(double.Epsilon));
            Assert.That(result.Vertical, Is.Zero);
            Assert.That(result.TargetMagnitude, Is.EqualTo(double.Epsilon));
        }

        [Test]
        public void Result_EqualityAndDefaultValidity_AreExplicit()
        {
            var first = Create(Math.PI / 4d, 1d, 0d).Process(0d, 1d);
            var second = Create(Math.PI / 4d, 1d, 0d).Process(0d, 1d);
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(default(InputVectorDirectionLimitResult).Succeeded, Is.False);
        }

        [Test]
        public void Process_AllSuccessfulSteps_RemainInsideUnitCircle()
        {
            var limiter = Create(Math.PI / 17d, 0.6d, 0.8d);
            for (var index = 0; index < 40; index++)
            {
                var result = limiter.Process(-0.8d, -0.6d);
                Assert.That(result.Succeeded, Is.True);
                Assert.That(Math.Sqrt(result.Horizontal * result.Horizontal + result.Vertical * result.Vertical), Is.LessThanOrEqualTo(1d + Tolerance));
            }
        }

        private static InputVectorDirectionLimiter Create(double maximumTurn, double horizontal, double vertical)
        {
            Assert.That(InputVectorDirectionLimiter.TryCreate(maximumTurn, horizontal, vertical, out var limiter, out var error), Is.True, error.ToString());
            return limiter;
        }

        private static void AssertResult(InputVectorDirectionLimitResult result, double horizontal, double vertical, double magnitude, double applied, double remaining, bool hadPrior, bool reached)
        {
            Assert.That(result.Succeeded, Is.True, result.Error.ToString());
            Assert.That(result.Horizontal, Is.EqualTo(horizontal).Within(Tolerance));
            Assert.That(result.Vertical, Is.EqualTo(vertical).Within(Tolerance));
            Assert.That(result.TargetMagnitude, Is.EqualTo(magnitude).Within(Tolerance));
            Assert.That(result.AppliedTurnRadians, Is.EqualTo(applied).Within(Tolerance));
            Assert.That(result.RemainingTurnRadians, Is.EqualTo(remaining).Within(Tolerance));
            Assert.That(result.HadPriorDirection, Is.EqualTo(hadPrior));
            Assert.That(result.ReachedTargetDirection, Is.EqualTo(reached));
            Assert.That(result.Error, Is.EqualTo(InputVectorDirectionLimiterError.None));
        }
    }
}
