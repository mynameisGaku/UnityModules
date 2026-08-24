// SPDX-License-Identifier: MIT

using System;
using UnityEngine;

namespace PlayerOptions
{
    /// <summary>画面解像度、表示方式、希望refresh rateをまとめた変更不能な表示設定。</summary>
    public readonly struct PlayerDisplayOptions : IEquatable<PlayerDisplayOptions>
    {
        /// <summary>表示設定を作る。値の利用可否は<see cref="PlayerOptionsService"/>が検証する。</summary>
        /// <param name="width">希望する画面幅。</param>
        /// <param name="height">希望する画面高さ。</param>
        /// <param name="fullScreenMode">希望する全画面表示方式。</param>
        /// <param name="preferredRefreshRate">希望refresh rate。numeratorとdenominatorが両方0なら指定しない。</param>
        public PlayerDisplayOptions(
            int width,
            int height,
            FullScreenMode fullScreenMode,
            RefreshRate preferredRefreshRate)
        {
            Width = width;
            Height = height;
            FullScreenMode = fullScreenMode;
            PreferredRefreshRate = preferredRefreshRate;
        }

        /// <summary>希望する画面幅。</summary>
        public int Width { get; }

        /// <summary>希望する画面高さ。</summary>
        public int Height { get; }

        /// <summary>希望する全画面表示方式。</summary>
        public FullScreenMode FullScreenMode { get; }

        /// <summary>希望refresh rate。numeratorとdenominatorが両方0なら指定しない。</summary>
        public RefreshRate PreferredRefreshRate { get; }

        /// <summary>全ての表示設定が等しい場合はtrueを返す。</summary>
        /// <param name="other">比較する表示設定。</param>
        /// <returns>全ての値が等しい場合はtrue。</returns>
        public bool Equals(PlayerDisplayOptions other)
        {
            return Width == other.Width &&
                   Height == other.Height &&
                   FullScreenMode == other.FullScreenMode &&
                   PreferredRefreshRate.Equals(other.PreferredRefreshRate);
        }

        /// <summary>指定objectが同じ表示設定ならtrueを返す。</summary>
        /// <param name="obj">比較するobject。</param>
        /// <returns>同じ表示設定ならtrue。</returns>
        public override bool Equals(object obj) => obj is PlayerDisplayOptions other && Equals(other);

        /// <summary>全ての表示設定からhash値を返す。</summary>
        /// <returns>表示設定のhash値。</returns>
        public override int GetHashCode() => HashCode.Combine(Width, Height, (int)FullScreenMode, PreferredRefreshRate);

        /// <summary>左右の表示設定が等しい場合はtrueを返す。</summary>
        /// <param name="left">左側の表示設定。</param>
        /// <param name="right">右側の表示設定。</param>
        /// <returns>左右が等しい場合はtrue。</returns>
        public static bool operator ==(PlayerDisplayOptions left, PlayerDisplayOptions right) => left.Equals(right);

        /// <summary>左右の表示設定が異なる場合はtrueを返す。</summary>
        /// <param name="left">左側の表示設定。</param>
        /// <param name="right">右側の表示設定。</param>
        /// <returns>左右が異なる場合はtrue。</returns>
        public static bool operator !=(PlayerDisplayOptions left, PlayerDisplayOptions right) => !left.Equals(right);
    }
}
