# 入力デバイス表示（InputDeviceDisplay）

## 30秒で分かる

Input Device Displayは、Input Systemが受け取ったglobalな実入力を監視し、最後に操作されたdeviceを表示向けのfamilyへ分類する小さなRuntimeモジュールです。KeyboardとMouseは共通familyとして扱い、既知のgamepadはXbox、PlayStation、Switch風へ分類します。分類できないgamepadには`GenericGamepad`、それ以外には設定したfallbackを返します。

画面上の「Press A」「Press Space」のような案内を切り替えるための判定だけを担当します。button画像やglyph atlasは同梱せず、利用側UIがfamilyに対応する文字列・画像・styleを選びます。

## こんなときに使う

- 最後に操作したKeyboard / MouseとGamepadで操作案内を切り替えたい。
- Xbox、PlayStation、Switch風の表示を型に基づいて選びたい。
- project固有device layoutを、厳密一致のoverrideで既知familyへ割り当てたい。
- device未操作時や分類不能時の表示を、明示したfallbackへ揃えたい。

判定はapplication全体で1つです。`PlayerInput`ごと、userごと、local multiplayerのplayerごとには追跡しません。

## 導入

Unity 6000.5.7f1以降とInput System 1.20.0を使用します。Package ManagerのGit URLには次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/InputDeviceDisplay#input-device-display-v1.0.0
```

利用側にasmdefがある場合は`InputDeviceDisplay.Runtime`を参照します。フォルダーを直接管理する場合だけ、`InputDeviceDisplay/`を`Assets/Modules/InputDeviceDisplay/`へ配置してください。

Package ManagerのSamplesから`Input Device Display Basics`をimportし、`InputDeviceDisplayBasics.unity`をPlayすると、実際のKeyboard、Mouse、Gamepad、Touch操作でfamily表示が切り替わることを確認できます。接続しただけでは切り替わりません。

## 3分で試す

1. 表示を切り替えるSceneのGameObjectへ`InputDeviceDisplayController`を追加します。
2. Inspectorで未操作時の`Fallback Style`、GamepadとMouseのactivity閾値、必要なexact layout overrideを設定します。`Unknown`はfallbackまたはoverride先に指定できません。
3. `StateChanged`を購読し、`State.Style`に対応する文字列、画像、glyph、styleを利用側UIで選びます。
4. Keyboard、Mouse、Gamepad、Touchscreenを実際に操作し、`HasDeviceActivity`、`DeviceId`、`LayoutName`を確認します。

```csharp
using InputDeviceDisplay;
using UnityEngine;

public sealed class PromptStylePresenter : MonoBehaviour
{
    [SerializeField] private InputDeviceDisplayController _controller;

    private void OnEnable()
    {
        _controller.StateChanged += HandleStateChanged;
        HandleStateChanged(_controller.State);
    }

    private void OnDisable()
    {
        if (_controller != null) _controller.StateChanged -= HandleStateChanged;
    }

    private static void HandleStateChanged(InputDeviceDisplayState state)
    {
        Debug.Log($"Prompt style: {state.Style}, layout: {state.LayoutName}");
    }
}
```

## 表示family

| family | 用途 |
| --- | --- |
| `KeyboardMouse` | KeyboardまたはMouseの操作案内 |
| `XboxStyleGamepad` | XInput系gamepadの表示 |
| `PlayStationStyleGamepad` | DualShock系gamepadの表示 |
| `SwitchStyleGamepad` | Switch Pro系gamepadの表示 |
| `GenericGamepad` | familyを特定できないGamepadの表示 |
| `Touch` | Touchscreenの表示 |
| `Unknown` | Controller未利用または設定エラーによる未判定状態。fallbackとoverride先には指定不可 |

既知gamepadの分類はInput Systemのdevice型階層を使います。`manufacturer`や`product`の自由記述文字列からfamilyを推測しないため、誤認識させたくないproject固有deviceはlayout overrideで明示します。

## layout overrideとfallback

layout overrideは、Input Systemがdeviceへ割り当てたlayout名との厳密一致です。部分一致、大文字小文字を無視した一致、manufacturer名からの推測は行いません。overrideを設定したlayoutは標準分類より先に評価されます。

有効な入力がまだない場合や、対象deviceを表示familyへ分類できない場合は設定済みfallbackを使用します。fallbackとoverrideは起動時に検証され、`Unknown`、空のlayout名、前後空白、同じlayoutの重複、0以下または非有限のactivity閾値など、曖昧な設定は黙って無視せず`InvalidConfiguration`になります。

## 契約

- device接続、current以外のdeviceの切断、configuration changeだけでは「最後の操作」を切り替えません。current deviceの削除・切断・無効化ではfallbackへ戻ります。
- button press、Gamepadの閾値以上の軸、Mouseのdelta・scroll・button press、Touchのpress・押下中移動をactivityとして扱います。release、stickの中央復帰、閾値未満のdrift、Touch release、Mouseのabsolute position-onlyは無視します。
- 最後に観測した対象deviceのfamilyだけを公開し、入力Actionやcontrol schemeを変更しません。
- 同じfamily内の別deviceへ変わると`DeviceId`と`LayoutName`を含むstateを通知します。描画assetがfamilyだけで決まるUIは、`Style`が同じなら再読込を省けます。
- layout overrideは完全一致で標準分類より優先し、未知のGamepadは`GenericGamepad`へ退避します。
- fallbackを含む設定は利用開始前に検証し、不正設定を正常状態として扱いません。
- 入力eventを消費せず、`PlayerInput`のpairing、Action Map、binding、rebind状態へ書き込みません。

## 公開API

- `InputDeviceDisplayController.State` — 現在の不変スナップショット。
- `InputDeviceDisplayController.StateChanged` — stateが変わったときの通知。1件の購読先が例外を送出しても、残りの購読先とControllerへ伝播しません。
- `InputDeviceDisplayState` — `IsReady`、`HasDeviceActivity`、`Style`、`DeviceId`、`LayoutName`、`Error`を保持します。
- `InputDeviceDisplayStyle` — 表示family。
- `InputDeviceDisplayError` — `None`、`InvalidConfiguration`、`ControllerUnavailable`。

## 含まないもの

- button glyph、font、画像、controller diagram、platform別asset bundle。
- binding表示文字列の生成、interactive rebinding、control scheme切替。
- device pairing、`InputUser`管理、`PlayerInput`生成、player別の最終device追跡。
- 入力eventの消費、Action bufferの消去、Gameplay入力の停止。
- `manufacturer`や`product`文字列によるdevice family推測。
- 設定の永続化、network同期、SceneをまたぐUIの自動生成。

詳しい優先順位と設定失敗条件は[Documentation](Documentation~/index.md)を参照してください。

利用条件は[LICENSE.md](LICENSE.md)、外部依存は[Third-Party Notices.txt](Third-Party%20Notices.txt)を参照してください。
