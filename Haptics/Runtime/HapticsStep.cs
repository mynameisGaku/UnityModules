// SPDX-License-Identifier: MIT

using System;

namespace Haptics
{
    /// <summary>
    /// 振動patternの1step。duration 1〜5000msとamplitude 0〜1を持つreadonly value。
    /// 不正値はconstructorで<see cref="ArgumentOutOfRangeException"/>として即時に報告する。
    /// </summary>
    public readonly struct HapticsStep : IEquatable<HapticsStep>
    {
        /// <summary>許容する最小duration。</summary>
        public const int MinDurationMilliseconds = 1;

        /// <summary>許容する最大duration。</summary>
        public const int MaxDurationMilliseconds = 5000;

        /// <summary>このstepの振動時間。</summary>
        public int DurationMilliseconds { get; }

        /// <summary>このstepの振動強度。0で休止、1で最大。</summary>
        public float Amplitude { get; }

        /// <summary>durationとamplitudeを検証してstepを作る。</summary>
        /// <param name="durationMilliseconds">1以上5000以下のduration。</param>
        /// <param name="amplitude">0以上1以下の有限値。</param>
        /// <exception cref="ArgumentOutOfRangeException">durationまたはamplitudeが範囲外、非有限。</exception>
        public HapticsStep(int durationMilliseconds, float amplitude)
        {
            if (durationMilliseconds < MinDurationMilliseconds ||
                durationMilliseconds > MaxDurationMilliseconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durationMilliseconds),
                    durationMilliseconds,
                    $"durationは{MinDurationMilliseconds}〜{MaxDurationMilliseconds}msで指定してください。");
            }

            if (float.IsNaN(amplitude) || float.IsInfinity(amplitude) ||
                amplitude < 0f || amplitude > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amplitude),
                    amplitude,
                    "amplitudeは0以上1以下の有限値で指定してください。");
            }

            DurationMilliseconds = durationMilliseconds;
            Amplitude = amplitude;
        }

        /// <summary>同じdurationとamplitudeを持つか比較する。</summary>
        /// <param name="other">比較相手。</param>
        /// <returns>全field一致の場合true。</returns>
        public bool Equals(HapticsStep other)
        {
            return DurationMilliseconds == other.DurationMilliseconds &&
                   Amplitude.Equals(other.Amplitude);
        }

        /// <summary>任意objectと比較する。</summary>
        /// <param name="obj">比較相手。</param>
        /// <returns>同型かつ全field一致の場合true。</returns>
        public override bool Equals(object obj)
        {
            return obj is HapticsStep other && Equals(other);
        }

        /// <summary>全fieldから安定したhashを計算する。</summary>
        /// <returns>hash code。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return (DurationMilliseconds.GetHashCode() * 397) ^ Amplitude.GetHashCode();
            }
        }

        /// <summary>人間可読な表現を返す。</summary>
        /// <returns>例: HapticsStep(30ms, 0.5)。</returns>
        public override string ToString()
        {
            return $"HapticsStep({DurationMilliseconds}ms, {Amplitude})";
        }

        /// <summary>全field一致の等価演算子。</summary>
        /// <param name="left">左辺。</param>
        /// <param name="right">右辺。</param>
        /// <returns>一致する場合true。</returns>
        public static bool operator ==(HapticsStep left, HapticsStep right)
        {
            return left.Equals(right);
        }

        /// <summary>不一致演算子。</summary>
        /// <param name="left">左辺。</param>
        /// <param name="right">右辺。</param>
        /// <returns>1つでも異なる場合true。</returns>
        public static bool operator !=(HapticsStep left, HapticsStep right)
        {
            return !left.Equals(right);
        }
    }
}
