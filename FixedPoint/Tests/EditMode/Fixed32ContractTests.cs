using System;
using System.Linq;
using NUnit.Framework;

namespace FixedPoint.Tests
{
    public sealed class Fixed32ContractTests
    {
        [Test]
        public void Constants_ExposeExactQ16Point16RawValues()
        {
            Assert.That(Fixed32.FractionalBitCount, Is.EqualTo(16));
            Assert.That(Fixed32.Scale, Is.EqualTo(65536));
            Assert.That(Fixed32.Zero.RawValue, Is.Zero);
            Assert.That(Fixed32.One.RawValue, Is.EqualTo(65536));
            Assert.That(Fixed32.MinValue.RawValue, Is.EqualTo(int.MinValue));
            Assert.That(Fixed32.MaxValue.RawValue, Is.EqualTo(int.MaxValue));
        }

        [TestCase(-32768, int.MinValue)]
        [TestCase(-1, -65536)]
        [TestCase(0, 0)]
        [TestCase(1, 65536)]
        [TestCase(32767, 2147418112)]
        public void FromInt32_ValidBoundary_ReturnsExactRaw(int input, int expectedRaw)
        {
            var result = Fixed32.FromInt32(input);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Error, Is.EqualTo(Fixed32Error.None));
            Assert.That(result.Value.RawValue, Is.EqualTo(expectedRaw));
        }

        [TestCase(-32769)]
        [TestCase(32768)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void FromInt32_OutsideRange_ReturnsOverflow(int input)
        {
            var result = Fixed32.FromInt32(input);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(Fixed32Error.Overflow));
            Assert.That(result.Value, Is.EqualTo(Fixed32.Zero));
        }

