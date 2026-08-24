// SPDX-License-Identifier: MIT

using NUnit.Framework;
using UnityEngine;

namespace PlayerOptions.Editor.Tests
{
    /// <summary>公開readonly valueとresult flagsの値semanticsを確認する。</summary>
    internal sealed class PlayerOptionsValueTests
    {
        [Test]
        public void DisplayOptions_ExposeEveryValueAndCompareEveryField()
        {
            var refreshRate = PlayerOptionsTestData.CreateRefreshRate(60000, 1000);
            var value = new PlayerDisplayOptions(
                2560,
                1440,
                FullScreenMode.FullScreenWindow,
                refreshRate);
            var equal = new PlayerDisplayOptions(
                2560,
                1440,
                FullScreenMode.FullScreenWindow,
                refreshRate);
            var differences = new[]
            {
                new PlayerDisplayOptions(1920, 1440, FullScreenMode.FullScreenWindow, refreshRate),
                new PlayerDisplayOptions(2560, 1080, FullScreenMode.FullScreenWindow, refreshRate),
                new PlayerDisplayOptions(2560, 1440, FullScreenMode.Windowed, refreshRate),
                new PlayerDisplayOptions(
                    2560,
                    1440,
                    FullScreenMode.FullScreenWindow,
                    PlayerOptionsTestData.CreateRefreshRate(120, 1)),
            };

            Assert.That(value.Width, Is.EqualTo(2560));
            Assert.That(value.Height, Is.EqualTo(1440));
            Assert.That(value.FullScreenMode, Is.EqualTo(FullScreenMode.FullScreenWindow));
            Assert.That(value.PreferredRefreshRate, Is.EqualTo(refreshRate));
            Assert.That(value.Equals(equal), Is.True);
            Assert.That(value.Equals((object)equal), Is.True);
            Assert.That(value == equal, Is.True);
            Assert.That(value != equal, Is.False);
            Assert.That(value.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            for (var index = 0; index < differences.Length; index++)
            {
                Assert.That(value != differences[index], Is.True, $"Difference {index} was ignored.");
            }
        }

        [Test]
        public void QualityOptions_NormalizeNullAndUseOrdinalIdentity()
        {
            var value = new PlayerQualityOptions(2, "Ultra");
            var equal = new PlayerQualityOptions(2, "Ultra");

            Assert.That(value.LevelIndex, Is.EqualTo(2));
            Assert.That(value.LevelName, Is.EqualTo("Ultra"));
            Assert.That(new PlayerQualityOptions(2, null).LevelName, Is.Empty);
            Assert.That(value, Is.EqualTo(equal));
            Assert.That(value == equal, Is.True);
            Assert.That(value != new PlayerQualityOptions(1, "Ultra"), Is.True);
            Assert.That(value != new PlayerQualityOptions(2, "ultra"), Is.True);
            Assert.That(value.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
        }

        [Test]
        public void State_ExposesValuesAndCompareEveryField()
        {
            var value = PlayerOptionsTestData.CreateDefaultState();
            var equal = PlayerOptionsTestData.CreateDefaultState();
            var differences = new[]
            {
                PlayerOptionsTestData.CreateState(width: 1280),
                PlayerOptionsTestData.CreateState(targetFrameRate: 90),
                PlayerOptionsTestData.CreateState(masterVolume: 0.5f),
                PlayerOptionsTestData.CreateState(qualityIndex: 2, qualityName: "Ultra"),
            };

            Assert.That(value.Display.Width, Is.EqualTo(1920));
            Assert.That(value.TargetFrameRate, Is.EqualTo(60));
            Assert.That(value.MasterVolume, Is.EqualTo(0.75f));
            Assert.That(value.Quality, Is.EqualTo(new PlayerQualityOptions(1, "High")));
            Assert.That(value.Equals(equal), Is.True);
            Assert.That(value.Equals((object)equal), Is.True);
            Assert.That(value == equal, Is.True);
            Assert.That(value.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            for (var index = 0; index < differences.Length; index++)
            {
                Assert.That(value != differences[index], Is.True, $"Difference {index} was ignored.");
            }
        }

        [Test]
        public void Result_SuccessExposesAllFlags()
        {
            var state = PlayerOptionsTestData.CreateDefaultState();
            var result = PlayerOptionsResult.Success(
                state,
                "adjusted",
                PlayerOptionsWarning.DisplayFallbackUsed | PlayerOptionsWarning.RefreshRateNormalized,
                usedDefaults: true,
                wasAdjusted: true,
                requiresSave: true);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Error, Is.EqualTo(PlayerOptionsError.None));
            Assert.That(result.Message, Is.EqualTo("adjusted"));
            Assert.That(result.State, Is.EqualTo(state));
            Assert.That(result.Warnings, Is.EqualTo(
                PlayerOptionsWarning.DisplayFallbackUsed |
                PlayerOptionsWarning.RefreshRateNormalized));
            Assert.That(result.UsedDefaults, Is.True);
            Assert.That(result.WasAdjusted, Is.True);
            Assert.That(result.RequiresSave, Is.True);
            PlayerOptionsResultAssertions.AssertFields(result, "AffectedFields");
            PlayerOptionsResultAssertions.AssertFields(result, "RollbackFailedFields");
            PlayerOptionsResultAssertions.AssertFields(result, "OutcomeUnknownFields");
        }

        [Test]
        public void Result_FailureClearsWarningsAndAdjustmentFlags()
        {
            var state = PlayerOptionsTestData.CreateDefaultState();
            var result = PlayerOptionsResult.Failure(
                state,
                PlayerOptionsError.CorruptData,
                null);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(PlayerOptionsError.CorruptData));
            Assert.That(result.Message, Is.Empty);
            Assert.That(result.State, Is.EqualTo(state));
            Assert.That(result.Warnings, Is.EqualTo(PlayerOptionsWarning.None));
            Assert.That(result.UsedDefaults, Is.False);
            Assert.That(result.WasAdjusted, Is.False);
            Assert.That(result.RequiresSave, Is.False);
            PlayerOptionsResultAssertions.AssertFields(result, "AffectedFields");
            PlayerOptionsResultAssertions.AssertFields(result, "RollbackFailedFields");
            PlayerOptionsResultAssertions.AssertFields(result, "OutcomeUnknownFields");
        }

        [Test]
        public void PlayerPrefsStorage_RejectsInvalidKeysAndExposesValidKey()
        {
            const string validKey = "com.studiogaku.player-options.tests.value";

            Assert.That(
                () => new PlayerPrefsPlayerOptionsStorage(null),
                Throws.TypeOf<System.ArgumentException>());
            Assert.That(
                () => new PlayerPrefsPlayerOptionsStorage("   "),
                Throws.TypeOf<System.ArgumentException>());
            Assert.That(
                () => new PlayerPrefsPlayerOptionsStorage(new string('x', 257)),
                Throws.TypeOf<System.ArgumentException>());
            Assert.That(
                new PlayerPrefsPlayerOptionsStorage(validKey).Key,
                Is.EqualTo(validKey));
        }
    }
}
