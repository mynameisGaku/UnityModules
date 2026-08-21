using System;
using NUnit.Framework;

namespace InputResponse.Tests
{
    public sealed class InputVectorResponseCurveTests
    {
        [TestCase((InputVectorResponseMode)0)]
        [TestCase((InputVectorResponseMode)5)]
        [TestCase((InputVectorResponseMode)(-1))]
        public void TryCreate_UndefinedMode_Fails(InputVectorResponseMode mode)
        {
            Assert.That(InputVectorResponseCurve.TryCreate(mode, out var curve, out var error), Is.False);
            Assert.That(curve, Is.EqualTo(default(InputVectorResponseCurve)));
            Assert.That(error, Is.EqualTo(InputVectorResponseCurveError.InvalidConfiguration));
        }

        [TestCase(InputVectorResponseMode.Linear)]
        [TestCase(InputVectorResponseMode.Squared)]
        [TestCase(InputVectorResponseMode.Cubic)]
        [TestCase(InputVectorResponseMode.SmoothStep)]
        public void TryCreate_DefinedMode_Succeeds(InputVectorResponseMode mode)
        {
            Assert.That(InputVectorResponseCurve.TryCreate(mode, out var curve, out var error), Is.True);
            Assert.That(curve.IsValid, Is.True);
            Assert.That(curve.Mode, Is.EqualTo(mode));
            Assert.That(error, Is.EqualTo(InputVectorResponseCurveError.None));
        }

        [Test]
        public void DefaultCurve_ReturnsInvalidConfiguration()
        {
            var result = default(InputVectorResponseCurve).Process(0d, 0d);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(InputVectorResponseCurveError.InvalidConfiguration));
        }

        [TestCase(double.NaN, 0d)]
        [TestCase(double.PositiveInfinity, 0d)]
        [TestCase(0d, double.NegativeInfinity)]
        public void Process_NonFiniteComponent_Fails(double horizontal, double vertical)
        {
            AssertFailure(Create(InputVectorResponseMode.Linear).Process(horizontal, vertical), InputVectorResponseCurveError.NonFiniteInput);
        }

        [TestCase(1.0000000001d, 0d)]
        [TestCase(0d, -1.0000000001d)]
        [TestCase(1d, 1d)]
        [TestCase(-0.8d, 0.8d)]
        public void Process_OutsideUnitCircle_Fails(double horizontal, double vertical)
        {
            AssertFailure(Create(InputVectorResponseMode.Linear).Process(horizontal, vertical), InputVectorResponseCurveError.InputOutOfRange);
        }

        [TestCase(InputVectorResponseMode.Linear)]
        [TestCase(InputVectorResponseMode.Squared)]
        [TestCase(InputVectorResponseMode.Cubic)]
        [TestCase(InputVectorResponseMode.SmoothStep)]
        public void Process_Zero_RemainsZero(InputVectorResponseMode mode)
        {
            var result = Create(mode).Process(0d, 0d);
            AssertSuccess(result, 0d, 0d, 0d);
            Assert.That(result.IsZero, Is.True);
        }

        [Test]
        public void Process_Linear_PreservesInput()
        {
            AssertSuccess(Create(InputVectorResponseMode.Linear).Process(0.3d, 0.4d), 0.3d, 0.4d, 0.5d);
        }

        [Test]
        public void Process_Squared_PreservesDirectionAndSquaresMagnitude()
        {
            AssertSuccess(Create(InputVectorResponseMode.Squared).Process(0.3d, 0.4d), 0.15d, 0.2d, 0.25d);
        }

        [Test]
        public void Process_Cubic_PreservesDirectionAndCubesMagnitude()
        {
            AssertSuccess(Create(InputVectorResponseMode.Cubic).Process(0.3d, 0.4d), 0.075d, 0.1d, 0.125d);
        }

        [TestCase(0.6d, 0.8d, 0.6d, 0.8d)]
        [TestCase(-0.6d, 0.8d, -0.6d, 0.8d)]
        public void Process_UnitBoundary_RemainsUnit(double horizontal, double vertical, double expectedHorizontal, double expectedVertical)
        {
            AssertSuccess(Create(InputVectorResponseMode.SmoothStep).Process(horizontal, vertical), expectedHorizontal, expectedVertical, 1d);
        }

        [Test]
        public void Process_SmoothStep_UsesEndpointStableCurve()
        {
            AssertSuccess(Create(InputVectorResponseMode.SmoothStep).Process(0.3d, 0.4d), 0.3d, 0.4d, 0.5d);
            var quarter = Create(InputVectorResponseMode.SmoothStep).Process(0.15d, 0.2d);
            AssertSuccess(quarter, 0.09375d, 0.125d, 0.15625d);
        }

        [Test]
        public void Process_SubnormalVector_RemainsFinite()
        {
            var input = double.Epsilon;
            var result = Create(InputVectorResponseMode.Linear).Process(input, -input);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(double.IsNaN(result.Horizontal) || double.IsInfinity(result.Horizontal), Is.False);
            Assert.That(double.IsNaN(result.Vertical) || double.IsInfinity(result.Vertical), Is.False);
            Assert.That(double.IsNaN(result.Magnitude) || double.IsInfinity(result.Magnitude), Is.False);
        }

        [Test]
        public void Equality_UsesMode()
        {
            var a = Create(InputVectorResponseMode.Squared);
            var b = Create(InputVectorResponseMode.Squared);
            var c = Create(InputVectorResponseMode.Cubic);
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a == b, Is.True);
            Assert.That(a != c, Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void ResultEquality_IncludesComponentsMagnitudeAndSuccessState()
        {
            var a = Create(InputVectorResponseMode.Squared).Process(0.3d, 0.4d);
            var b = Create(InputVectorResponseMode.Squared).Process(0.3d, 0.4d);
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a == b, Is.True);
            Assert.That(a, Is.Not.EqualTo(default(InputVectorResponseResult)));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        private static InputVectorResponseCurve Create(InputVectorResponseMode mode)
        {
            Assert.That(InputVectorResponseCurve.TryCreate(mode, out var curve, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputVectorResponseCurveError.None));
            return curve;
        }

        private static void AssertSuccess(InputVectorResponseResult result, double horizontal, double vertical, double magnitude)
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Error, Is.EqualTo(InputVectorResponseCurveError.None));
            Assert.That(result.Horizontal, Is.EqualTo(horizontal).Within(1e-12d));
            Assert.That(result.Vertical, Is.EqualTo(vertical).Within(1e-12d));
            Assert.That(result.Magnitude, Is.EqualTo(magnitude).Within(1e-12d));
        }

        private static void AssertFailure(InputVectorResponseResult result, InputVectorResponseCurveError error)
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Horizontal, Is.Zero);
            Assert.That(result.Vertical, Is.Zero);
            Assert.That(result.Magnitude, Is.Zero);
            Assert.That(result.Error, Is.EqualTo(error));
        }
    }
}
