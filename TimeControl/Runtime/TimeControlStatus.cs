namespace TimeControl
{
    /// <summary>時間倍率の所有状態、基準値、適用値、要求数をまとめた通知用スナップショット。</summary>
    public readonly struct TimeControlStatus
    {
        /// <summary>所有状態と計算済みの時間倍率から通知用スナップショットを作る。</summary>
        /// <param name="isControlling">Time.timeScaleを管理中ならtrue。</param>
        /// <param name="error">現在の所有状態へ至った理由。</param>
        /// <param name="baselineTimeScale">所有開始時に取得した基準値。</param>
        /// <param name="effectiveMultiplier">有効な要求のうち最小の相対倍率。</param>
        /// <param name="effectiveTimeScale">最後に確認できた実際のTime.timeScale。</param>
        /// <param name="activeLeaseCount">現在有効な取得権の数。</param>
        internal TimeControlStatus(
            bool isControlling,
            TimeControlError error,
            float baselineTimeScale,
            float effectiveMultiplier,
            float effectiveTimeScale,
            int activeLeaseCount)
        {
            IsControlling = isControlling;
            Error = error;
            BaselineTimeScale = baselineTimeScale;
            EffectiveMultiplier = effectiveMultiplier;
            EffectiveTimeScale = effectiveTimeScale;
            ActiveLeaseCount = activeLeaseCount;
        }

        /// <summary>このControllerがTime.timeScaleを正常に管理中ならtrue。</summary>
        public bool IsControlling { get; }

        /// <summary>現在の所有状態へ至った理由。正常な管理中はNone。</summary>
        public TimeControlError Error { get; }

        /// <summary>所有開始時に取得した変更前のTime.timeScale。</summary>
        public float BaselineTimeScale { get; }

        /// <summary>有効な取得権のうち最小の相対倍率。取得権がなければ1。</summary>
        public float EffectiveMultiplier { get; }

        /// <summary>最後に書き込み後の一致を確認できた値、または異常検出時に読み取ったTime.timeScale。</summary>
        public float EffectiveTimeScale { get; }

        /// <summary>現在の世代で有効な取得権の数。所有終了後は0。</summary>
        public int ActiveLeaseCount { get; }
    }
}
