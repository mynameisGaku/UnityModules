// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Haptics.Editor.Tests
{
    /// <summary>HapticsPatternの検証、不変性、presetの妥当性を確認する。</summary>
    internal sealed class HapticsPatternTests
    {
        [Test]
        public void Constructor_AcceptsValidStepsAndCopiesDefensively()
        {
            var source = new[]
            {
                new HapticsStep(10, 1f),
                new HapticsStep(20, 0f),
                new HapticsStep(30, 0.5f),
            };
            var pattern = new HapticsPattern(source);

            Assert.That(pattern.Steps.Count, Is.EqualTo(3));
            Assert.That(pattern.TotalDurationMilliseconds, Is.EqualTo(60));
            source[0] = new HapticsStep(1000, 0.25f);
            Assert.That(pattern.Steps[0].DurationMilliseconds, Is.EqualTo(10));
        }

        [Test]
        public void Steps_CannotBeMutatedThroughProperty()
        {
            var pattern = new HapticsPattern(new HapticsStep(10, 0.5f));

            Assert.That(pattern.Steps, Is.InstanceOf<IReadOnlyList<HapticsStep>>());
            Assert.That(
                pattern.Steps,
                Is.Not.AssignableTo<HapticsStep[]>(),
                "Stepsは外部から書き換え可能な配列として公開してはいけない。");
        }

        [Test]
        public void Constructor_RejectsNullAndEmptyAndTooLong()
        {
            Assert.Throws<ArgumentNullException>(
                () => new HapticsPattern((HapticsStep[])null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HapticsPattern());
            Assert.Throws<ArgumentOutOfRangeException>(() => new HapticsPattern(new HapticsStep[0]));

            var tooMany = new HapticsStep[HapticsPattern.MaxStepCount + 1];
            for (var index = 0; index < tooMany.Length; index++)
            {
                tooMany[index] = new HapticsStep(10, 0.5f);
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => new HapticsPattern(tooMany));
            var boundarySteps = new HapticsStep[HapticsPattern.MaxStepCount];
            for (var index = 0; index < boundarySteps.Length; index++)
            {
                boundarySteps[index] = new HapticsStep(10, 0.5f);
            }

            Assert.That(() => new HapticsPattern(boundarySteps), Throws.Nothing);
        }

        [Test]
        public void ValidateSteps_ClassifiesEveryError()
        {
            Assert.That(
                HapticsPattern.ValidateSteps(null, out var nullError), Is.False);
            Assert.That(nullError, Is.EqualTo(HapticsError.NullPattern));

            Assert.That(
                HapticsPattern.ValidateSteps(new HapticsStep[0], out var emptyError), Is.False);
            Assert.That(emptyError, Is.EqualTo(HapticsError.EmptyPattern));

            var tooLong = new HapticsStep[HapticsPattern.MaxStepCount + 1];
            for (var index = 0; index < tooLong.Length; index++)
            {
                tooLong[index] = new HapticsStep(10, 0.5f);
            }
            Assert.That(HapticsPattern.ValidateSteps(tooLong, out var tooLongError), Is.False);
            Assert.That(tooLongError, Is.EqualTo(HapticsError.PatternTooLong));

            Assert.Throws<ArgumentOutOfRangeException>(() => new HapticsStep(0, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HapticsStep(5001, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HapticsStep(10, float.NaN));

            var zeroDuration = new HapticsStep[2];
            Assert.That(
                HapticsPattern.ValidateSteps(zeroDuration, out var durationError), Is.False);
            Assert.That(durationError, Is.EqualTo(HapticsError.InvalidDuration));

            var valid = new[] { new HapticsStep(1, 0f), new HapticsStep(5000, 1f) };
            Assert.That(HapticsPattern.ValidateSteps(valid, out var noneError), Is.True);
            Assert.That(noneError, Is.EqualTo(HapticsError.None));
        }

        [Test]
        public void TryValidate_ReportsNoneForConstructedPattern()
        {
            var pattern = new HapticsPattern(new HapticsStep(30, 0.5f));

            Assert.That(pattern.TryValidate(out var error), Is.True);
            Assert.That(error, Is.EqualTo(HapticsError.None));
        }

        [Test]
        public void Presets_DefineEveryIntentWithValidValues()
        {
            var intents = new[]
            {
                HapticsIntent.SelectionTick,
                HapticsIntent.ImpactLight,
                HapticsIntent.ImpactMedium,
                HapticsIntent.ImpactHeavy,
                HapticsIntent.NotificationSuccess,
                HapticsIntent.NotificationWarning,
                HapticsIntent.NotificationError,
            };

            foreach (var intent in intents)
            {
                var pattern = HapticsPattern.Presets.Get(intent);
                Assert.That(pattern, Is.Not.Null, $"{intent} preset was missing.");
                Assert.That(pattern.TryValidate(out _), Is.True, $"{intent} preset was invalid.");
                Assert.That(
                    pattern.Steps.Count,
                    Is.InRange(1, HapticsPattern.MaxStepCount),
                    $"{intent} preset step count out of range.");
                Assert.That(
                    pattern.TotalDurationMilliseconds,
                    Is.GreaterThan(0),
                    $"{intent} preset total duration must be positive.");
            }
        }

        [Test]
        public void Presets_ImpactLightIsShortHighAmplitude()
        {
            var light = HapticsPattern.Presets.Get(HapticsIntent.ImpactLight);

            Assert.That(light.Steps.Count, Is.EqualTo(1));
            Assert.That(light.Steps[0].DurationMilliseconds, Is.LessThanOrEqualTo(20));
            Assert.That(light.Steps[0].Amplitude, Is.GreaterThan(0.5f));
        }

        [Test]
        public void Presets_GetRejectsUndefinedIntent()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => HapticsPattern.Presets.Get((HapticsIntent)999));
        }

        [Test]
        public void Presets_ReturnDeterministicInstances()
        {
            Assert.That(
                HapticsPattern.Presets.Get(HapticsIntent.SelectionTick),
                Is.SameAs(HapticsPattern.Presets.Get(HapticsIntent.SelectionTick)));
        }
    }
}
