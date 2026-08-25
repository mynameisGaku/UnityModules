// SPDX-License-Identifier: MIT

namespace Haptics
{
    /// <summary>振動要求が失敗した理由。</summary>
    public enum HapticsError
    {
        /// <summary>失敗していない。</summary>
        None = 0,

        /// <summary>現在platformまたはdriverが振動を提供せず、要求を実行できなかった。</summary>
        UnsupportedPlatform = 1,

        /// <summary>service構築時にdriverを解決できなかった。通常の構築経路では発生しない予約値。</summary>
        DriverMissing = 2,

        /// <summary>patternとしてnull参照が渡された。</summary>
        NullPattern = 3,

        /// <summary>patternにstepが1つも含まれない。</summary>
        EmptyPattern = 4,

        /// <summary>patternのstep数が最大64を超えた。</summary>
        PatternTooLong = 5,

        /// <summary>step durationが1〜5000msの範囲外。</summary>
        InvalidDuration = 6,

        /// <summary>amplitudeが0〜1の範囲外、NaN、または無限大。</summary>
        InvalidAmplitude = 7,

        /// <summary>serviceはDispose済みで、以降の再生呼出しを受け付けない。</summary>
        ServiceDisposed = 8,

        /// <summary>定義されていないHapticsIntent値。enum cast対策。</summary>
        UnknownIntent = 9,
    }
}
