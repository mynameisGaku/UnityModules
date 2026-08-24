# Input Device Display

Input Device Displayは、Input System上のglobalな入力activityから最後に操作されたdeviceを選び、UI表示に使えるfamilyへ分類します。導入の最短手順はパッケージ直下の[README](../README.md)を参照してください。

## 必要環境

- Unity 6000.5.7f1以降。
- Input System 1.20.0。
- Runtime参照: `InputDeviceDisplay.Runtime`。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/InputDeviceDisplay#input-device-display-v1.0.0
```

## 責務

このモジュールが所有するのは「application全体で最後に操作されたdevice family」という表示判断だけです。Input Action、binding、control scheme、`PlayerInput`、`InputUser`のownerにはなりません。利用側UIは公開されたfamilyを受け取り、project自身が所有する文字列、glyph、画像、styleを表示します。

追跡単位はglobalです。複数playerが同時に操作するgameでは、最後にactivityが観測されたplayerのdeviceがapplication全体の表示を切り替えます。playerごとの案内が必要な場合は、このモジュールの対象外です。

## family

| family | 標準の分類元 | fallback |
| --- | --- | --- |
| `KeyboardMouse` | `Keyboard`、`Mouse` | しない |
| `XboxStyleGamepad` | XInput系の型階層 | しない |
| `PlayStationStyleGamepad` | DualShock系の型階層 | しない |
| `SwitchStyleGamepad` | Switch Pro系の型階層 | しない |
| `GenericGamepad` | 上記へ特定できない`Gamepad` | Gamepad内の最終退避先 |
| `Touch` | `Touchscreen` | しない |
| `Unknown` | Controller未利用または設定エラーによる未判定 | fallback・override先には指定不可 |

標準分類はInput Systemのdevice型階層を使用します。platformによって変わり得る`manufacturer`、`product`、display nameの文字列は分類根拠にしません。

## 判定の優先順位

対象deviceで実入力activityを観測したとき、次の順に表示familyを決めます。

1. deviceのlayout名と厳密一致するlayout override。
2. Keyboard、Mouse、Touchscreen、および既知gamepad型の標準分類。
3. 未分類の`Gamepad`に対する`GenericGamepad`。
4. 分類できないdeviceに対する設定済みfallback。

layout名は完全一致で比較します。部分一致や大文字小文字を無視した補正は行いません。project固有layoutを使う場合は、Input Systemへ登録した正確なlayout名を指定してください。

## activityと状態変更

device追加、current以外のdeviceの削除・切断、configuration change、layout登録だけは操作activityではありません。current deviceが削除・切断・無効化された場合はfallbackへ戻ります。表示familyの候補になるのはInput Systemから届いた実入力です。このモジュールはeventを観測するだけで、handled状態、control値、Action状態を書き換えません。

button press、Gamepadと汎用deviceの閾値以上の軸、Mouseのdelta・scroll・button press、Touchのpress・押下中移動をactivityとして扱います。button release、軸の中央復帰、閾値未満のdrift、Touch release、Mouseのabsolute position-onlyはactivityにしません。

公開状態はfallbackから開始し、有効なactivityを観測すると新しいfamilyへ移ります。同じfamilyに分類されるdevice間の移動ではfamily値は変わりませんが、`DeviceId`または`LayoutName`が変わるためstateは通知されます。device instanceそのものが必要な処理や、playerごとの所有判断には使用しないでください。

## 設定検証

設定は追跡開始前に一度検証します。少なくとも次の曖昧な設定は明示的な設定エラーです。

- layout名が空または空白だけのoverride。
- layout名の前後に空白を含むoverride。
- 同じlayout名を複数familyへ割り当てる重複override。
- `Unknown`または定義外のfamily値をfallback・override先へ指定した設定。
- 0以下、非有限、または許容範囲外のactivity閾値。

不正なentryだけを黙って無視して追跡を続けることはしません。修正可能な設定問題と、入力activityがまだない正常なfallback状態を区別できるようにします。

## 非目標

- glyph asset、controller画像、font、platform別のbutton表記。
- rebind UI、binding文字列生成、control scheme選択。
- `PlayerInput`、`InputUser`、device pairing、local multiplayerの生成または管理。
- 入力eventの消費、Action Map停止、held入力やbufferの消去。
- player別またはUI panel別の最終device追跡。
- manufacturer、product、display nameの文字列推測。
- 自動Scene連携、保存、network同期。

## 公開状態と通知

`InputDeviceDisplayController`はGameObjectが所有し、静的singletonは作りません。`State`は現在の`InputDeviceDisplayState`を返し、`StateChanged`はdevice、layout、family、監視状態のいずれかが変わったときに通知します。各購読先は個別に呼び出され、1件の例外で残りの購読先や入力監視を停止しません。

`IsReady=false`なら`Error`を確認します。`InvalidConfiguration`はInspector設定の修正が必要で、`ControllerUnavailable`はcomponentが無効、破棄済み、またはInput System購読を開始できない状態です。fallback中は`HasDeviceActivity=false`、`DeviceId`は無効値、`LayoutName`は空文字列です。

## 検証方針

Runtimeテストでは標準family、override優先、fallback、重複・空layout・定義外familyの拒否を確認します。Input System統合テストではdevice追加だけでfamilyが変わらないこと、実入力で切り替わること、同familyの連続activity、unknown gamepadの`GenericGamepad`退避、eventを消費しないことを確認します。
