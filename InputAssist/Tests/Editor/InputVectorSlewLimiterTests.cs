using NUnit.Framework;

namespace InputSmoothing.Tests
{
    public sealed class InputVectorSlewLimiterTests
    {
        [TestCase(0d, 0d, 0d, InputVectorSlewLimiterError.InvalidConfiguration)]
        [TestCase(-0.1d, 0d, 0d, InputVectorSlewLimiterError.InvalidConfiguration)]
        [TestCase(double.NaN, 0d, 0d, InputVectorSlewLimiterError.InvalidConfiguration)]
        [TestCase(0.1d, double.NaN, 0d, InputVectorSlewLimiterError.NonFiniteInput)]
        [TestCase(0.1d, 1.1d, 0d, InputVectorSlewLimiterError.InputOutOfRange)]
        public void TryCreate_InvalidInput_Fails(double maximumDelta, double horizontal, double vertical, InputVectorSlewLimiterError expected)
        {
            Assert.That(InputVectorSlewLimiter.TryCreate(maximumDelta, horizontal, vertical, out var limiter, out var error), Is.False);
            Assert.That(limiter, Is.Null);
            Assert.That(error, Is.EqualTo(expected));
        }

        [Test]
        public void TryCreate_ValidInput_ExposesReconstructableState()
        {
            var limiter = Create(0.25d, -0.5d, 0.75d);
            Assert.That(limiter.MaximumDeltaPerStep, Is.EqualTo(0.25d));
            Assert.That(limiter.CurrentHorizontal, Is.EqualTo(-0.5d));
            Assert.That(limiter.CurrentVertical, Is.EqualTo(0.75d));
        }

        [Test]
        public void Process_TargetInsideLimit_ReachesExactly()
        {
            var result = Create(0.5d).Process(0.3d, 0.4d);
            AssertSuccess(result, 0.3d, 0.4d, 0.5d, true);
        }

        [Test]
        public void Process_TargetAtInclusiveLimit_ReachesExactly()
        {
            AssertSuccess(Create(0.5d).Process(-0.3d, 0.4d), -0.3d, 0.4d, 0.5d, true);
        }

        [Test]
        public void Process_TargetOutsideLimit_PreservesDeltaDirection()
        {
            AssertSuccess(Create(0.25d).Process(0.6d, 0.8d), 0.15d, 0.2d, 0.25d, false);
        }

        [Test]
        public void Process_RepeatedSteps_ConvergeWithoutOvershoot()
        {
            var limiter = Create(0.25d);
            AssertSuccess(limiter.Process(1d, 0d), 0.25d, 0d, 0.25d, false);
            AssertSuccess(limiter.Process(1d, 0d), 0.5d, 0d, 0.25d, false);
            AssertSuccess(limiter.Process(1d, 0d), 0.75d, 0d, 0.25d, false);
            AssertSuccess(limiter.Process(1d, 0d), 1d, 0d, 0.25d, true);
            AssertSuccess(limiter.Process(1d, 0d), 1d, 0d, 0d, true);
        }

        [Test]
        public void Process_TargetFlip_UsesCurrentState()
        {
            var limiter = Create(0.5d);
            limiter.Process(1d, 0d);
            AssertSuccess(limiter.Process(-1d, 0d), 0d, 0d, 0.5d, false);
        }

        [TestCase(double.NaN, 0d, InputVectorSlewLimiterError.NonFiniteInput)]
        [TestCase(double.PositiveInfinity, 0d, InputVectorSlewLimiterError.NonFiniteInput)]
        [TestCase(1.01d, 0d, InputVectorSlewLimiterError.InputOutOfRange)]
        [TestCase(0d, -1.01d, InputVectorSlewLimiterError.InputOutOfRange)]
        public void Process_InvalidTarget_DoesNotMutate(double horizontal, double vertical, InputVectorSlewLimiterError expected)
        {
            var limiter = Create(0.25d, 0.2d, -0.3d);
            var result = limiter.Process(horizontal, vertical);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(expected));
            Assert.That(limiter.CurrentHorizontal, Is.EqualTo(0.2d));
            Assert.That(limiter.CurrentVertical, Is.EqualTo(-0.3d));
        }

        [Test]
        public void TryReset_ValidState_ReconstructsExactly()
        {
            var limiter = Create(0.25d);
            limiter.Process(1d, 0d);
            Assert.That(limiter.TryReset(-0.75d, 0.5d, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputVectorSlewLimiterError.None));
            Assert.That(limiter.CurrentHorizontal, Is.EqualTo(-0.75d));
            Assert.That(limiter.CurrentVertical, Is.EqualTo(0.5d));
        }

        [Test]
        public void TryReset_InvalidState_DoesNotMutate()
        {
            var limiter = Create(0.25d, 0.1d, 0.2d);
            Assert.That(limiter.TryReset(2d, 0d, out var error), Is.False);
            Assert.That(error, Is.EqualTo(InputVectorSlewLimiterError.InputOutOfRange));
            Assert.That(limiter.CurrentHorizontal, Is.EqualTo(0.1d));
            Assert.That(limiter.CurrentVertical, Is.EqualTo(0.2d));
        }

        [Test]
        public void IndependentInstances_DoNotShareState()
        {
            var first = Create(0.5d);
            var second = Create(0.5d);
            first.Process(1d, 0d);
            Assert.That(second.CurrentHorizontal, Is.Zero);
            Assert.That(second.CurrentVertical, Is.Zero);
        }

        [Test]
        public void ResultEquality_IncludesEveryObservableField()
        {
            var a = Create(0.5d).Process(0.3d, 0.4d);
            var b = Create(0.5d).Process(0.3d, 0.4d);
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a == b, Is.True);
            Assert.That(a, Is.Not.EqualTo(default(InputVectorSlewResult)));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        private static InputVectorSlewLimiter Create(double maximumDelta, double horizontal = 0d, double vertical = 0d)
        {
            Assert.That(InputVectorSlewLimiter.TryCreate(maximumDelta, horizontal, vertical, out var limiter, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputVectorSlewLimiterError.None));
            return limiter;
        }

        private static void AssertSuccess(InputVectorSlewResult result, double horizontal, double vertical, double applied, bool reached)
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Error, Is.EqualTo(InputVectorSlewLimiterError.None));
            Assert.That(result.Horizontal, Is.EqualTo(horizontal).Within(1e-12d));
            Assert.That(result.Vertical, Is.EqualTo(vertical).Within(1e-12d));
            Assert.That(result.AppliedDeltaMagnitude, Is.EqualTo(applied).Within(1e-12d));
            Assert.That(result.ReachedTarget, Is.EqualTo(reached));
        }
    }
}
