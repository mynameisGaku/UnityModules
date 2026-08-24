// SPDX-License-Identifier: MIT

using System;

namespace PlayerOptions
{
    /// <summary>操作結果に付随する補正またはUnity側の反映条件。</summary>
    [Flags]
    public enum PlayerOptionsWarning
    {
        /// <summary>警告がない。</summary>
        None = 0,

        /// <summary>保存済み表示設定が現在環境で利用できず、typed defaultを使用した。</summary>
        DisplayFallbackUsed = 1 << 0,

        /// <summary>品質名が一意に見つかり、保存済みindexを現在indexへ修復した。</summary>
        QualityIndexAdjusted = 1 << 1,

        /// <summary>保存済み品質を一意に特定できず、typed defaultを使用した。</summary>
        QualityFallbackUsed = 1 << 2,

        /// <summary>等価なrefresh rateを最大公約数で約分した。</summary>
        RefreshRateNormalized = 1 << 3,

        /// <summary>VSyncまたはOnDemandRenderingが正のtarget frame rateより優先される可能性がある。</summary>
        TargetFrameRateMayBeOverridden = 1 << 4,

        /// <summary>画面変更要求を発行したが、同じframe内での実反映は確認していない。</summary>
        ResolutionChangeDeferred = 1 << 5,

        /// <summary>画面変更呼出しが例外になり、requestが受理されたか確認できない。</summary>
        ResolutionOutcomeUnknown = 1 << 6,
    }
}
