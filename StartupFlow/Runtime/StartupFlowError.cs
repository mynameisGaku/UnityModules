namespace StartupFlow
{
    /// <summary>起動処理を開始または完了できなかった理由。</summary>
    public enum StartupFlowError
    {
        /// <summary>失敗していない。</summary>
        None = 0,
        /// <summary>別のflowを実行中、完了通知中、または状態通知中。</summary>
        Busy = 1,
        /// <summary>Unityメインスレッド以外から呼ばれた。</summary>
        MainThreadRequired = 2,
        /// <summary>step一覧、step、またはstep識別子が不正。</summary>
        InvalidSteps = 3,
        /// <summary>step数が上限を超えている。</summary>
        TooManySteps = 4,
        /// <summary>同じ識別子のstepが複数ある。</summary>
        DuplicateStepId = 5,
        /// <summary>進捗が有限の0以上1以下ではないか、前回値より小さい。</summary>
        InvalidProgress = 6,
        /// <summary>完了済み、または現在実行中ではないstepのcontextを使った。</summary>
        StepNotActive = 7,
        /// <summary>利用側のCancellationTokenで中止された。</summary>
        Canceled = 8,
        /// <summary>Play Modeまたはアプリケーションが終了している。</summary>
        ApplicationExiting = 9,
        /// <summary>stepが例外で失敗した。</summary>
        StepFailed = 10,
        /// <summary>flow内部の状態更新またはthread復帰に失敗した。</summary>
        OperationFailed = 11
    }
}
