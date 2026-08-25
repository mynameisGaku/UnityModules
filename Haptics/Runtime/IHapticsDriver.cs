// SPDX-License-Identifier: MIT

namespace Haptics
{
    /// <summary>
    /// 振動要求を受け取りboolで成否だけを返すdriver境界。
    /// 遅延、キュー、schedulingは扱わない。
    /// </summary>
    public interface IHapticsDriver
    {
        /// <summary>このdriverが提供する振動機能。</summary>
        HapticsCapability Capability { get; }

        /// <summary>patternを再生する。</summary>
        /// <param name="pattern">serviceにより検証・変換済みのpattern。</param>
        /// <returns>要求を受理した場合はtrue。未対応、初期化未了、platform層の失敗はfalse。</returns>
        bool TryVibrate(HapticsPattern pattern);
    }
}
