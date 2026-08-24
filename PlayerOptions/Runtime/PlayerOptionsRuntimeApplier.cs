// SPDX-License-Identifier: MIT

using System;
using UnityEngine;

namespace PlayerOptions
{
    /// <summary>検証済みstateを安全な順序でUnity runtimeへ適用し、同期値をbest-effortで復元する。</summary>
    internal static class PlayerOptionsRuntimeApplier
    {
        /// <summary>品質、frame rate、音量、最後に画面要求の順で適用する。</summary>
        internal static bool TryApply(
            PlayerOptionsState state,
            IPlayerOptionsRuntime runtime,
            out PlayerOptionsError error,
            out PlayerOptionsWarning warnings,
            out string message)
        {
            return TryApply(
                state,
                runtime,
                out error,
                out warnings,
                out message,
                out _,
                out _,
                out _);
        }

        /// <summary>適用対象、rollback失敗、結果不明fieldを含めてUnity runtimeへ適用する。</summary>
        internal static bool TryApply(
            PlayerOptionsState state,
            IPlayerOptionsRuntime runtime,
            out PlayerOptionsError error,
            out PlayerOptionsWarning warnings,
            out string message,
            out PlayerOptionsField affectedFields,
            out PlayerOptionsField rollbackFailedFields,
            out PlayerOptionsField outcomeUnknownFields)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));

            error = PlayerOptionsError.None;
            warnings = PlayerOptionsWarning.None;
            message = string.Empty;
            affectedFields = PlayerOptionsField.None;
            rollbackFailedFields = PlayerOptionsField.None;
            outcomeUnknownFields = PlayerOptionsField.None;

            int previousQuality;
            int previousTargetFrameRate;
            float previousMasterVolume;
            bool requestResolution;
            try
            {
                previousQuality = runtime.QualityLevel;
                previousTargetFrameRate = runtime.TargetFrameRate;
                previousMasterVolume = runtime.MasterVolume;
                requestResolution = !DisplayMatchesRuntime(state.Display, runtime);
            }
            catch (Exception exception)
            {
                error = PlayerOptionsError.RuntimeUnavailable;
                message = $"適用前のUnity runtime状態を確認できませんでした: {SafeMessage(exception)}";
                return false;
            }

            var qualityTouched = false;
            var targetFrameRateTouched = false;
            var masterVolumeTouched = false;
            var resolutionCallStarted = false;
            try
            {
                if (previousQuality != state.Quality.LevelIndex)
                {
                    qualityTouched = true;
                    affectedFields |= PlayerOptionsField.Quality;
                    runtime.SetQualityLevel(state.Quality.LevelIndex);
                    if (runtime.QualityLevel != state.Quality.LevelIndex)
                    {
                        throw new InvalidOperationException("品質levelの読戻し値が要求値と一致しません。");
                    }
                }

                if (previousTargetFrameRate != state.TargetFrameRate)
                {
                    targetFrameRateTouched = true;
                    affectedFields |= PlayerOptionsField.TargetFrameRate;
                    runtime.SetTargetFrameRate(state.TargetFrameRate);
                    if (runtime.TargetFrameRate != state.TargetFrameRate)
                    {
                        throw new InvalidOperationException("target frame rateの読戻し値が要求値と一致しません。");
                    }
                }

                if (previousMasterVolume != state.MasterVolume)
                {
                    masterVolumeTouched = true;
                    affectedFields |= PlayerOptionsField.MasterVolume;
                    runtime.SetMasterVolume(state.MasterVolume);
                    if (runtime.MasterVolume != state.MasterVolume)
                    {
                        throw new InvalidOperationException("master volumeの読戻し値が要求値と一致しません。");
                    }
                }

                if (requestResolution)
                {
                    resolutionCallStarted = true;
                    affectedFields |= PlayerOptionsField.Display;
                    runtime.SetResolution(state.Display, HasSpecifiedRefreshRate(state.Display.PreferredRefreshRate));
                    warnings |= PlayerOptionsWarning.ResolutionChangeDeferred;
                }
            }
            catch (Exception exception)
            {
                if (resolutionCallStarted)
                {
                    outcomeUnknownFields |= PlayerOptionsField.Display;
                    warnings |= PlayerOptionsWarning.ResolutionOutcomeUnknown;
                }

                rollbackFailedFields = TryRollback(
                    runtime,
                    qualityTouched,
                    previousQuality,
                    targetFrameRateTouched,
                    previousTargetFrameRate,
                    masterVolumeTouched,
                    previousMasterVolume);
                error = rollbackFailedFields == PlayerOptionsField.None
                    ? PlayerOptionsError.ApplyFailed
                    : PlayerOptionsError.RollbackFailed;
                message = rollbackFailedFields == PlayerOptionsField.None
                    ? $"Unity runtimeへの適用に失敗し、同期値を復元しました: {SafeMessage(exception)}"
                    : $"Unity runtimeへの適用と同期値の完全な復元に失敗しました（未復元field: {rollbackFailedFields}）: {SafeMessage(exception)}";
                if (outcomeUnknownFields != PlayerOptionsField.None)
                {
                    message += " 画面変更要求の結果は確認できません。";
                }

                return false;
            }

            if (state.TargetFrameRate > 0 && MayOverrideTargetFrameRate(runtime))
            {
                warnings |= PlayerOptionsWarning.TargetFrameRateMayBeOverridden;
            }

            message = requestResolution
                ? "player optionを適用し、画面変更を要求しました。"
                : "player optionを適用しました。";
            return true;
        }

        private static PlayerOptionsField TryRollback(
            IPlayerOptionsRuntime runtime,
            bool qualityTouched,
            int previousQuality,
            bool targetFrameRateTouched,
            int previousTargetFrameRate,
            bool masterVolumeTouched,
            float previousMasterVolume)
        {
            var failedFields = PlayerOptionsField.None;
            if (masterVolumeTouched)
            {
                try
                {
                    runtime.SetMasterVolume(previousMasterVolume);
                    if (runtime.MasterVolume != previousMasterVolume)
                    {
                        failedFields |= PlayerOptionsField.MasterVolume;
                    }
                }
                catch (Exception)
                {
                    failedFields |= PlayerOptionsField.MasterVolume;
                }
            }

            if (targetFrameRateTouched)
            {
                try
                {
                    runtime.SetTargetFrameRate(previousTargetFrameRate);
                    if (runtime.TargetFrameRate != previousTargetFrameRate)
                    {
                        failedFields |= PlayerOptionsField.TargetFrameRate;
                    }
                }
                catch (Exception)
                {
                    failedFields |= PlayerOptionsField.TargetFrameRate;
                }
            }

            if (qualityTouched)
            {
                try
                {
                    runtime.SetQualityLevel(previousQuality);
                    if (runtime.QualityLevel != previousQuality)
                    {
                        failedFields |= PlayerOptionsField.Quality;
                    }
                }
                catch (Exception)
                {
                    failedFields |= PlayerOptionsField.Quality;
                }
            }

            return failedFields;
        }

        private static bool DisplayMatchesRuntime(PlayerDisplayOptions display, IPlayerOptionsRuntime runtime)
        {
            if (display.Width != runtime.ScreenWidth ||
                display.Height != runtime.ScreenHeight ||
                display.FullScreenMode != runtime.FullScreenMode)
            {
                return false;
            }

            return !HasSpecifiedRefreshRate(display.PreferredRefreshRate) ||
                   RefreshRatesEqual(display.PreferredRefreshRate, runtime.CurrentRefreshRate);
        }

        private static bool MayOverrideTargetFrameRate(IPlayerOptionsRuntime runtime)
        {
            try
            {
                return runtime.VSyncCount > 0 || runtime.RenderFrameInterval > 1;
            }
            catch (Exception)
            {
                return false;
            }
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

        private static string SafeMessage(Exception exception)
        {
            var safeMessage = string.IsNullOrWhiteSpace(exception?.Message)
                ? exception?.GetType().Name ?? "Unknown error"
                : exception.Message;
            return safeMessage.Length <= 1024 ? safeMessage : safeMessage.Substring(0, 1024);
        }
    }
}
