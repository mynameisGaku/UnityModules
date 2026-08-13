using UnityEngine;

namespace ScreenTransition
{
    /// <summary>公開した変化曲線を決定論的に評価する。</summary>
    internal static class ScreenTransitionEasingUtility
    {
        /// <summary>0以上1以下の進捗へ指定曲線を適用する。</summary>
        /// <param name="easing">適用する変化曲線。</param>
        /// <param name="progress">曲線適用前の進捗。</param>
        /// <returns>0以上1以下の表示進捗。</returns>
        internal static float Evaluate(ScreenTransitionEasing easing, float progress)
        {
            var value = Mathf.Clamp01(progress);
            switch (easing)
            {
                case ScreenTransitionEasing.Linear:
                    return value;
                case ScreenTransitionEasing.EaseIn:
                    return value * value;
                case ScreenTransitionEasing.EaseOut:
                    return 1f - ((1f - value) * (1f - value));
                case ScreenTransitionEasing.EaseInOut:
                    return value * value * (3f - (2f * value));
                default:
                    return value;
            }
        }
    }
}
