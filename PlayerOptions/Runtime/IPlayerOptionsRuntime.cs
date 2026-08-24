// SPDX-License-Identifier: MIT

using System;
using UnityEngine;

namespace PlayerOptions
{
    /// <summary>Unity global optionの観測と書込をserviceの計算処理から分離する内部境界。</summary>
    internal interface IPlayerOptionsRuntime
    {
        /// <summary>現在threadがUnity main threadならtrue。</summary>
        bool IsMainThread { get; }

        /// <summary>現在の画面幅。</summary>
        int ScreenWidth { get; }

        /// <summary>現在の画面高さ。</summary>
        int ScreenHeight { get; }

        /// <summary>現在の全画面表示方式。</summary>
        FullScreenMode FullScreenMode { get; }

        /// <summary>現在displayのrefresh rate。</summary>
        RefreshRate CurrentRefreshRate { get; }

        /// <summary>現在displayが列挙するfullscreen resolution。</summary>
        Resolution[] Resolutions { get; }

        /// <summary>現在のtarget frame rate。</summary>
        int TargetFrameRate { get; }

        /// <summary>現在のmaster volume。</summary>
        float MasterVolume { get; }

        /// <summary>現在の品質level index。</summary>
        int QualityLevel { get; }

        /// <summary>現在projectの品質level名。</summary>
        string[] QualityNames { get; }

        /// <summary>現在品質のvertical synchronization間隔。</summary>
        int VSyncCount { get; }

        /// <summary>OnDemandRenderingが使用する描画frame間隔。</summary>
        int RenderFrameInterval { get; }

        /// <summary>品質levelを適用する。</summary>
        /// <param name="levelIndex">現在project内の有効index。</param>
        void SetQualityLevel(int levelIndex);

        /// <summary>target frame rateを適用する。</summary>
        /// <param name="targetFrameRate">-1または正数。</param>
        void SetTargetFrameRate(int targetFrameRate);

        /// <summary>master volumeを適用する。</summary>
        /// <param name="masterVolume">0以上1以下の有限値。</param>
        void SetMasterVolume(float masterVolume);

        /// <summary>画面変更を要求する。</summary>
        /// <param name="display">事前検証済みの表示設定。</param>
        /// <param name="specifyRefreshRate">refresh rate付きoverloadを使用する場合はtrue。</param>
        void SetResolution(PlayerDisplayOptions display, bool specifyRefreshRate);

        /// <summary>購読先例外をUnity Consoleへ記録する。</summary>
        /// <param name="exception">購読先から送出された例外。</param>
        void LogObserverException(Exception exception);
    }
}
