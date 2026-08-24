namespace InputDeviceDisplay
{
    /// <summary>画面へ表示する入力端末のボタン表記体系。</summary>
    public enum InputDeviceDisplayStyle
    {
        /// <summary>対応する表記体系を特定できない。</summary>
        Unknown = 0,

        /// <summary>キーボードとマウス向けの表記。</summary>
        KeyboardMouse = 1,

        /// <summary>Xbox系ゲームパッド向けの表記。</summary>
        XboxStyleGamepad = 2,

        /// <summary>PlayStation系ゲームパッド向けの表記。</summary>
        PlayStationStyleGamepad = 3,

        /// <summary>Nintendo Switch系ゲームパッド向けの表記。</summary>
        SwitchStyleGamepad = 4,

        /// <summary>機種を特定しないゲームパッド向けの表記。</summary>
        GenericGamepad = 5,

        /// <summary>タッチ操作向けの表記。</summary>
        Touch = 6,
    }
}
