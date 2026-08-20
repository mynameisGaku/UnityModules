# Input Gate

Input Gateは、`PlayerInput`が実行時に所有する指定Action Mapを、1件以上の`InputGateLease`が存在する間だけ停止する小さな入力制御モジュールです。最初の取得時にActionごとの有効状態を保存し、最後の解放時にその部分有効状態まで復元します。

Scene Flow、Screen Transition、Time Controlとは依存せず、利用側ownerが必要な期間だけleaseを保持して組み合わせます。global singleton、自動生成GameObject、`DontDestroyOnLoad`は作りません。

## 導入

Unity 6000.5.7f1以降とInput System 1.20.0を使用します。Package ManagerのGit URLには次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/InputGate#input-gate-v1.0.0
```

利用側にasmdefがある場合は`InputGate.Runtime`を参照します。フォルダーを直接管理する場合だけ、`InputGate/`を`Assets/Modules/InputGate/`へ配置してください。

## 最小設定

1. `PlayerInput`が存在するGameObjectまたは同じ寿命のGameObjectへ`InputGateController`を追加します。
2. `Player Input`へ、実際に利用する`PlayerInput`を割り当てます。未指定時は同じGameObjectから取得します。
3. `Blocked Action Map Names`へ、停止対象だけを正確なMap名で指定します。UI操作を継続する場合はUI mapを含めません。
4. 停止が必要なownerが`TryAcquire`でleaseを取得し、`Dispose`で自分のleaseだけを解放します。

```csharp
if (inputGate.TryAcquire(out var lease, out var error))
{
    try
    {
        // Screen Transitionやpause menuが入力を止めている期間。
    }
    finally
    {
        lease.Dispose();
    }
}
```

`PlayerInput`は複数playerでAction Assetを複製するため、ControllerはMap名やGuidではなく実行中の`InputActionMap` instanceを所有します。同名Mapを持つ別playerは独立して制御できます。同じinstanceを別Controllerが重複所有しようとした場合は`OwnerAlreadyExists`です。

## 契約

- 最初のlease取得で、指定Map内のActionごとの有効状態を保存してMapを停止します。
- leaseが入れ子になっても停止は継続し、最後のlease解放だけが保存状態を復元します。
- 停止対象外のUI mapや別playerのAction Mapは変更しません。
- `InputGateLease.Dispose`は任意スレッドから呼べ、重複呼出しでも安全です。worker解放は次のController `Update`でまとめて反映します。
- 取得、状態参照、event購読はUnityメインスレッドから行います。`StatusChanged`中の新規取得は`Busy`です。
- 停止中に外部コードが対象Actionを有効化、または`PlayerInput.actions`を交換した場合は`ExternalActionStateChanged`でfail closedとなり、外部状態を上書きしません。
- Controllerの無効化・破棄では、外部変更がない健康な停止だけを復元し、全leaseを無効化します。
- Actionを停止すると、進行中Actionの`canceled` callbackが同期実行される場合があります。Controllerはその再入中の取得を拒否し、lifecycle変更後に古い状態を再通知しません。同じcallback内で再有効化された場合も、処理中の取得を失敗させてcleanup後の新しい所有世代へ切り替えます。
- 複数Mapの停止とActionごとの復元では、各書換え後に`PlayerInput.actions`の実instanceを再確認します。交換検知後は残りの旧Mapへ書き込みません。

## 含まないもの

- Legacy Input Manager、直接device polling、個別Action filtering。
- EventSystemやUI Toolkit pointer eventの遮断。
- rebind、device pairing、control scheme、`PlayerInput`の自動生成。
- 押下中入力の消費、buffer削除、再有効化後のAction発火抑制。
- Scene Flow、Screen Transition、Time Controlとの自動連携。
- singleton、network同期、永続化。

再有効化時のAction挙動はInput SystemのAction種別と`initialStateCheck`に従います。Input Gateはdevice状態を変更せず、入力を消費したことにはしません。

詳しい状態と失敗理由は[Documentation](Documentation~/index.md)、導入サンプルはPackage Managerの`Input Gate Basics`を参照してください。

利用条件は[LICENSE.md](LICENSE.md)、同梱物と外部依存は[Third-Party Notices.txt](Third-Party%20Notices.txt)を参照してください。
