// SPDX-License-Identifier: MIT

using System;
using NUnit.Framework;

namespace Haptics.Editor.Tests
{
    /// <summary>HapticsStepの値semanticsと検証境界を確認する。</summary>
    internal sealed class HapticsStepTests
    {
        [Test]
        public void Constructor_AcceptsBoundaryValues()
        {
            Assert.That(
                () => new HapticsStep(HapticsStep.MinDurationMilliseconds, 0f),
                Throws.Nothing);
            Assert.That(
                () => new HapticsStep(HapticsStep.MaxDurationMilliseconds, 1f),
                Throws.Nothing);
            Assert.That(
                () => new HapticsStep(30, 0.5f),
                Throws.Nothing);
        }

        [Test]
        public void Constructor_RejectsInvalidDurations()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HapticsStep(0, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HapticsStep(-1, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new HapticsStep(HapticsStep.MaxDurationMilliseconds + 1, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HapticsStep(int.MinValue, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HapticsStep(int.MaxValue, 1f));
        }

        [Test]
        public void Constructor_RejectsInvalidAmplitudes()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HapticsStep(10, -0.001f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HapticsStep(10, 1.001f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HapticsStep(10, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HapticsStep(10, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HapticsStep(10, float.NegativeInfinity));
        }

        [Test]
        public void ValueSemantics_CompareEveryField()
        {
            var value = new HapticsStep(30, 0.5f);
            var equal = new HapticsStep(30, 0.5f);
            var differences = new[]
            {
                new HapticsStep(31, 0.5f),
                new HapticsStep(30, 0.51f),
            };

            Assert.That(value.DurationMilliseconds, Is.EqualTo(30));
            Assert.That(value.Amplitude, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(value.Equals(equal), Is.True);
            Assert.That(value.Equals((object)equal), Is.True);
            Assert.That(value == equal, Is.True);
            Assert.That(value != equal, Is.False);
            Assert.That(value.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            for (var index = 0; index < differences.Length; index++)
            {
                Assert.That(value != differences[index], Is.True, $"Difference {index} was ignored.");
                Assert.That(value.Equals(differences[index]), Is.False);
            }
        }

        [Test]
        public void ToString_ContainsDurationAndAmplitude()
        {
            var text = new HapticsStep(30, 0.5f).ToString();

            Assert.That(text, Does.Contain("30"));
            Assert.That(text, Does.Contain("0.5"));
        }
    }
}
