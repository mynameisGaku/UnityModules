namespace TimeControl
{
    /// <summary>時間倍率の所有または変更を完了できなかった理由。</summary>
    public enum TimeControlError
    {
        /// <summary>失敗していない。</summary>
        None = 0,

        /// <summary>要求した倍率が有限値ではない、負値、または上限超過。</summary>
        InvalidMultiplier = 1,

        /// <summary>基準値または要求倍率から求めた時間倍率が許容範囲外。</summary>
        EffectiveTimeScaleOutOfRange = 2,

        /// <summary>Unityメインスレッド以外から取得を要求した。</summary>
        MainThreadRequired = 3,

        /// <summary>状態通知中のため、新しい取得要求を受け付けられない。</summary>
        Busy = 4,

        /// <summary>別のControllerがTime.timeScaleの所有権を保持している。</summary>
        OwnerAlreadyExists = 5,

        /// <summary>Controllerが無効、破棄済み、または所有権を解放済み。</summary>
        ControllerUnavailable = 6,

        /// <summary>アプリケーション終了処理により所有権を解放した。</summary>
        ApplicationExiting = 7,

        /// <summary>管理中のTime.timeScaleが外部から変更された。</summary>
        ExternalTimeScaleChanged = 8,

        /// <summary>Time.timeScaleの読み書きまたは書き戻し確認に失敗した。</summary>
        TimeScaleWriteFailed = 9,
    }
}
