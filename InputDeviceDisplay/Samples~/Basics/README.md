# Input Device Display Basics

`InputDeviceDisplayBasics.unity`を開いてPlayし、Keyboard、Mouse、Gamepad、Touchscreenを実際に操作してください。

- device接続、current以外のdeviceの切断、configuration changeだけでは表示familyは切り替わりません。
- current deviceが削除・切断・無効化された場合は、安全なfallback表示へ戻ります。
- KeyboardとMouseは`KeyboardMouse`として同じ表示へまとまります。
- 既知gamepadはXbox、PlayStation、Switch風へ分類し、それ以外のGamepadは`GenericGamepad`になります。
- 大きなfamily表示の下に、最後に操作したdevice IDとInput System layout名を表示します。
- glyph、画像、fontは同梱せず、familyに応じた色と文字だけをsample UIが選びます。

Sceneは`InputDeviceDisplayController`、`UIDocument`、sample controllerを設定済みです。ImportだけではInput System設定、Project Settings、開いているSceneを変更しません。
