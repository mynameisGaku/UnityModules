namespace InputGate
{
    /// <summary>Action Mapの所有、停止、復元を完了できなかった理由。</summary>
    public enum InputGateError
    {
        /// <summary>失敗していない。</summary>
        None = 0,

        /// <summary>PlayerInputまたは停止対象Action Map名の設定が利用できない。</summary>
        InvalidConfiguration = 1,

        /// <summary>指定されたAction Map名がPlayerInputの実行中Action Assetに存在しない。</summary>
        ActionMapNotFound = 2,

        /// <summary>同じAction Mapが停止対象へ複数回指定されている。</summary>
        DuplicateActionMap = 3,

        /// <summary>別のControllerが同じ実行中Action Mapを所有している。</summary>
        OwnerAlreadyExists = 4,

        /// <summary>Controllerが無効、破棄済み、または所有準備を完了していない。</summary>
        ControllerUnavailable = 5,

        /// <summary>Unityメインスレッド以外から取得を要求した。</summary>
        MainThreadRequired = 6,

        /// <summary>通知またはAction状態変更の処理中で、新しい取得要求を受け付けられない。</summary>
        Busy = 7,

        /// <summary>停止中のActionが外部から有効化された、またはPlayerInputのAction Assetが交換された。</summary>
        ExternalActionStateChanged = 8,

        /// <summary>Actionの停止、復元、または書き戻し確認に失敗した。</summary>
        ActionStateChangeFailed = 9,

        /// <summary>アプリケーション終了処理により所有権を解放した。</summary>
        ApplicationExiting = 10,
    }
}
