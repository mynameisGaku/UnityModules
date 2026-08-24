// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PlayerOptions.Runtime.Tests
{
    /// <summary>実Unity global optionをcaptureし、必ずfinallyで元値へ戻すintegration test。</summary>
    [Parallelizable(ParallelScope.None)]
    public sealed class UnityPlayerOptionsRuntimeTests
    {
        private const int MaximumResolutionRestoreFrames = 120;

        private bool _displayRestoreRequired;
        private int _originalWidth;
        private int _originalHeight;
        private FullScreenMode _originalMode;
        private RefreshRate _originalRefreshRate;

        [UnityTearDown]
        public IEnumerator RestoreDisplayAfterEachTest()
        {
            if (!_displayRestoreRequired) yield break;

            RestoreResolution(
                _originalWidth,
                _originalHeight,
                _originalMode,
                _originalRefreshRate);
            for (var frame = 0; frame < MaximumResolutionRestoreFrames; frame++)
            {
                yield return null;
                if (DisplayMatchesOriginal())
                {
                    _displayRestoreRequired = false;
                    yield break;
                }
            }

            Assert.Fail(
                $"displayを{MaximumResolutionRestoreFrames} frame以内に元へ戻せませんでした。" +
                $" expected={_originalWidth}x{_originalHeight} {_originalMode} " +
                $"{_originalRefreshRate.numerator}/{_originalRefreshRate.denominator}," +
                $" actual={Screen.width}x{Screen.height} {Screen.fullScreenMode} " +
                $"{Screen.currentResolution.refreshRateRatio.numerator}/" +
                $"{Screen.currentResolution.refreshRateRatio.denominator}");
        }

        [UnityTest]
        public IEnumerator CreateDefault_CapturesActualRuntimeAndExactApplyIsNoOp()
        {
            yield return null;

            var key = CreateUniqueKey("capture");
            try
            {
                var service = PlayerOptionsService.CreateDefault(key);
                var defaults = service.Defaults;
                var qualityNames = QualitySettings.names;
                var qualityIndex = QualitySettings.GetQualityLevel();

                Assert.That(defaults.Display.Width, Is.EqualTo(Screen.width));
                Assert.That(defaults.Display.Height, Is.EqualTo(Screen.height));
                Assert.That(defaults.Display.FullScreenMode, Is.EqualTo(Screen.fullScreenMode));
                Assert.That(defaults.TargetFrameRate, Is.EqualTo(Application.targetFrameRate));
                Assert.That(defaults.MasterVolume, Is.EqualTo(AudioListener.volume));
                Assert.That(defaults.Quality.LevelIndex, Is.EqualTo(qualityIndex));
                Assert.That(defaults.Quality.LevelName, Is.EqualTo(qualityNames[qualityIndex]));

                var result = service.Apply();

                Assert.That(result.IsSuccess, Is.True, result.Message);
                Assert.That(
                    (result.Warnings & PlayerOptionsWarning.ResolutionChangeDeferred) == 0,
                    Is.True);
                Assert.That(result.AffectedFields, Is.EqualTo(PlayerOptionsField.None));
                Assert.That(result.RollbackFailedFields, Is.EqualTo(PlayerOptionsField.None));
                Assert.That(result.OutcomeUnknownFields, Is.EqualTo(PlayerOptionsField.None));
                Assert.That(PlayerPrefs.HasKey(key), Is.False);
            }
            finally
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        [UnityTest]
        public IEnumerator Apply_ChangesSafeSynchronousValuesAndFinallyRestoresThem()
        {
            yield return null;

            var key = CreateUniqueKey("sync");
            var originalQuality = QualitySettings.GetQualityLevel();
            var originalTargetFrameRate = Application.targetFrameRate;
            var originalMasterVolume = AudioListener.volume;
            try
            {
                var qualityNames = QualitySettings.names;
                Assert.That(qualityNames, Is.Not.Null.And.Not.Empty);
                Assert.That(originalQuality, Is.InRange(0, qualityNames.Length - 1));
                Assert.That(
                    originalTargetFrameRate == -1 || originalTargetFrameRate > 0,
                    Is.True,
                    "Unity targetFrameRate must satisfy the public state contract.");
                Assert.That(originalMasterVolume, Is.InRange(0f, 1f));

                var service = PlayerOptionsService.CreateDefault(key);
                var desiredQuality = qualityNames.Length > 1
                    ? (originalQuality + 1) % qualityNames.Length
                    : originalQuality;
                var desiredTargetFrameRate = originalTargetFrameRate == 60 ? 59 : 60;
                var desiredMasterVolume = originalMasterVolume > 0.5f ? 0.4f : 0.6f;
                var desired = new PlayerOptionsState(
                    service.Defaults.Display,
                    desiredTargetFrameRate,
                    desiredMasterVolume,
                    new PlayerQualityOptions(
                        desiredQuality,
                        qualityNames[desiredQuality]));

                var set = service.SetState(desired);
                Assert.That(set.IsSuccess, Is.True, set.Message);
                var applied = service.Apply();

                Assert.That(applied.IsSuccess, Is.True, applied.Message);
                Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(desiredQuality));
                Assert.That(Application.targetFrameRate, Is.EqualTo(desiredTargetFrameRate));
                Assert.That(AudioListener.volume, Is.EqualTo(desiredMasterVolume).Within(0.0001f));
                Assert.That(
                    (applied.Warnings & PlayerOptionsWarning.ResolutionChangeDeferred) == 0,
                    Is.True);
                var expectedFields = PlayerOptionsField.TargetFrameRate |
                                     PlayerOptionsField.MasterVolume;
                if (desiredQuality != originalQuality)
                {
                    expectedFields |= PlayerOptionsField.Quality;
                }

                Assert.That(applied.AffectedFields, Is.EqualTo(expectedFields));
                Assert.That(applied.RollbackFailedFields, Is.EqualTo(PlayerOptionsField.None));
                Assert.That(applied.OutcomeUnknownFields, Is.EqualTo(PlayerOptionsField.None));
                Assert.That(PlayerPrefs.HasKey(key), Is.False);
            }
            finally
            {
                try
                {
                    AudioListener.volume = originalMasterVolume;
                }
                finally
                {
                    try
                    {
                        Application.targetFrameRate = originalTargetFrameRate;
                    }
                    finally
                    {
                        try
                        {
                            QualitySettings.SetQualityLevel(originalQuality, true);
                        }
                        finally
                        {
                            PlayerPrefs.DeleteKey(key);
                            PlayerPrefs.Save();
                        }
                    }
                }
            }

            yield return null;
            Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(originalQuality));
            Assert.That(Application.targetFrameRate, Is.EqualTo(originalTargetFrameRate));
            Assert.That(AudioListener.volume, Is.EqualTo(originalMasterVolume).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator Apply_DisplayDifferenceIsDeferredAndRestored()
        {
            yield return null;

            var key = CreateUniqueKey("resolution");
            var originalWidth = Screen.width;
            var originalHeight = Screen.height;
            var originalMode = Screen.fullScreenMode;
            var originalRefreshRate = Screen.currentResolution.refreshRateRatio;
            _displayRestoreRequired = true;
            _originalWidth = originalWidth;
            _originalHeight = originalHeight;
            _originalMode = originalMode;
            _originalRefreshRate = originalRefreshRate;
            try
            {
                var service = PlayerOptionsService.CreateDefault(key);
                var desiredMode = originalMode == FullScreenMode.Windowed
                    ? FullScreenMode.FullScreenWindow
                    : FullScreenMode.Windowed;
                var desiredDisplay = new PlayerDisplayOptions(
                    originalWidth,
                    originalHeight,
                    desiredMode,
                    originalRefreshRate);
                var desired = new PlayerOptionsState(
                    desiredDisplay,
                    service.State.TargetFrameRate,
                    service.State.MasterVolume,
                    service.State.Quality);
                var set = service.SetState(desired);
                Assert.That(set.IsSuccess, Is.True, set.Message);

                var applied = service.Apply();

                Assert.That(applied.IsSuccess, Is.True, applied.Message);
                Assert.That(
                    (applied.Warnings & PlayerOptionsWarning.ResolutionChangeDeferred) != 0,
                    Is.True);
                Assert.That(applied.AffectedFields, Is.EqualTo(PlayerOptionsField.Display));
                Assert.That(applied.RollbackFailedFields, Is.EqualTo(PlayerOptionsField.None));
                Assert.That(applied.OutcomeUnknownFields, Is.EqualTo(PlayerOptionsField.None));
            }
            finally
            {
                try
                {
                    RestoreResolution(
                        originalWidth,
                        originalHeight,
                        originalMode,
                        originalRefreshRate);
                }
                finally
                {
                    PlayerPrefs.DeleteKey(key);
                    PlayerPrefs.Save();
                }
            }

            yield return null;
        }

        private static string CreateUniqueKey(string purpose)
        {
            return $"com.studiogaku.player-options.tests.{purpose}.{Guid.NewGuid():N}";
        }

        private static void RestoreResolution(
            int width,
            int height,
            FullScreenMode mode,
            RefreshRate refreshRate)
        {
            if (refreshRate.numerator > 0 && refreshRate.denominator > 0)
            {
                Screen.SetResolution(width, height, mode, refreshRate);
                return;
            }

            Screen.SetResolution(width, height, mode);
        }

        private static bool RefreshRatesEqual(RefreshRate left, RefreshRate right)
        {
            if (left.numerator == 0 ||
                left.denominator == 0 ||
                right.numerator == 0 ||
                right.denominator == 0)
            {
                return false;
            }

            return (ulong)left.numerator * right.denominator ==
                   (ulong)right.numerator * left.denominator;
        }

        private bool DisplayMatchesOriginal()
        {
            var currentRefreshRate = Screen.currentResolution.refreshRateRatio;
            var refreshMatches = (_originalRefreshRate.numerator == 0 &&
                                  _originalRefreshRate.denominator == 0 &&
                                  currentRefreshRate.numerator == 0 &&
                                  currentRefreshRate.denominator == 0) ||
                                 RefreshRatesEqual(_originalRefreshRate, currentRefreshRate);
            return Screen.width == _originalWidth &&
                   Screen.height == _originalHeight &&
                   Screen.fullScreenMode == _originalMode &&
                   refreshMatches;
        }
    }
}
