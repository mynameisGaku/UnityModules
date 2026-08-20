namespace AudioControl
{
    /// <summary>音声操作を受け付けられなかった理由を表します。</summary>
    public enum AudioControlError
    {
        /// <summary>失敗はありません。</summary>
        None = 0,

        /// <summary>再生対象のAudioClipが指定されていません。</summary>
        InvalidClip = 1,

        /// <summary>音量、pitch、fade時間、またはpriorityが許容範囲外です。</summary>
        InvalidRequest = 2,

        /// <summary>Controllerが無効、未初期化、または破棄済みです。</summary>
        ControllerUnavailable = 3,

        /// <summary>Unityメインスレッド以外から操作されました。</summary>
        MainThreadRequired = 4,

        /// <summary>空きvoiceがなく、現在のpriority規則ではstealできません。</summary>
        VoiceLimitReached = 5,

        /// <summary>別のControllerまたは過去の有効期間に属するhandleです。</summary>
        ForeignHandle = 6,

        /// <summary>既に停止または解放されたhandleです。</summary>
        ReleasedHandle = 7,

        /// <summary>Application終了処理中のため新しい操作を受け付けません。</summary>
        ApplicationExiting = 8,

        /// <summary>AudioSourceによる再生開始に失敗しました。</summary>
        PlaybackFailed = 9
    }
}
