// SPDX-License-Identifier: MIT

namespace Haptics
{
    /// <summary>何もしないdriver。EditorとDesktopなど非対応platform用。</summary>
    public sealed class UnityNoOpHapticsDriver : IHapticsDriver
    {
        /// <summary>常に<see cref="HapticsCapability.None"/>。</summary>
        public HapticsCapability Capability => HapticsCapability.None;

        /// <summary>何もせず常にfalse。</summary>
        /// <param name="pattern">無視されるpattern。</param>
        /// <returns>常にfalse。</returns>
        public bool TryVibrate(HapticsPattern pattern)
        {
            return false;
        }

        /// <summary>解放する資源はない。</summary>
        public void Dispose()
        {
        }
    }
}
