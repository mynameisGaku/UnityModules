# Input Gate

Input Gateは、`PlayerInput`が実行時に所有する指定Action Mapを、入れ子にできるleaseで一時停止する入力制御モジュールです。導入の最短手順はパッケージ直下の[README](../README.md)を参照してください。

## 必要環境

- Unity 6000.5.7f1以降。
- Input System 1.20.0。
- Runtime参照: `InputGate.Runtime`。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/InputGate#input-gate-v1.0.0
```

## 所有単位

Controllerは`PlayerInput.actions`から設定済みMap名を解決し、その実行中`InputActionMap` instanceを所有します。複数の`PlayerInput`が同じ元Assetを使っても、Input Systemが作る各playerのcopyは別instanceなので独立して所有できます。同じinstanceの重複所有は`OwnerAlreadyExists`です。

Controllerはglobal singletonを作りません。Sceneをまたぐ場合は、利用側が`PlayerInput`とControllerのGameObject寿命を揃えます。`PlayerInput`自身の無効化、Action Map切替、default map再有効化はInput System側の責務であり、このモジュールはそれを代替しません。

## 公開型

| 型 | 役割 |
| --- | --- |
| `InputGateController` | 設定Mapの所有、lease受付、停止、復元、状態通知 |
| `InputGateLease` | 1ownerの停止要求を`Dispose`まで保持 |
| `InputGateStatus` | readiness、停止中、error、Map数、lease数の不変snapshot |
| `InputGateError` | 設定、所有、thread、再入、外部変更、復元失敗の理由 |

## 状態遷移

Controller有効化時に設定を検証し、Map instanceの予約に成功すると`IsReady=true`になります。最初のlease取得時に各Actionの`enabled`を保存し、対象Mapを一括`Disable`します。追加leaseはAction状態を書き換えません。最後のlease解放時だけ、保存時に有効だったActionを個別に`Enable`して部分有効状態を復元します。

停止対象外Mapは変更しません。UI mapを設定一覧から外すことで、Gameplay入力を止めたままpause menuや遷移UIを操作できます。

## threadとcallback

`TryAcquire`、`Status`、event購読はUnityメインスレッド専用です。`InputGateLease.Dispose`と`IsActive`だけは任意スレッドから利用できます。worker解放はmanaged待機列へ入り、次のController `Update`で同時解放をまとめて処理します。

`StatusChanged`は主スレッドで状態とAction状態が一致した後に通知します。通知先ごとの例外を隔離し、通知中の新規`TryAcquire`は`Busy`です。通知中のlease解放は失われず、外側の通知終了後に反映します。

Input Systemは進行中Actionを無効化すると`canceled` callbackを同期実行します。そのcallback中の新規取得も`Busy`です。callbackがControllerを無効化または破棄した場合、取得要求は成功として返さず、健康な停止前状態をcleanupして`ControllerUnavailable`へ収束します。同じcallback内で再有効化された場合は古い世代を終了し、cleanup完了後に新しい所有を準備します。

## 外部変更

停止中は対象Actionが全て無効であることと、`PlayerInput.actions`が同じMap instanceを返すことを、取得・解放・通知後・毎`Update`で確認します。複数Mapの停止とActionごとの復元でも各書換え直後に確認し、不一致後は残りの旧Mapへ書き込みません。不一致は`ExternalActionStateChanged`です。この場合は新しい外部状態を保護するため復元を書き込まず、全leaseを無効化し、Controllerを無効化して再所有するまでfail closedを維持します。

外部コードが確認の間に変更して元へ戻した場合は観測できません。Action Mapの有効化を複数ownerから直接行わず、同じ責務をInput Gateへ集約してください。

## エラー

| 値 | 意味 |
| --- | --- |
| `InvalidConfiguration` | PlayerInput、Action Asset、Map名一覧が利用不可 |
| `ActionMapNotFound` | 指定Map名を実行中Action Assetから解決できない |
| `DuplicateActionMap` | 同じ実Mapを一覧へ重複指定 |
| `OwnerAlreadyExists` | 別Controllerが同じ実Mapを予約中 |
| `ControllerUnavailable` | lifecycle外または所有準備未完了 |
| `MainThreadRequired` | workerから取得を要求 |
| `Busy` | 状態通知またはAction変更callbackへ再入 |
| `ExternalActionStateChanged` | 停止中の外部有効化またはAction Asset交換 |
| `ActionStateChangeFailed` | Disable、復元、書戻し確認に失敗 |
| `ApplicationExiting` | アプリケーション終了で所有終了 |

## 非目標

- Legacy Input、直接device polling、個別Action filtering。
- UI Toolkit pointer event、uGUI EventSystem、`PlayerInput.DeactivateInput`の代替。
- held control、入力event、Action bufferの消費または消去。
- rebind、device pairing、control scheme、local multiplayer生成。
- Scene、画面遷移、時間倍率、音声、network、保存との自動連携。

Actionの再有効化後にheld controlがperformedを発生させるかは、Action typeと`initialStateCheck`に従います。Input Gateはdevice stateを改変しません。

## 検証

EditModeテストは公開面、世代分離、重複・worker解放を確認します。PlayModeテストは部分有効状態の復元、入れ子lease、対象外UI map、外部有効化、所有競合、別player instance、通知再入、同期`canceled` callback、lifecycle cleanupを実Input Systemで確認します。
