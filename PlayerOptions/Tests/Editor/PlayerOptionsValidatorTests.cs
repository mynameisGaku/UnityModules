// SPDX-License-Identifier: MIT

using System;
using NUnit.Framework;
using UnityEngine;

namespace PlayerOptions.Editor.Tests
{
    /// <summary>shape、runtime capability、quality identity、fallback規則を確認する。</summary>
    internal sealed class PlayerOptionsValidatorTests
    {
        [Test]
        public void Strict_ValidStateNormalizesEquivalentRefreshRateOnly()
        {
            var runtime = new FakePlayerOptionsRuntime();
            var state = PlayerOptionsTestData.CreateState(
                refreshNumerator: 60000,
                refreshDenominator: 1000);

            var success = PlayerOptionsValidator.TryNormalizeStrict(
                state,
                runtime,
                out var normalized,
                out var warnings,
                out var message);

            Assert.That(success, Is.True, message);
            Assert.That(normalized.Display.PreferredRefreshRate.numerator, Is.EqualTo(60));
            Assert.That(normalized.Display.PreferredRefreshRate.denominator, Is.EqualTo(1));
            Assert.That(warnings, Is.EqualTo(PlayerOptionsWarning.RefreshRateNormalized));
        }

        [TestCase(0, 1080)]
        [TestCase(-1, 1080)]
        [TestCase(1920, 0)]
        [TestCase(1920, -1)]
        public void Shape_RejectsNonPositiveDisplayDimensions(int width, int height)
        {
            var success = PlayerOptionsValidator.TryNormalizeShape(
                PlayerOptionsTestData.CreateState(width: width, height: height),
                out _,
                out _,
                out _);

            Assert.That(success, Is.False);
        }

        [Test]
        public void Shape_RejectsUndefinedModeAndHalfSpecifiedRefreshRate()
        {
            Assert.That(
                PlayerOptionsValidator.TryNormalizeShape(
                    PlayerOptionsTestData.CreateState(fullScreenMode: (FullScreenMode)999),
                    out _,
                    out _,
                    out _),
                Is.False);
            Assert.That(
                PlayerOptionsValidator.TryNormalizeShape(
                    PlayerOptionsTestData.CreateState(
                        refreshNumerator: 60,
                        refreshDenominator: 0),
                    out _,
                    out _,
                    out _),
                Is.False);
            Assert.That(
                PlayerOptionsValidator.TryNormalizeShape(
                    PlayerOptionsTestData.CreateState(
                        refreshNumerator: 0,
                        refreshDenominator: 1),
                    out _,
                    out _,
                    out _),
                Is.False);
        }

        [TestCase(0)]
        [TestCase(-2)]
        public void Shape_RejectsUnsupportedTargetFrameRate(int targetFrameRate)
        {
            Assert.That(
                PlayerOptionsValidator.TryNormalizeShape(
                    PlayerOptionsTestData.CreateState(targetFrameRate: targetFrameRate),
                    out _,
                    out _,
                    out _),
                Is.False);
        }

        [TestCase(-0.01f)]
        [TestCase(1.01f)]
        public void Shape_RejectsOutOfRangeVolume(float volume)
        {
            Assert.That(
                PlayerOptionsValidator.TryNormalizeShape(
                    PlayerOptionsTestData.CreateState(masterVolume: volume),
                    out _,
                    out _,
                    out _),
                Is.False);
        }

        [Test]
        public void Shape_RejectsNonFiniteVolumeAndMissingQualityIdentity()
        {
            var invalidVolumes = new[]
            {
                float.NaN,
                float.PositiveInfinity,
                float.NegativeInfinity,
            };
            for (var index = 0; index < invalidVolumes.Length; index++)
            {
                Assert.That(
                    PlayerOptionsValidator.TryNormalizeShape(
                        PlayerOptionsTestData.CreateState(masterVolume: invalidVolumes[index]),
                        out _,
                        out _,
                        out _),
                    Is.False);
            }

            Assert.That(
                PlayerOptionsValidator.TryNormalizeShape(
                    PlayerOptionsTestData.CreateState(qualityIndex: -1),
                    out _,
                    out _,
                    out _),
                Is.False);
            Assert.That(
                PlayerOptionsValidator.TryNormalizeShape(
                    PlayerOptionsTestData.CreateState(qualityName: null),
                    out _,
                    out _,
                    out _),
                Is.False);
        }

        [Test]
        public void Strict_RejectsQualityCaseIndexMismatchAndUnsupportedDisplay()
        {
            var runtime = new FakePlayerOptionsRuntime();

            Assert.That(
                PlayerOptionsValidator.TryNormalizeStrict(
                    PlayerOptionsTestData.CreateState(qualityName: "high"),
                    runtime,
                    out _,
                    out _,
                    out _),
                Is.False);
            Assert.That(
                PlayerOptionsValidator.TryNormalizeStrict(
                    PlayerOptionsTestData.CreateState(qualityIndex: 0, qualityName: "High"),
                    runtime,
                    out _,
                    out _,
                    out _),
                Is.False);
            Assert.That(
                PlayerOptionsValidator.TryNormalizeStrict(
                    PlayerOptionsTestData.CreateState(
                        width: 2560,
                        height: 1440,
                        fullScreenMode: FullScreenMode.ExclusiveFullScreen),
                    runtime,
                    out _,
                    out _,
                    out _),
                Is.False);
        }

