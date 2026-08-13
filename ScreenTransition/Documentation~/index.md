# Screen Transition

Screen Transitionは、利用側が所有するUI Toolkitのpanelへ単色オーバーレイを配置し、透明度を時間で変える小さな画面遷移モジュールです。導入の最短手順はパッケージ直下の [README](../README.md) を参照してください。

動作確認済み: **Unity 6000.5.7f1** / Windows / .NET Standard 2.1

## 導入

Package Managerの **Add package from git URL** に次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/ScreenTransition#screen-transition-v1.0.1
```

利用側のasmdefから `ScreenTransition.Runtime` を参照します。外部パッケージへの依存はありません。

## 構成

利用側のUI ownerが、次の寿命と設定を所有します。

1. 描画先とscale、target display、panel間の順序を決める`PanelSettings`。
2. panel内での順序を決める`UIDocument`。
3. オーバーレイ、要求の直列化、更新、終了処理を所有するController。

Screen Transitionはglobal singletonを作らず、別のUI ownerの状態を変更しません。同じPanel Settingsを共有するUIDocumentの順序と、異なるPanel Settings間の順序は利用側で管理してください。

## 公開API

`ScreenTransitionController`は同じGameObjectの`UIDocument`を使います。`UIDocument.panelSettings`は利用側が設定してください。

```csharp
ScreenTransitionResult cover = await controller.CoverAsync(
    Color.black,
    0.25f,
    ScreenTransitionEasing.EaseInOut);

if (cover.IsSuccess)
{
    // 表示内容の切替は利用側の責務。
    ScreenTransitionResult reveal = await controller.RevealAsync(
        Color.black,
        0.25f,
        ScreenTransitionEasing.EaseInOut);
}
```

`ExecuteAsync(ScreenTransitionRequest)`はCoverとRevealの共通入口です。Controllerは`ScreenTransitionStatus`型の`Status`と`IsBusy`を公開し、`StatusChanged`で進捗、`Finished`で完了結果を通知します。

主な失敗理由は次のとおりです。

| `ScreenTransitionError` | 意味 |
|---|---|
| `InvalidRequest` | 0以上3600以下ではない時間、色、操作種類、補間方法が不正 |
| `MainThreadRequired` | Unityのメインスレッド以外から呼んだ |
| `Busy` | 同じControllerが処理中、または完了通知中 |
| `SurfaceUnavailable` | UIDocumentまたは描画先panelを準備できない |
| `ApplicationExiting` | Controller無効化、破棄、Play終了、アプリ終了で待機を終えた |
| `OperationFailed` | 予期しないUnity APIまたは内部処理の失敗 |

## 描画範囲

オーバーレイは自身のUIDocument rootへabsolute配置され、left、top、right、bottomを0としてpanelを覆います。この「全画面」はPanel Settingsが描画するdisplay viewportを意味します。

次の対象は自動では覆いません。

- 別のtarget display。
- 別RenderTextureへ描画するpanel。
- world-space panel。
- より高いsort orderのPanel SettingsまたはUIDocument。

## 時間と進捗

遷移は`Time.unscaledDeltaTime`に基づくため、`Time.timeScale == 0`でも進みます。durationは0以上3600以下の秒数を受け付け、0なら次の更新を待たずに終端値へ確定します。内部経過時間はdoubleで蓄積し、進捗は0以上1以下で後退せず、成功時に1になります。

Coverは指定色のalpha、Revealは不透明度0を終端とします。Coverは0から指定alphaへ、Revealは指定alphaから0へ、遷移進捗に応じて変化します。

## 直列化と通知

同じControllerが処理中、または完了通知を配信中に受けた新しい要求はBusyです。進行中の要求は上書きしません。状態通知と完了通知はメインスレッドで行い、通知先ごとの例外はほかの通知先と遷移本体から分離します。通知内で次の遷移を開始せず、通知から戻った後のframeで開始してください。

## 終了処理

Controllerを無効化または破棄すると、進行中の要求を終了してオーバーレイを入力対象から外します。Play Mode終了とアプリケーション終了では、待機中の処理を終了理由付きで確定します。Domain Reloadでmanaged owner自体が破棄される場合の完了通知は保証しません。

## Scene Flow連携

Screen TransitionはScene Flowを参照しません。両方を参照する利用側ownerで次の順に呼びます。

```text
Coverが成功
  -> Scene FlowのScene操作
  -> Scene操作の結果に応じて利用側の状態を更新
  -> Reveal
```

Scene操作が失敗した場合にRevealするか、覆ったまま復旧画面へ進むかは利用側が決めます。この方針により、画面表示とScene寿命の責務を混ぜません。

## 非目標

- Sceneの読込、切替、Unload。
- 入力map、EventSystem、ゲーム操作の停止。
- 音声、動画、画像、shader、wipe効果。
- Addressablesやネットワーク同期。
- global singleton、常駐Manager。
- 実行開始後のcancel。
- Panel Settings、UIDocument、sort order、target displayの自動変更。

## 検証

PlayModeテストは実際の`PanelSettings`、`UIDocument`、`VisualElement`を使い、次を確認します。

- rootとオーバーレイの描画矩形が一致すること。
- Cover、Reveal、0秒要求の終端不透明度。
- `Time.timeScale == 0`で遷移が完了すること。
- 処理中と通知中の再入がBusyになること。
- 無効化、破棄後にオーバーレイが入力を遮らないこと。
- 後から兄弟要素を追加しても、表示中のオーバーレイが最前面へ戻ること。
- UIDocument無効化またはPanel Settings喪失を`SurfaceUnavailable`で完了すること。

batchmode PlayModeテストはpanelのgeometry、解決済みstyle、状態契約を検証し、GPUのframe読取には使いません。実際の画面pixelへ指定色が反映されることは、graphics deviceを有効にしたMono PlayerとIL2CPP Playerの後段gateで確認します。Game View解像度、DPI、color spaceによる境界差を避けるため、Player gateでは中央の広い単色領域を証拠点として使います。