        [TestCase(3, 2, 98304)]
        [TestCase(-1, 4, -16384)]
        [TestCase(1, 3, 21845)]
        [TestCase(-1, 3, -21845)]
        [TestCase(1, -3, -21845)]
        public void FromRatio_UsesTowardZeroRounding(int numerator, int denominator, int expectedRaw)
        {
            var result = Fixed32.FromRatio(numerator, denominator);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Value.RawValue, Is.EqualTo(expectedRaw));
        }

        [Test]
        public void FromRatio_ZeroDenominator_ReturnsDivisionByZero()
        {
            var result = Fixed32.FromRatio(1, 0);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(Fixed32Error.DivisionByZero));
        }

        [Test]
        public void AddAndSubtract_ReturnExactValues()
        {
            var oneAndHalf = Fixed32.FromRatio(3, 2).Value;
            var quarter = Fixed32.FromRatio(1, 4).Value;
            Assert.That(Fixed32.Add(oneAndHalf, quarter).Value.RawValue, Is.EqualTo(114688));
            Assert.That(Fixed32.Subtract(oneAndHalf, quarter).Value.RawValue, Is.EqualTo(81920));
        }

        [Test]
        public void AddAndSubtract_Overflow_ReturnFailureZero()
        {
            var add = Fixed32.Add(Fixed32.MaxValue, Fixed32.FromRaw(1));
            var subtract = Fixed32.Subtract(Fixed32.MinValue, Fixed32.FromRaw(1));
            Assert.That(add.Error, Is.EqualTo(Fixed32Error.Overflow));
            Assert.That(add.Value, Is.EqualTo(Fixed32.Zero));
            Assert.That(subtract.Error, Is.EqualTo(Fixed32Error.Overflow));
            Assert.That(subtract.Value, Is.EqualTo(Fixed32.Zero));
        }

        [TestCase(3, 2, 2, 1, 196608)]
        [TestCase(-3, 2, 1, 2, -49152)]
        [TestCase(1, 3, 1, 3, 7281)]
        public void Multiply_Uses64BitIntermediateAndTowardZeroRounding(int an, int ad, int bn, int bd, int expectedRaw)
        {
            var left = Fixed32.FromRatio(an, ad).Value;
            var right = Fixed32.FromRatio(bn, bd).Value;
            var result = Fixed32.Multiply(left, right);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Value.RawValue, Is.EqualTo(expectedRaw));
        }

        [Test]
        public void Multiply_Overflow_ReturnsFailure()
        {
            Assert.That(Fixed32.Multiply(Fixed32.MaxValue, Fixed32.FromInt32(2).Value).Error, Is.EqualTo(Fixed32Error.Overflow));
        }

        [TestCase(3, 2, 2, 1, 49152)]
        [TestCase(-3, 2, 2, 1, -49152)]
        [TestCase(1, 1, 3, 1, 21845)]
        public void Divide_Uses64BitIntermediateAndTowardZeroRounding(int an, int ad, int bn, int bd, int expectedRaw)
        {
            var left = Fixed32.FromRatio(an, ad).Value;
            var right = Fixed32.FromRatio(bn, bd).Value;
            var result = Fixed32.Divide(left, right);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Value.RawValue, Is.EqualTo(expectedRaw));
        }

        [Test]
        public void Divide_ZeroAndOverflow_AreExplicit()
        {
            Assert.That(Fixed32.Divide(Fixed32.One, Fixed32.Zero).Error, Is.EqualTo(Fixed32Error.DivisionByZero));
            Assert.That(Fixed32.Divide(Fixed32.MaxValue, Fixed32.FromRaw(1)).Error, Is.EqualTo(Fixed32Error.Overflow));
        }

        [Test]
        public void NegateAndAbs_MinimumOverflowIsExplicit()
        {
            Assert.That(Fixed32.Negate(Fixed32.FromRatio(3, 2).Value).Value.RawValue, Is.EqualTo(-98304));
            Assert.That(Fixed32.Abs(Fixed32.FromRatio(-3, 2).Value).Value.RawValue, Is.EqualTo(98304));
            Assert.That(Fixed32.Negate(Fixed32.MinValue).Error, Is.EqualTo(Fixed32Error.Overflow));
            Assert.That(Fixed32.Abs(Fixed32.MinValue).Error, Is.EqualTo(Fixed32Error.Overflow));
        }

        [TestCase(98304, 1, 1, 2)]
        [TestCase(-98304, -1, -2, -1)]
        [TestCase(65536, 1, 1, 1)]
        [TestCase(-65536, -1, -1, -1)]
        public void IntegerConversions_HaveExplicitDirections(int raw, int truncate, int floor, int ceiling)
        {
            var value = Fixed32.FromRaw(raw);
            Assert.That(value.TruncateToInt32(), Is.EqualTo(truncate));
            Assert.That(value.FloorToInt32(), Is.EqualTo(floor));
            Assert.That(value.CeilingToInt32(), Is.EqualTo(ceiling));
        }

        [Test]
        public void GoldenSequence_ProducesExpectedRawValue()
        {
            var current = Fixed32.FromRatio(3, 2).Value;
            current = Fixed32.Add(current, Fixed32.FromRatio(-1, 4).Value).Value;
            current = Fixed32.Multiply(current, Fixed32.FromInt32(2).Value).Value;
            current = Fixed32.Divide(current, Fixed32.FromInt32(4).Value).Value;
            Assert.That(current.RawValue, Is.EqualTo(40960));
            Assert.That(current.ToDouble(), Is.EqualTo(0.625d));
            Assert.That(current.ToString(), Is.EqualTo("0.625"));
        }

        [Test]
        public void EqualityOrderingAndHashCode_UseRawValue()
        {
            var a = Fixed32.FromRaw(42);
            var b = Fixed32.FromRaw(42);
            var c = Fixed32.FromRaw(43);
            Assert.That(a == b, Is.True);
            Assert.That(a != c, Is.True);
            Assert.That(a < c && c > a && a <= b && b >= a, Is.True);
            Assert.That(a.CompareTo(c), Is.LessThan(0));
            Assert.That(a.GetHashCode(), Is.EqualTo(42));
        }

        [Test]
        public void PublicRuntimeSurface_ContainsExactlyThreeTypes()
        {
            var types = typeof(Fixed32).Assembly.GetExportedTypes().OrderBy(value => value.FullName).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                typeof(Fixed32),
                typeof(Fixed32Error),
                typeof(Fixed32Result)
            }.OrderBy(value => value.FullName).ToArray(), types);
        }

        [Test]
        public void ArithmeticOperators_AreNotPublic()
        {
            var names = typeof(Fixed32).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Select(value => value.Name).ToArray();
            Assert.That(names, Does.Not.Contain("op_Addition"));
            Assert.That(names, Does.Not.Contain("op_Subtraction"));
            Assert.That(names, Does.Not.Contain("op_Multiply"));
            Assert.That(names, Does.Not.Contain("op_Division"));
        }
    }
}
