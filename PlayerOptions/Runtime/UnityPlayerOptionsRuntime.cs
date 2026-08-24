// SPDX-License-Identifier: MIT

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PlayerOptions
{
    /// <summary>Unity 6000.5のglobal option APIへ接続する標準runtime境界。</summary>
    internal sealed class UnityPlayerOptionsRuntime : IPlayerOptionsRuntime
    {
        /// <summary>状態を持たない標準runtime境界。</summary>
        internal static readonly UnityPlayerOptionsRuntime Instance = new UnityPlayerOptionsRuntime();

        private UnityPlayerOptionsRuntime()
        {
        }

        /// <inheritdoc/>
        public bool IsMainThread => PlayerOptionsMainThread.IsCurrent;

        /// <inheritdoc/>
        public int ScreenWidth => Screen.width;

        /// <inheritdoc/>
        public int ScreenHeight => Screen.height;

        /// <inheritdoc/>
        public FullScreenMode FullScreenMode => Screen.fullScreenMode;

        /// <inheritdoc/>
        public RefreshRate CurrentRefreshRate => Screen.currentResolution.refreshRateRatio;

        /// <inheritdoc/>
        public Resolution[] Resolutions => Screen.resolutions;

        /// <inheritdoc/>
        public int TargetFrameRate => Application.targetFrameRate;

        /// <inheritdoc/>
        public float MasterVolume => AudioListener.volume;

        /// <inheritdoc/>
        public int QualityLevel => QualitySettings.GetQualityLevel();

        /// <inheritdoc/>
        public string[] QualityNames => QualitySettings.names;

        /// <inheritdoc/>
        public int VSyncCount => QualitySettings.vSyncCount;

        /// <inheritdoc/>
        public int RenderFrameInterval => OnDemandRendering.renderFrameInterval;

        /// <inheritdoc/>
        public void SetQualityLevel(int levelIndex) => QualitySettings.SetQualityLevel(levelIndex, true);

        /// <inheritdoc/>
        public void SetTargetFrameRate(int targetFrameRate) => Application.targetFrameRate = targetFrameRate;

        /// <inheritdoc/>
        public void SetMasterVolume(float masterVolume) => AudioListener.volume = masterVolume;

        /// <inheritdoc/>
        public void SetResolution(PlayerDisplayOptions display, bool specifyRefreshRate)
        {
            if (specifyRefreshRate)
            {
                Screen.SetResolution(
                    display.Width,
                    display.Height,
                    display.FullScreenMode,
                    display.PreferredRefreshRate);
                return;
            }

            Screen.SetResolution(display.Width, display.Height, display.FullScreenMode);
        }

        /// <inheritdoc/>
        public void LogObserverException(Exception exception) => Debug.LogException(exception);
    }
}
