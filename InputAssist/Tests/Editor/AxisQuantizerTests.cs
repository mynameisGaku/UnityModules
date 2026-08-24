using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace InputQuantization.Tests
{
    public sealed class AxisQuantizerTests
    {
        [TestCase(-0.1d, 8)]
        [TestCase(1d, 8)]
        [TestCase(double.NaN, 8)]
        [TestCase(double.PositiveInfinity, 8)]
        [TestCase(0.1d, 0)]
        [TestCase(0.1d, -1)]
        [TestCase(0.1d, AxisQuantizer.MaximumStepsPerDirection + 1)]
        public void TryCreate_InvalidConfiguration_Fails(double deadZone, int steps)
        {
            Assert.That(AxisQuantizer.TryCreate(deadZone, steps, out var quantizer, out var error), Is.False);
            Assert.That(quantizer, Is.EqualTo(default(AxisQuantizer)));
            Assert.That(error, Is.EqualTo(InputQuantizationError.InvalidConfiguration));
        }

        [TestCase(0d, 1)]
        [TestCase(0.1d, 8)]
        [TestCase(0.999999d, AxisQuantizer.MaximumStepsPerDirection)]
        public void TryCreate_ValidBoundary_Succeeds(double deadZone, int steps)
        {
            Assert.That(AxisQuantizer.TryCreate(deadZone, steps, out var quantizer, out var error), Is.True);
            Assert.That(quantizer.IsValid, Is.True);
            Assert.That(quantizer.DeadZone, Is.EqualTo(deadZone));
            Assert.That(quantizer.StepsPerDirection, Is.EqualTo(steps));
            Assert.That(error, Is.EqualTo(InputQuantizationError.None));
        }

        [Test]
        public void DefaultQuantizer_ReturnsInvalidConfiguration()
        {
            var result = default(AxisQuantizer).Quantize(0d);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(InputQuantizationError.InvalidConfiguration));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void Quantize_NonFiniteInput_Fails(double input)
        {
            var result = Create(0.1d, 8).Quantize(input);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Value, Is.Zero);
            Assert.That(result.Error, Is.EqualTo(InputQuantizationError.NonFiniteInput));
        }

        [TestCase(-0.1d)]
        [TestCase(-0.05d)]
        [TestCase(0d)]
        [TestCase(0.05d)]
        [TestCase(0.1d)]
        public void Quantize_InsideInclusiveDeadZone_ReturnsZero(double input)
        {
            var result = Create(0.1d, 8).Quantize(input);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Value, Is.Zero);
            Assert.That(result.Error, Is.EqualTo(InputQuantizationError.None));
        }

        [TestCase(0.55d, 4)]
        [TestCase(-0.55d, -4)]
        [TestCase(1d, 8)]
        [TestCase(-1d, -8)]
        [TestCase(2d, 8)]
        [TestCase(-2d, -8)]
        public void Quantize_GoldenValues_AreSymmetricAndClamped(double input, short expected)
        {
            var result = Create(0.1d, 8).Quantize(input);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Value, Is.EqualTo(expected));
        }

        [Test]
        public void Quantize_ExactHalfStep_RoundsAwayFromZero()
        {
            var quantizer = Create(0d, 8);

            Assert.That(quantizer.Quantize(0.0625d).Value, Is.EqualTo(1));
            Assert.That(quantizer.Quantize(-0.0625d).Value, Is.EqualTo(-1));
        }

        [Test]
        public void Quantize_AdjacentRepresentableValues_StraddleHalfBoundaryDeterministically()
        {
            var quantizer = Create(0d, 8);
            var halfStepBits = BitConverter.DoubleToInt64Bits(0.0625d);
            var below = BitConverter.Int64BitsToDouble(halfStepBits - 1L);
            var above = BitConverter.Int64BitsToDouble(halfStepBits + 1L);

            Assert.That(quantizer.Quantize(below).Value, Is.Zero);
            Assert.That(quantizer.Quantize(above).Value, Is.EqualTo(1));
            Assert.That(quantizer.Quantize(-below).Value, Is.Zero);
            Assert.That(quantizer.Quantize(-above).Value, Is.EqualTo(-1));
        }

        [Test]
        public void Quantize_MaximumSteps_FitsSignedShort()
        {
            var quantizer = Create(0d, AxisQuantizer.MaximumStepsPerDirection);

            Assert.That(quantizer.Quantize(1d).Value, Is.EqualTo(short.MaxValue));
            Assert.That(quantizer.Quantize(-1d).Value, Is.EqualTo(-short.MaxValue));
        }

        [Test]
        public void Result_Default_IsNotSuccess()
        {
            var result = default(InputQuantizationResult);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Value, Is.Zero);
            Assert.That(result.Error, Is.EqualTo(InputQuantizationError.None));
        }

        [Test]
        public void Quantizer_EqualityAndHash_UseBothConfigurationFields()
        {
            var first = Create(0.1d, 8);
            var same = Create(0.1d, 8);
            var otherDeadZone = Create(0.2d, 8);
            var otherSteps = Create(0.1d, 16);

            Assert.That(first == same, Is.True);
            Assert.That(first != otherDeadZone, Is.True);
            Assert.That(first != otherSteps, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
        }

        [Test]
        public void PublicRuntimeSurface_ContainsExactlyThreeTypes()
        {
            var exported = typeof(AxisQuantizer).Assembly.GetExportedTypes().Where(type => string.Equals(type.Namespace, "InputQuantization", StringComparison.Ordinal)).OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();

            Assert.That(exported, Is.EqualTo(new[]
            {
                typeof(AxisQuantizer),
                typeof(InputQuantizationError),
                typeof(InputQuantizationResult)
            }.OrderBy(type => type.FullName, StringComparer.Ordinal)));
        }

        [Test]
        public void PublicRuntimeSurface_DoesNotExposeUnityEngineTypes()
        {
            var publicTypes = typeof(AxisQuantizer).Assembly.GetExportedTypes().Where(type => string.Equals(type.Namespace, "InputQuantization", StringComparison.Ordinal)).ToArray();
            var engineTypes = publicTypes.SelectMany(PublicSignatureTypes).SelectMany(ExpandSignatureType).Where(type => (type.Namespace ?? string.Empty).StartsWith("UnityEngine", StringComparison.Ordinal)).Select(type => type.FullName ?? type.Name).Distinct().OrderBy(name => name, StringComparer.Ordinal).ToArray();

            Assert.That(engineTypes, Is.Empty);
        }

        private static AxisQuantizer Create(double deadZone, int steps)
        {
            Assert.That(AxisQuantizer.TryCreate(deadZone, steps, out var quantizer, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputQuantizationError.None));
            return quantizer;
        }

        private static IEnumerable<Type> PublicSignatureTypes(Type type)
        {
            yield return type;
            if (type.BaseType != null) yield return type.BaseType;
            foreach (var interfaceType in type.GetInterfaces()) yield return interfaceType;
            foreach (var genericArgument in type.GetGenericArguments()) yield return genericArgument;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var field in type.GetFields(flags)) yield return field.FieldType;
            foreach (var property in type.GetProperties(flags))
            {
                yield return property.PropertyType;
                foreach (var parameter in property.GetIndexParameters()) yield return parameter.ParameterType;
            }
            foreach (var eventInfo in type.GetEvents(flags)) yield return eventInfo.EventHandlerType;
            foreach (var constructor in type.GetConstructors(flags))
            {
                foreach (var parameter in constructor.GetParameters()) yield return parameter.ParameterType;
            }
            foreach (var method in type.GetMethods(flags))
            {
                yield return method.ReturnType;
                foreach (var parameter in method.GetParameters()) yield return parameter.ParameterType;
                foreach (var genericArgument in method.GetGenericArguments()) yield return genericArgument;
            }
        }

        private static IEnumerable<Type> ExpandSignatureType(Type type)
        {
            if (type == null) yield break;
            yield return type;
            if (type.HasElementType)
            {
                foreach (var elementType in ExpandSignatureType(type.GetElementType())) yield return elementType;
            }
            if (type.IsGenericType)
            {
                foreach (var genericArgument in type.GetGenericArguments())
                {
                    foreach (var argumentType in ExpandSignatureType(genericArgument)) yield return argumentType;
                }
            }
            if (type.IsGenericParameter)
            {
                foreach (var constraint in type.GetGenericParameterConstraints())
                {
                    foreach (var constraintType in ExpandSignatureType(constraint)) yield return constraintType;
                }
            }
        }
    }
}
