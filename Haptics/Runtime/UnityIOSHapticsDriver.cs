// SPDX-License-Identifier: MIT

#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
#endif

namespace Haptics
{
    /// <summary>
    /// iOS AudioToolbox frameworkのシステム振動(kSystemSoundID_Vibrate=4095)を使うdriver。
    /// capabilityはVibrateのみ。ネイティブプラグイン無しの実装範囲であり、
    /// patternのduration並びは反映されず、最初のstep durationで粗く近似される。
    /// </summary>
    public sealed class UnityIOSHapticsDriver : IHapticsDriver
    {
#if UNITY_IOS && !UNITY_EDITOR
        private const uint SystemSoundIdVibrate = 4095;

        [DllImport("/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox")]
        private static extern void AudioServicesPlaySystemSound(uint inSystemSoundID);
#endif

        /// <summary>常に<see cref="HapticsCapability.Vibrate"/>のみ。Editor/DesktopスタブではNone。</summary>
        public HapticsCapability Capability
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return HapticsCapability.Vibrate;
#else
                return HapticsCapability.None;
#endif
            }
        }

        /// <summary>システム振動を1回呼ぶ。長いpatternも最初のstep durationで粗く近似される。</summary>
        /// <param name="pattern">再生する検証済みpattern。nullの場合はfalse。</param>
        /// <returns>システム振動の呼出しに成功した場合はtrue。</returns>
        public bool TryVibrate(HapticsPattern pattern)
        {
            if (pattern == null || pattern.Steps.Count == 0) return false;

#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                AudioServicesPlaySystemSound(SystemSoundIdVibrate);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>解放する資源はない。</summary>
        public void Dispose()
        {
        }
    }
}
