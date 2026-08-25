// SPDX-License-Identifier: MIT

namespace Haptics
{
    /// <summary>platform判定により既定driverを1つ返すstatic factory。</summary>
    public static class HapticsDrivers
    {
        /// <summary>現在build targetへ適したdriverを作る。Editorでは必ずNoOp。</summary>
        /// <returns>platform対応driver。未対応platformはNoOp driver。</returns>
        public static IHapticsDriver ResolveDefault()
        {
#if UNITY_EDITOR
            return new UnityNoOpHapticsDriver();
#elif UNITY_ANDROID
            return new UnityAndroidHapticsDriver();
#elif UNITY_IOS
            return new UnityIOSHapticsDriver();
#else
            return new UnityNoOpHapticsDriver();
#endif
        }
    }
}
