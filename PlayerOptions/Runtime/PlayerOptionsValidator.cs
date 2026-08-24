// SPDX-License-Identifier: MIT

using System;
using UnityEngine;

namespace PlayerOptions
{
    /// <summary>option値の形と現在runtimeでの利用可否を副作用なしで検証する。</summary>
    internal static class PlayerOptionsValidator
    {
        /// <summary>厳格な変更要求を検証し、refresh rateだけを等価な既約分数へ正規化する。</summary>
        internal static bool TryNormalizeStrict(
            PlayerOptionsState state,
            IPlayerOptionsRuntime runtime,
            out PlayerOptionsState normalized,
            out PlayerOptionsWarning warnings,
            out string message)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));

            if (!TryNormalizeShape(state, out normalized, out warnings, out message))
            {
                return false;
            }

            var capabilities = RuntimeCapabilities.Capture(runtime);
            if (!IsDisplaySupported(normalized.Display, capabilities))
            {
                message = "表示設定は現在のdisplayで利用できません。";
                return false;
            }

            if (!IsExactQuality(normalized.Quality, capabilities.QualityNames))
            {
                message = "品質levelのindexと一意な名前が現在projectに一致しません。";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 保存値を検証し、環境依存値だけを一意な品質名またはtyped defaultへ補正する。
        /// 形が不正な保存値はfallbackせずCorruptDataとして返す。
        /// </summary>
        internal static bool TryNormalizeLoaded(
            PlayerOptionsState state,
            PlayerOptionsState defaults,
            IPlayerOptionsRuntime runtime,
            out PlayerOptionsState normalized,
            out PlayerOptionsWarning warnings,
            out bool usedDefaults,
            out bool wasAdjusted,
            out PlayerOptionsError error,
            out string message)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));

            usedDefaults = false;
            error = PlayerOptionsError.None;
            if (!TryNormalizeShape(state, out normalized, out warnings, out message))
            {
                wasAdjusted = false;
                error = PlayerOptionsError.CorruptData;
                return false;
            }

            wasAdjusted = warnings != PlayerOptionsWarning.None;
            if (!TryNormalizeShape(defaults, out var normalizedDefaults, out _, out var defaultMessage))
            {
                error = PlayerOptionsError.RuntimeUnavailable;
                message = $"typed defaultが不正です: {defaultMessage}";
                return false;
            }

            var capabilities = RuntimeCapabilities.Capture(runtime);
            if (!IsDisplaySupported(normalizedDefaults.Display, capabilities) ||
                !IsExactQuality(normalizedDefaults.Quality, capabilities.QualityNames))
            {
                error = PlayerOptionsError.RuntimeUnavailable;
                message = "typed defaultは現在runtimeで利用できません。";
                return false;
            }

            var display = normalized.Display;
            if (!IsDisplaySupported(display, capabilities))
            {
                display = normalizedDefaults.Display;
                warnings |= PlayerOptionsWarning.DisplayFallbackUsed;
                usedDefaults = true;
                wasAdjusted = true;
            }

            var quality = normalized.Quality;
            if (!IsExactQuality(quality, capabilities.QualityNames))
            {
                var matchingIndex = FindUniqueQualityIndex(quality.LevelName, capabilities.QualityNames);
                if (matchingIndex >= 0)
                {
                    quality = new PlayerQualityOptions(matchingIndex, capabilities.QualityNames[matchingIndex]);
                    warnings |= PlayerOptionsWarning.QualityIndexAdjusted;
                }
                else
                {
                    quality = normalizedDefaults.Quality;
                    warnings |= PlayerOptionsWarning.QualityFallbackUsed;
                    usedDefaults = true;
                }

                wasAdjusted = true;
            }

            normalized = new PlayerOptionsState(
                display,
                normalized.TargetFrameRate,
                normalized.MasterVolume,
                quality);
            message = string.Empty;
            return true;
        }

        /// <summary>runtime非依存の範囲、enum、有限値、refresh rate表現を検証する。</summary>
        internal static bool TryNormalizeShape(
            PlayerOptionsState state,
            out PlayerOptionsState normalized,
            out PlayerOptionsWarning warnings,
            out string message)
        {
            normalized = state;
            warnings = PlayerOptionsWarning.None;
            message = string.Empty;

            if (state.Display.Width <= 0 || state.Display.Height <= 0)
            {
                message = "画面幅と高さは正数にしてください。";
                return false;
            }

            if (!Enum.IsDefined(typeof(FullScreenMode), state.Display.FullScreenMode))
            {
                message = "全画面表示方式が不正です。";
                return false;
            }

            if (!TryNormalizeRefreshRate(
                    state.Display.PreferredRefreshRate,
                    out var refreshRate,
                    out var refreshAdjusted))
            {
                message = "refresh rateは0/0、または正の分子と分母の組にしてください。";
                return false;
            }

            if (state.TargetFrameRate != -1 && state.TargetFrameRate <= 0)
            {
                message = "target frame rateは-1または正数にしてください。";
                return false;
            }

            if (float.IsNaN(state.MasterVolume) ||
                float.IsInfinity(state.MasterVolume) ||
                state.MasterVolume < 0f ||
                state.MasterVolume > 1f)
            {
                message = "master volumeは0以上1以下の有限値にしてください。";
                return false;
            }

            if (state.Quality.LevelIndex < 0 || string.IsNullOrEmpty(state.Quality.LevelName))
            {
                message = "品質levelには0以上のindexと空でない名前が必要です。";
                return false;
            }

            if (refreshAdjusted)
            {
                warnings |= PlayerOptionsWarning.RefreshRateNormalized;
                var display = new PlayerDisplayOptions(
                    state.Display.Width,
                    state.Display.Height,
                    state.Display.FullScreenMode,
                    refreshRate);
                normalized = new PlayerOptionsState(
                    display,
                    state.TargetFrameRate,
                    state.MasterVolume,
                    state.Quality);
            }

            return true;
        }

        private static bool TryNormalizeRefreshRate(
            RefreshRate refreshRate,
            out RefreshRate normalized,
            out bool wasAdjusted)
        {
            normalized = refreshRate;
            wasAdjusted = false;
            if (refreshRate.numerator == 0 && refreshRate.denominator == 0)
            {
                return true;
            }

            if (refreshRate.numerator == 0 || refreshRate.denominator == 0)
            {
                return false;
            }

            var divisor = GreatestCommonDivisor(refreshRate.numerator, refreshRate.denominator);
            if (divisor <= 1) return true;

            normalized = new RefreshRate
            {
                numerator = refreshRate.numerator / divisor,
                denominator = refreshRate.denominator / divisor,
            };
            wasAdjusted = true;
            return true;
        }

        private static uint GreatestCommonDivisor(uint left, uint right)
        {
            while (right != 0)
            {
                var remainder = left % right;
                left = right;
                right = remainder;
            }

            return left;
        }

        private static bool IsDisplaySupported(PlayerDisplayOptions display, RuntimeCapabilities capabilities)
        {
            if (display.FullScreenMode != FullScreenMode.ExclusiveFullScreen)
            {
                return true;
            }

            var resolutions = capabilities.Resolutions;
            if (resolutions == null) return false;

            for (var index = 0; index < resolutions.Length; index++)
            {
                var resolution = resolutions[index];
                if (resolution.width != display.Width || resolution.height != display.Height)
                {
                    continue;
                }

                if (!HasSpecifiedRefreshRate(display.PreferredRefreshRate) ||
                    RefreshRatesEqual(display.PreferredRefreshRate, resolution.refreshRateRatio))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsExactQuality(PlayerQualityOptions quality, string[] qualityNames)
        {
            if (qualityNames == null ||
                quality.LevelIndex < 0 ||
                quality.LevelIndex >= qualityNames.Length ||
                !string.Equals(
                    quality.LevelName,
                    qualityNames[quality.LevelIndex],
                    StringComparison.Ordinal))
            {
                return false;
            }

            return FindUniqueQualityIndex(quality.LevelName, qualityNames) == quality.LevelIndex;
        }

        private static int FindUniqueQualityIndex(string levelName, string[] qualityNames)
        {
            if (qualityNames == null) return -1;

            var matchingIndex = -1;
            for (var index = 0; index < qualityNames.Length; index++)
            {
                if (!string.Equals(levelName, qualityNames[index], StringComparison.Ordinal)) continue;
                if (matchingIndex >= 0) return -1;
                matchingIndex = index;
            }

            return matchingIndex;
        }

        private static bool HasSpecifiedRefreshRate(RefreshRate refreshRate)
        {
            return refreshRate.numerator != 0 && refreshRate.denominator != 0;
        }

        private static bool RefreshRatesEqual(RefreshRate left, RefreshRate right)
        {
            if (!HasSpecifiedRefreshRate(left) || !HasSpecifiedRefreshRate(right)) return false;
            return (ulong)left.numerator * right.denominator ==
                   (ulong)right.numerator * left.denominator;
        }

        private readonly struct RuntimeCapabilities
        {
            private RuntimeCapabilities(
                Resolution[] resolutions,
                string[] qualityNames)
            {
                Resolutions = resolutions;
                QualityNames = qualityNames;
            }

            internal Resolution[] Resolutions { get; }

            internal string[] QualityNames { get; }

            internal static RuntimeCapabilities Capture(IPlayerOptionsRuntime runtime)
            {
                return new RuntimeCapabilities(
                    runtime.Resolutions,
                    runtime.QualityNames);
            }
        }
    }
}
