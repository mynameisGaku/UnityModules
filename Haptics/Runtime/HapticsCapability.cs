// SPDX-License-Identifier: MIT

using System;

namespace Haptics
{
    /// <summary>driverが実際に提供する振動機能。flags型で、利用側はこれを見て劣化度合いを選ぶ。</summary>
    [Flags]
    public enum HapticsCapability
    {
        /// <summary>振動機能を持たない。EditorとDesktop既定値。</summary>
        None = 0,

        /// <summary>何らかの振動を鳴らせる。</summary>
        Vibrate = 1,

        /// <summary>振動の強度を連続値で制御できる。</summary>
        AmplitudeControl = 2,

        /// <summary>duration付きwaveform patternを再現できる。</summary>
        PatternWaveform = 4,
    }

    /// <summary>HapticsCapabilityの結合可否を判定する静的helper。</summary>
    public static class HapticsCapabilitySupport
    {
        private const HapticsCapability PrecisePatterns =
            HapticsCapability.AmplitudeControl | HapticsCapability.PatternWaveform;

        /// <summary>capabilityが振動そのものを提供するか。</summary>
        /// <param name="capability">判定対象のcapability。</param>
        /// <returns><see cref="HapticsCapability.Vibrate"/>を持つ場合はtrue。</returns>
        public static bool CanVibrate(this HapticsCapability capability)
        {
            return (capability & HapticsCapability.Vibrate) != 0;
        }

        /// <summary>capabilityが振動強度の連続制御を提供するか。</summary>
        /// <param name="capability">判定対象のcapability。</param>
        /// <returns><see cref="HapticsCapability.AmplitudeControl"/>を持つ場合はtrue。</returns>
        public static bool CanControlAmplitude(this HapticsCapability capability)
        {
            return (capability & HapticsCapability.AmplitudeControl) != 0;
        }

        /// <summary>capabilityがduration付きwaveform patternを再現できるか。</summary>
        /// <param name="capability">判定対象のcapability。</param>
        /// <returns><see cref="HapticsCapability.PatternWaveform"/>を持つ場合はtrue。</returns>
        public static bool CanPlayWaveformPatterns(this HapticsCapability capability)
        {
            return (capability & HapticsCapability.PatternWaveform) != 0;
        }

        /// <summary>capabilityがamplitudeと波形の両方を備えた精密patternを提供するか。</summary>
        /// <param name="capability">判定対象のcapability。</param>
        /// <returns>両方のflagを持つ場合だけtrue。</returns>
        public static bool SupportsPrecisePatterns(this HapticsCapability capability)
        {
            return (capability & PrecisePatterns) == PrecisePatterns;
        }
    }
}
