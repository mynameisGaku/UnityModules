using System.Collections.Generic;

namespace TimeControl
{
    /// <summary>要求倍率の検査と、複数要求から適用倍率を決める純粋計算を提供する。</summary>
    internal static class TimeScaleResolver
    {
        // Controller公開前にも同じ境界値で純粋計算を検証できるようにする。
        private const float MaximumMultiplier = 100f;

        // Controller公開前にも同じ適用上限で基準値と積を検証できるようにする。
        private const float MaximumEffectiveTimeScale = 100f;

        /// <summary>所有開始時の基準値が管理可能な範囲か調べる。</summary>
        /// <param name="baselineTimeScale">所有開始時に読み取った時間倍率。</param>
        /// <returns>管理可能ならNone、そうでなければ範囲外理由。</returns>
        internal static TimeControlError ValidateBaseline(float baselineTimeScale)
        {
            return IsFinite(baselineTimeScale) && baselineTimeScale >= 0f && baselineTimeScale <= MaximumEffectiveTimeScale
                ? TimeControlError.None
                : TimeControlError.EffectiveTimeScaleOutOfRange;
        }

        /// <summary>要求倍率と、他の要求に隠れない場合の適用値を検査する。</summary>
        /// <param name="baselineTimeScale">所有開始時の時間倍率。</param>
        /// <param name="multiplier">検査する相対倍率。</param>
        /// <param name="effectiveTimeScale">検査に成功した場合の基準値と倍率の積。</param>
        /// <returns>使用可能ならNone、そうでなければ拒否理由。</returns>
        internal static TimeControlError ValidateMultiplier(float baselineTimeScale, float multiplier, out float effectiveTimeScale)
        {
            effectiveTimeScale = 0f;
            if (!IsFinite(multiplier) || multiplier < 0f || multiplier > MaximumMultiplier)
            {
                return TimeControlError.InvalidMultiplier;
            }

            if (ValidateBaseline(baselineTimeScale) != TimeControlError.None)
            {
                return TimeControlError.EffectiveTimeScaleOutOfRange;
            }

            var unmaskedEffectiveTimeScale = (double)baselineTimeScale * multiplier;
            if (double.IsNaN(unmaskedEffectiveTimeScale) || double.IsInfinity(unmaskedEffectiveTimeScale) ||
                unmaskedEffectiveTimeScale < 0d || unmaskedEffectiveTimeScale > MaximumEffectiveTimeScale)
            {
                return TimeControlError.EffectiveTimeScaleOutOfRange;
            }

            effectiveTimeScale = (float)unmaskedEffectiveTimeScale;
            return TimeControlError.None;
        }

        /// <summary>1件以上の要求から最小倍率を選び、空なら既定倍率1を返す。</summary>
        /// <param name="multipliers">有効性を検査済みの要求倍率。</param>
        /// <returns>適用する最小倍率、または1。</returns>
        internal static float ResolveMinimum(IEnumerable<float> multipliers)
        {
            var minimum = 1f;
            var found = false;
            foreach (var multiplier in multipliers)
            {
                if (!found || multiplier < minimum) minimum = multiplier;
                found = true;
            }

            return found ? minimum : 1f;
        }

        /// <summary>値がNaNでも無限大でもなければtrue。</summary>
        /// <param name="value">検査する値。</param>
        /// <returns>有限値ならtrue。</returns>
        internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