        [Test]
        public void Strict_RejectsExactQualityPairWhenNameIsNotUnique()
        {
            var runtime = new FakePlayerOptionsRuntime
            {
                QualityLevelValue = 0,
                QualityNameValues = new[] { "High", "High", "Ultra" },
            };

            var success = PlayerOptionsValidator.TryNormalizeStrict(
                PlayerOptionsTestData.CreateState(qualityIndex: 0, qualityName: "High"),
                runtime,
                out _,
                out _,
                out _);

            Assert.That(success, Is.False);
        }

        [Test]
        public void Strict_AcceptsCurrentTupleAndEnumeratedResolution()
        {
            var runtime = new FakePlayerOptionsRuntime
            {
                ResolutionValues = new[]
                {
                    PlayerOptionsTestData.CreateResolution(1280, 720, 60, 1),
                },
            };

            Assert.That(
                PlayerOptionsValidator.TryNormalizeStrict(
                    PlayerOptionsTestData.CreateDefaultState(),
                    runtime,
                    out _,
                    out _,
                    out _),
                Is.True);
            Assert.That(
                PlayerOptionsValidator.TryNormalizeStrict(
                    PlayerOptionsTestData.CreateState(
                        width: 1280,
                        height: 720,
                        fullScreenMode: FullScreenMode.ExclusiveFullScreen),
                    runtime,
                    out _,
                    out _,
                    out _),
                Is.True);
        }

        [Test]
        public void Strict_WindowedTupleDoesNotRequireExclusiveResolutionEnumeration()
        {
            var runtime = new FakePlayerOptionsRuntime
            {
                ResolutionValues = Array.Empty<Resolution>(),
            };

            Assert.That(
                PlayerOptionsValidator.TryNormalizeStrict(
                    PlayerOptionsTestData.CreateState(width: 1234, height: 777),
                    runtime,
                    out _,
                    out _,
                    out _),
                Is.True);
        }

        [Test]
        public void Strict_CurrentExclusiveTupleStillRequiresResolutionEnumeration()
        {
            var runtime = new FakePlayerOptionsRuntime
            {
                FullScreenModeValue = FullScreenMode.ExclusiveFullScreen,
                ResolutionValues = Array.Empty<Resolution>(),
            };
            var state = PlayerOptionsTestData.CreateState(
                fullScreenMode: FullScreenMode.ExclusiveFullScreen);

            Assert.That(
                PlayerOptionsValidator.TryNormalizeStrict(
                    state,
                    runtime,
                    out _,
                    out _,
                    out _),
                Is.False);
        }

        [Test]
        public void LoadedQuality_ExactPairWithDuplicateNameFallsBackToUniqueDefault()
        {
            var runtime = new FakePlayerOptionsRuntime
            {
                QualityLevelValue = 2,
                QualityNameValues = new[] { "High", "High", "Ultra" },
            };
            var defaults = PlayerOptionsTestData.CreateState(qualityIndex: 2, qualityName: "Ultra");
            var loaded = PlayerOptionsTestData.CreateState(qualityIndex: 1, qualityName: "High");

            var success = PlayerOptionsValidator.TryNormalizeLoaded(
                loaded,
                defaults,
                runtime,
                out var normalized,
                out var warnings,
                out var usedDefaults,
                out var wasAdjusted,
                out var error,
                out var message);

            Assert.That(success, Is.True, message);
            Assert.That(error, Is.EqualTo(PlayerOptionsError.None));
            Assert.That(normalized.Quality, Is.EqualTo(defaults.Quality));
            Assert.That(warnings, Is.EqualTo(PlayerOptionsWarning.QualityFallbackUsed));
            Assert.That(usedDefaults, Is.True);
            Assert.That(wasAdjusted, Is.True);
        }

        [Test]
        public void LoadedQuality_ReorderedUniqueNameRepairsIndex()
        {
            var runtime = new FakePlayerOptionsRuntime
            {
                QualityLevelValue = 0,
                QualityNameValues = new[] { "High", "Low", "Ultra" },
            };
            var defaults = PlayerOptionsTestData.CreateState(qualityIndex: 0, qualityName: "High");
            var loaded = PlayerOptionsTestData.CreateState(qualityIndex: 1, qualityName: "High");

            var success = PlayerOptionsValidator.TryNormalizeLoaded(
                loaded,
                defaults,
                runtime,
                out var normalized,
                out var warnings,
                out var usedDefaults,
                out var wasAdjusted,
                out var error,
                out var message);

            Assert.That(success, Is.True, message);
            Assert.That(error, Is.EqualTo(PlayerOptionsError.None));
            Assert.That(normalized.Quality, Is.EqualTo(new PlayerQualityOptions(0, "High")));
            Assert.That(warnings, Is.EqualTo(PlayerOptionsWarning.QualityIndexAdjusted));
            Assert.That(usedDefaults, Is.False);
            Assert.That(wasAdjusted, Is.True);
        }

