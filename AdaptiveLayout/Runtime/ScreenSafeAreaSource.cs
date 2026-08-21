using UnityEngine;

namespace AdaptiveLayout
{
    internal sealed class ScreenSafeAreaSource : ISafeAreaSource
    {
        internal static readonly ScreenSafeAreaSource Instance = new ScreenSafeAreaSource();

        private ScreenSafeAreaSource()
        {
        }

        public bool TryGetSnapshot(out SafeAreaSnapshot snapshot)
        {
            return SafeAreaMath.TryCreateSnapshot(Screen.width, Screen.height, Screen.safeArea, out snapshot);
        }
    }
}
