// SPDX-License-Identifier: MIT

using System;
using UnityEngine;

namespace PlayerOptions
{
    /// <summary>PlayerPrefsまたは差替storageへ書き出す内部JSON schema。</summary>
    [Serializable]
    internal sealed class PlayerOptionsDocument
    {
        private const int MissingInt = int.MinValue;
        private const long MissingLong = -1L;

        /// <summary>schema fieldが欠落した場合に残るsentinel値付き文書を作る。</summary>
        internal PlayerOptionsDocument()
        {
            SchemaVersion = MissingInt;
            DisplayWidth = MissingInt;
            DisplayHeight = MissingInt;
            FullScreenMode = MissingInt;
            RefreshRateNumerator = MissingLong;
            RefreshRateDenominator = MissingLong;
            TargetFrameRate = MissingInt;
            MasterVolume = float.NaN;
            QualityLevelIndex = MissingInt;
            QualityLevelName = null;
        }

        /// <summary>保存文書のschema version。</summary>
        public int SchemaVersion;

        /// <summary>希望画面幅。</summary>
        public int DisplayWidth;

        /// <summary>希望画面高さ。</summary>
        public int DisplayHeight;

        /// <summary><see cref="UnityEngine.FullScreenMode"/>の整数値。</summary>
        public int FullScreenMode;

        /// <summary>希望refresh rateの分子。uint全域と欠落を区別するためJSONではlongを使う。</summary>
        public long RefreshRateNumerator;

        /// <summary>希望refresh rateの分母。uint全域と欠落を区別するためJSONではlongを使う。</summary>
        public long RefreshRateDenominator;

        /// <summary>-1または正数の目標frame rate。</summary>
        public int TargetFrameRate;

        /// <summary>0以上1以下のmaster volume。</summary>
        public float MasterVolume;

        /// <summary>保存時の品質level index。</summary>
        public int QualityLevelIndex;

        /// <summary>保存時の品質level名。</summary>
        public string QualityLevelName;

        /// <summary>全ての必須fieldがJSON内に存在した場合はtrue。</summary>
        internal bool HasAllRequiredFields
        {
            get
            {
                return SchemaVersion != MissingInt &&
                       DisplayWidth != MissingInt &&
                       DisplayHeight != MissingInt &&
                       FullScreenMode != MissingInt &&
                       RefreshRateNumerator != MissingLong &&
                       RefreshRateDenominator != MissingLong &&
                       TargetFrameRate != MissingInt &&
                       !float.IsNaN(MasterVolume) &&
                       QualityLevelIndex != MissingInt &&
                       QualityLevelName != null;
            }
        }

        /// <summary>正規化済みstateからcurrent schema文書を作る。</summary>
        internal static PlayerOptionsDocument FromState(PlayerOptionsState state)
        {
            return new PlayerOptionsDocument
            {
                SchemaVersion = PlayerOptionsSchema.CurrentVersion,
                DisplayWidth = state.Display.Width,
                DisplayHeight = state.Display.Height,
                FullScreenMode = (int)state.Display.FullScreenMode,
                RefreshRateNumerator = (long)state.Display.PreferredRefreshRate.numerator,
                RefreshRateDenominator = (long)state.Display.PreferredRefreshRate.denominator,
                TargetFrameRate = state.TargetFrameRate,
                MasterVolume = state.MasterVolume,
                QualityLevelIndex = state.Quality.LevelIndex,
                QualityLevelName = state.Quality.LevelName,
            };
        }

        /// <summary>文書fieldを公開stateへ写す。値の正当性はvalidatorが後で確認する。</summary>
        internal PlayerOptionsState ToState()
        {
            var refreshRate = new RefreshRate
            {
                numerator = checked((uint)RefreshRateNumerator),
                denominator = checked((uint)RefreshRateDenominator),
            };
            var display = new PlayerDisplayOptions(
                DisplayWidth,
                DisplayHeight,
                (UnityEngine.FullScreenMode)FullScreenMode,
                refreshRate);
            var quality = new PlayerQualityOptions(QualityLevelIndex, QualityLevelName);
            return new PlayerOptionsState(display, TargetFrameRate, MasterVolume, quality);
        }
    }
}
