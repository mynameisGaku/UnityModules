using System;
using NUnit.Framework;

namespace InputDirectionQuantization.Tests
{
    public sealed class InputDirectionQuantizerTests
    {
        [TestCase(-0.1d, InputDirectionMode.FourWay)]
        [TestCase(1d, InputDirectionMode.EightWay)]
        [TestCase(double.NaN, InputDirectionMode.FourWay)]
        [TestCase(double.PositiveInfinity, InputDirectionMode.EightWay)]
        [TestCase(0.1d, (InputDirectionMode)0)]
        [TestCase(0.1d, (InputDirectionMode)3)]
        public void TryCreate_InvalidConfiguration_Fails(double deadZone, InputDirectionMode mode)
        {
            Assert.That(InputDirectionQuantizer.TryCreate(deadZone, mode, out var quantizer, out var error), Is.False);
            Assert.That(quantizer, Is.EqualTo(default(InputDirectionQuantizer)));
            Assert.That(error, Is.EqualTo(InputDirectionQuantizationError.InvalidConfiguration));
        }

        [TestCase(0d, InputDirectionMode.FourWay)]
        [TestCase(0.1d, InputDirectionMode.EightWay)]
        [TestCase(0.999999d, InputDirectionMode.FourWay)]
        public void TryCreate_ValidBoundary_Succeeds(double deadZone, InputDirectionMode mode)
        {
            Assert.That(InputDirectionQuantizer.TryCreate(deadZone, mode, out var quantizer, out var error), Is.True);
            Assert.That(quantizer.IsValid, Is.True);
            Assert.That(quantizer.DeadZone, Is.EqualTo(deadZone));
            Assert.That(quantizer.Mode, Is.EqualTo(mode));
            Assert.That(error, Is.EqualTo(InputDirectionQuantizationError.None));
        }

        [Test]
        public void DefaultQuantizer_ReturnsInvalidConfiguration()
        {
            var result = default(InputDirectionQuantizer).Quantize(0d, 0d);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(InputDirectionQuantizationError.InvalidConfiguration));
        }

