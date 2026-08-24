namespace InputDeviceDisplay
{
    /// <summary>入力端末の表示状態を更新できない理由。</summary>
    public enum InputDeviceDisplayError
    {
        /// <summary>失敗していない。</summary>
        None = 0,

        /// <summary>閾値またはlayout上書き設定が利用できない。</summary>
        InvalidConfiguration = 1,

        /// <summary>Controllerが無効、破棄済み、または購読を開始できない。</summary>
        ControllerUnavailable = 2,
    }
}