        [Test]
        public void LoadedQuality_DuplicateNameFallsBackToDefault()
        {
            var runtime = new FakePlayerOptionsRuntime
            {
                QualityLevelValue = 2,
                QualityNameValues = new[] { "High", "High", "Ultra" },
            };
            var defaults = PlayerOptionsTestData.CreateState(qualityIndex: 2, qualityName: "Ultra");
            var loaded = PlayerOptionsTestData.CreateState(qualityIndex: 0, qualityName: "High");

            var success = PlayerOptionsValidator.TryNormalizeLoaded(
                loaded,
                defaults,
                runtime,
                out var normalized,
                out var warnings,
                out var usedDefaults,
                out var wasAdjusted,
                out _,
                out var message);

            Assert.That(success, Is.True, message);
            Assert.That(normalized.Quality, Is.EqualTo(defaults.Quality));
            Assert.That(warnings, Is.EqualTo(PlayerOptionsWarning.QualityFallbackUsed));
            Assert.That(usedDefaults, Is.True);
            Assert.That(wasAdjusted, Is.True);
        }

        [Test]
        public void LoadedDisplay_UnsupportedTupleFallsBackToDefault()
        {
            var runtime = new FakePlayerOptionsRuntime();
            var defaults = PlayerOptionsTestData.CreateDefaultState();
            var loaded = PlayerOptionsTestData.CreateState(
                width: 2560,
                height: 1440,
                fullScreenMode: FullScreenMode.ExclusiveFullScreen);

            var success = PlayerOptionsValidator.TryNormalizeLoaded(
                loaded,
                defaults,
                runtime,
                out var normalized,
                out var warnings,
                out var usedDefaults,
                out var wasAdjusted,
                out _,
                out var message);

            Assert.That(success, Is.True, message);
            Assert.That(normalized.Display, Is.EqualTo(defaults.Display));
            Assert.That(warnings, Is.EqualTo(PlayerOptionsWarning.DisplayFallbackUsed));
            Assert.That(usedDefaults, Is.True);
            Assert.That(wasAdjusted, Is.True);
        }

        [Test]
        public void Loaded_InvalidShapeIsCorruptInsteadOfFallback()
        {
            var success = PlayerOptionsValidator.TryNormalizeLoaded(
                PlayerOptionsTestData.CreateState(targetFrameRate: 0),
                PlayerOptionsTestData.CreateDefaultState(),
                new FakePlayerOptionsRuntime(),
                out _,
                out _,
                out var usedDefaults,
                out var wasAdjusted,
                out var error,
                out _);

            Assert.That(success, Is.False);
            Assert.That(error, Is.EqualTo(PlayerOptionsError.CorruptData));
            Assert.That(usedDefaults, Is.False);
            Assert.That(wasAdjusted, Is.False);
        }

        [Test]
        public void Loaded_InvalidRuntimeDefaultIsRuntimeUnavailable()
        {
            var runtime = new FakePlayerOptionsRuntime
            {
                QualityNameValues = new[] { "Only" },
                QualityLevelValue = 0,
            };

            var success = PlayerOptionsValidator.TryNormalizeLoaded(
                PlayerOptionsTestData.CreateState(qualityIndex: 0, qualityName: "Only"),
                PlayerOptionsTestData.CreateDefaultState(),
                runtime,
                out _,
                out _,
                out _,
                out _,
                out var error,
                out _);

            Assert.That(success, Is.False);
            Assert.That(error, Is.EqualTo(PlayerOptionsError.RuntimeUnavailable));
        }

        [Test]
        public void Loaded_CombinesRefreshNormalizationAndDisplayFallbackFlags()
        {
            var loaded = PlayerOptionsTestData.CreateState(
                width: 2560,
                height: 1440,
                fullScreenMode: FullScreenMode.ExclusiveFullScreen,
                refreshNumerator: 120,
                refreshDenominator: 2);

            var success = PlayerOptionsValidator.TryNormalizeLoaded(
                loaded,
                PlayerOptionsTestData.CreateDefaultState(),
                new FakePlayerOptionsRuntime(),
                out var normalized,
                out var warnings,
                out var usedDefaults,
                out var wasAdjusted,
                out _,
                out var message);

            Assert.That(success, Is.True, message);
            Assert.That(normalized.Display, Is.EqualTo(PlayerOptionsTestData.CreateDefaultState().Display));
            Assert.That(
                warnings,
                Is.EqualTo(
                    PlayerOptionsWarning.RefreshRateNormalized |
                    PlayerOptionsWarning.DisplayFallbackUsed));
            Assert.That(usedDefaults, Is.True);
            Assert.That(wasAdjusted, Is.True);
        }
    }
}