        [TestCase(double.NaN, 0d)]
        [TestCase(double.PositiveInfinity, 0d)]
        [TestCase(0d, double.NegativeInfinity)]
        [TestCase(0d, double.NaN)]
        public void Quantize_NonFiniteComponent_Fails(double horizontal, double vertical)
        {
            var result = Create(0.1d, InputDirectionMode.EightWay).Quantize(horizontal, vertical);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Horizontal, Is.Zero);
            Assert.That(result.Vertical, Is.Zero);
            Assert.That(result.Error, Is.EqualTo(InputDirectionQuantizationError.NonFiniteInput));
        }

        [TestCase(0d, 0d)]
        [TestCase(0.1d, 0d)]
        [TestCase(0d, -0.1d)]
        [TestCase(0.06d, 0.08d)]
        public void Quantize_InsideInclusiveRadialDeadZone_IsNeutral(double horizontal, double vertical)
        {
            var result = Create(0.1d, InputDirectionMode.EightWay).Quantize(horizontal, vertical);
            AssertDirection(result, 0, 0);
            Assert.That(result.IsNeutral, Is.True);
        }

        [Test]
        public void Quantize_ComponentsInsideDeadZoneButRadialOutside_IsNotNeutral()
        {
            AssertDirection(Create(0.1d, InputDirectionMode.EightWay).Quantize(0.08d, 0.08d), 1, 1);
        }

        [TestCase(0.9d, 0.2d, 1, 0)]
        [TestCase(-0.9d, 0.2d, -1, 0)]
        [TestCase(0.2d, 0.9d, 0, 1)]
        [TestCase(0.2d, -0.9d, 0, -1)]
        public void Quantize_FourWay_SelectsDominantAxis(double horizontal, double vertical, int expectedHorizontal, int expectedVertical)
        {
            AssertDirection(Create(0d, InputDirectionMode.FourWay).Quantize(horizontal, vertical), expectedHorizontal, expectedVertical);
        }

        [TestCase(0.5d, 0.5d, 0, 1)]
        [TestCase(-0.5d, 0.5d, 0, 1)]
        [TestCase(0.5d, -0.5d, 0, -1)]
        [TestCase(-0.5d, -0.5d, 0, -1)]
        public void Quantize_FourWayExactTie_PrefersVertical(double horizontal, double vertical, int expectedHorizontal, int expectedVertical)
        {
            AssertDirection(Create(0d, InputDirectionMode.FourWay).Quantize(horizontal, vertical), expectedHorizontal, expectedVertical);
        }

        [TestCase(0.9d, 0.1d, 1, 0)]
        [TestCase(-0.9d, 0.1d, -1, 0)]
        [TestCase(0.1d, 0.9d, 0, 1)]
        [TestCase(0.1d, -0.9d, 0, -1)]
        [TestCase(0.7d, 0.7d, 1, 1)]
        [TestCase(-0.7d, 0.7d, -1, 1)]
        [TestCase(0.7d, -0.7d, 1, -1)]
        [TestCase(-0.7d, -0.7d, -1, -1)]
        public void Quantize_EightWay_ProducesCardinalAndDiagonalDirections(double horizontal, double vertical, int expectedHorizontal, int expectedVertical)
        {
            var result = Create(0d, InputDirectionMode.EightWay).Quantize(horizontal, vertical);
            AssertDirection(result, expectedHorizontal, expectedVertical);
            Assert.That(result.IsDiagonal, Is.EqualTo(expectedHorizontal != 0 && expectedVertical != 0));
        }

        [Test]
        public void Quantize_EightWayHorizontalBoundary_IsInclusiveCardinal()
        {
            AssertDirection(Create(0d, InputDirectionMode.EightWay).Quantize(1d, InputDirectionQuantizer.DiagonalThreshold), 1, 0);
        }

        [Test]
        public void Quantize_EightWayVerticalBoundary_IsInclusiveCardinal()
        {
            AssertDirection(Create(0d, InputDirectionMode.EightWay).Quantize(InputDirectionQuantizer.DiagonalThreshold, 1d), 0, 1);
        }

        [Test]
        public void Quantize_EightWayJustInsideSector_IsDiagonal()
        {
            var offset = 0.000000000000001d;
            AssertDirection(Create(0d, InputDirectionMode.EightWay).Quantize(1d, InputDirectionQuantizer.DiagonalThreshold + offset), 1, 1);
        }

        [Test]
        public void Quantize_ClampsComponentsBeforeClassification()
        {
            AssertDirection(Create(0d, InputDirectionMode.EightWay).Quantize(10d, -10d), 1, -1);
        }

        [Test]
        public void Equality_UsesDeadZoneAndMode()
        {
            var a = Create(0.1d, InputDirectionMode.EightWay);
            var b = Create(0.1d, InputDirectionMode.EightWay);
            var c = Create(0.1d, InputDirectionMode.FourWay);
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a == b, Is.True);
            Assert.That(a != c, Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void ResultEquality_IncludesDirectionAndSuccessState()
        {
            var a = Create(0d, InputDirectionMode.EightWay).Quantize(1d, 1d);
            var b = Create(0d, InputDirectionMode.EightWay).Quantize(1d, 1d);
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a == b, Is.True);
            Assert.That(a, Is.Not.EqualTo(default(InputDirectionQuantizationResult)));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        private static InputDirectionQuantizer Create(double deadZone, InputDirectionMode mode)
        {
            Assert.That(InputDirectionQuantizer.TryCreate(deadZone, mode, out var quantizer, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputDirectionQuantizationError.None));
            return quantizer;
        }

        private static void AssertDirection(InputDirectionQuantizationResult result, int horizontal, int vertical)
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Error, Is.EqualTo(InputDirectionQuantizationError.None));
            Assert.That(result.Horizontal, Is.EqualTo(horizontal));
            Assert.That(result.Vertical, Is.EqualTo(vertical));
        }
    }
}
