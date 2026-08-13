namespace ScreenTransition
{
    /// <summary>画面遷移要求を完了できなかった理由。</summary>
    public enum ScreenTransitionError
    {
        /// <summary>失敗していない。</summary>
        None = 0,

        /// <summary>操作、色、時間、または変化曲線が不正。</summary>
        InvalidRequest = 1,

        /// <summary>Unityメインスレッド以外から呼ばれた。</summary>
        MainThreadRequired = 2,

        /// <summary>別の要求を処理中、または通知中。</summary>
        Busy = 3,

        /// <summary>表示先となるUIDocumentを使用できない。</summary>
        SurfaceUnavailable = 4,

        /// <summary>Controllerの無効化、破棄、またはアプリケーション終了で中断した。</summary>
        ApplicationExiting = 5,

        /// <summary>予期しない処理失敗が起きた。</summary>
        OperationFailed = 6,
    }
}
