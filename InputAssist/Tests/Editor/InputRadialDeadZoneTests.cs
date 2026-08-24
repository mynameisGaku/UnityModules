using System;
using NUnit.Framework;

namespace InputDeadZones.Tests
{
    public sealed class InputRadialDeadZoneTests
    {
        [TestCase(-0.1d, 1d)]
        [TestCase(0.1d, 0.1d)]
        [TestCase(0.5d, 0.4d)]
        [TestCase(0d, 1.000001d)]
        [TestCase(double.NaN, 1d)]
        [TestCase(0d, double.PositiveInfinity)]
        public void TryCreate_InvalidConfiguration_Fails(double inner, double outer)
        {
            Assert.That(InputRadialDeadZone.TryCreate(inner, outer, out var deadZone, out var error), Is.False);
            Assert.That(deadZone, Is.EqualTo(default(InputRadialDeadZone)));
            Assert.That(error, Is.EqualTo(InputRadialDeadZoneError.InvalidConfiguration));
        }

        [TestCase(0d, 1d)]
        [TestCase(0.1d, 1d)]
        [TestCase(0.25d, 0.75d)]
        public void TryCreate_ValidBoundary_Succeeds(double inner, double outer)
        {
            Assert.That(InputRadialDeadZone.TryCreate(inner, outer, out var deadZone, out var error), Is.True);
            Assert.That(deadZone.IsValid, Is.True);
            Assert.That(deadZone.InnerDeadZone, Is.EqualTo(inner));
            Assert.That(deadZone.OuterDeadZone, Is.EqualTo(outer));
            Assert.That(error, Is.EqualTo(InputRadialDeadZoneError.None));
        }

        [Test]
        public void DefaultDeadZone_ReturnsInvalidConfiguration()
        {
            var result = default(InputRadialDeadZone).Process(0d, 0d);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(InputRadialDeadZoneError.InvalidConfiguration));
        }

        [TestCase(double.NaN, 0d)]
        [TestCase(double.PositiveInfinity, 0d)]
        [TestCase(0d, double.NegativeInfinity)]
        public void Process_NonFiniteComponent_Fails(double horizontal, double vertical)
        {
            var result = Create(0.1d, 1d).Process(horizontal, vertical);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Horizontal, Is.Zero);
            Assert.That(result.Vertical, Is.Zero);
            Assert.That(result.Magnitude, Is.Zero);
            Assert.That(result.Error, Is.EqualTo(InputRadialDeadZoneError.NonFiniteInput));
        }

        [TestCase(0d, 0d)]
        [TestCase(0.1d, 0d)]
        [TestCase(0d, -0.1d)]
        [TestCase(0.06d, 0.08d)]
        public void Process_InsideInclusiveInnerBoundary_IsZero(double horizontal, double vertical)
        {
            var result = Create(0.1d, 1d).Process(horizontal, vertical);
            AssertSuccess(result, 0d, 0d, 0d);
            Assert.That(result.IsZero, Is.True);
        }

        [Test]
        public void Process_BetweenBoundaries_RemapsMagnitudeLinearly()
        {
            AssertSuccess(Create(0.1d, 1d).Process(0.55d, 0d), 0.5d, 0d, 0.5d);
        }

        [Test]
        public void Process_DiagonalBetweenBoundaries_PreservesDirection()
        {
            var result = Create(0.2d, 1d).Process(0.3d, 0.4d);
            AssertSuccess(result, 0.225d, 0.3d, 0.375d);
            Assert.That(result.Horizontal / result.Vertical, Is.EqualTo(0.75d).Within(1e-12d));
        }

        [TestCase(0d, 1d, 0d, 1d)]
        [TestCase(1d, 0d, 1d, 0d)]
        [TestCase(-1d, 0d, -1d, 0d)]
        public void Process_AtInclusiveOuterBoundary_ReturnsUnit(double horizontal, double vertical, double expectedHorizontal, double expectedVertical)
        {
            AssertSuccess(Create(0.1d, 1d).Process(horizontal, vertical), expectedHorizontal, expectedVertical, 1d);
        }

        [Test]
        public void Process_OverRangeVector_NormalizesWithoutChangingDirection()
        {
            AssertSuccess(Create(0.1d, 1d).Process(3d, 4d), 0.6d, 0.8d, 1d);
        }

        [Test]
        public void Process_MaxFiniteComponents_DoesNotOverflowDirection()
        {
            var result = Create(0.1d, 1d).Process(double.MaxValue, -double.MaxValue);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Magnitude, Is.EqualTo(1d));
            Assert.That(result.Horizontal, Is.EqualTo(Math.Sqrt(0.5d)).Within(1e-15d));
            Assert.That(result.Vertical, Is.EqualTo(-Math.Sqrt(0.5d)).Within(1e-15d));
        }

        [Test]
        public void Process_SubnormalVector_RemainsFiniteAndZero()
        {
            AssertSuccess(Create(0.1d, 1d).Process(double.Epsilon, -double.Epsilon), 0d, 0d, 0d);
        }

        [Test]
        public void Equality_UsesBothBoundaries()
        {
            var a = Create(0.1d, 1d);
            var b = Create(0.1d, 1d);
            var c = Create(0.2d, 1d);
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a == b, Is.True);
            Assert.That(a != c, Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void ResultEquality_IncludesComponentsMagnitudeAndSuccessState()
        {
            var a = Create(0.1d, 1d).Process(0.55d, 0d);
            var b = Create(0.1d, 1d).Process(0.55d, 0d);
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a == b, Is.True);
            Assert.That(a, Is.Not.EqualTo(default(InputRadialDeadZoneResult)));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        private static InputRadialDeadZone Create(double inner, double outer)
        {
            Assert.That(InputRadialDeadZone.TryCreate(inner, outer, out var deadZone, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputRadialDeadZoneError.None));
            return deadZone;
        }

        private static void AssertSuccess(InputRadialDeadZoneResult result, double horizontal, double vertical, double magnitude)
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Error, Is.EqualTo(InputRadialDeadZoneError.None));
            Assert.That(result.Horizontal, Is.EqualTo(horizontal).Within(1e-12d));
            Assert.That(result.Vertical, Is.EqualTo(vertical).Within(1e-12d));
            Assert.That(result.Magnitude, Is.EqualTo(magnitude).Within(1e-12d));
        }
    }
}
