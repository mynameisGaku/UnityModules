# Time Control

Time Controlは、複数の利用者が要求する相対倍率をleaseとして集約し、ゲーム全体の`Time.timeScale`へ反映する小さな時間制御モジュールです。導入の最短手順はパッケージ直下の [README](../README.md) を参照してください。

動作確認済み: **Unity 6000.5.7f1** / Windows / .NET Standard 2.1

## 導入

Package Managerの **Add package from git URL** に次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/TimeControl#time-control-v1.0.0
```

利用側のasmdefから `TimeControl.Runtime` を参照します。パッケージは同梱Basics sampleのため、Unity組込みの`com.unity.modules.uielements` 1.0.0を宣言します。第三者packageへの依存はなく、Runtimeの時間制御APIはUI Toolkit型を公開しません。

## 所有と寿命

`Time.timeScale`を変更するSceneまたはゲーム進行ownerが、`TimeControlController`を明示的に1つ所有します。

1. 最初に有効になったControllerが現在の`Time.timeScale`を基準値として取得します。
2. 利用者は必要な期間だけ`TimeScaleLease`を保持します。
3. Controllerは有効なleaseの最小倍率を基準値へ掛け、`Time.timeScale`へ反映します。
4. 最後のleaseを破棄すると基準値へ戻ります。
5. Controllerの無効化または破棄では、競合がない限り基準値を復元し、発行済みleaseを無効にします。

2つ目のControllerは有効なownerから所有権を奪いません。先のownerが寿命を終えた後は、待機していたControllerへの次の`TryAcquire`が新しい所有期間を開始できます。所有権を自動移送したり、古いleaseを引き継いだりはしません。Time Controlはglobal singletonや自動生成GameObjectを作りません。Sceneをまたぐ場合のGameObject寿命は利用側が管理します。

## 公開API

```csharp
if (!controller.TryAcquire(0.25f, out TimeScaleLease slowLease, out TimeControlError error))
{
    Debug.LogError($"Slow motionを開始できません: {error}");
    return;
}

try
{
    // slowLeaseを必要とする処理。
}
finally
{
    slowLease.Dispose();
}
```

公開する型は次の4つです。

| 型 | 役割 |
|---|---|
| `TimeControlController` | `Time.timeScale`の所有、lease発行、集約、競合検出、終了処理 |
| `TimeScaleLease` | 1利用者の相対倍率と解放責任を表す`IDisposable` |
| `TimeControlStatus` | 基準値、最小倍率、実効値、lease数、制御可否、失敗理由のsnapshot |
| `TimeControlError` | 取得失敗または制御停止の理由 |

`TimeControlController.TryAcquire(float, out TimeScaleLease, out TimeControlError)`は、成功時だけ有効なleaseを返します。`Status`は現在の`TimeControlStatus`、`IsControlling`は所有権を持って健全に制御できるかを返します。`StatusChanged`は状態snapshotを通知します。

`TimeScaleLease.Multiplier`は要求した倍率、`IsActive`はそのleaseがまだ現在のownerへ影響できるかを表します。`Dispose`は繰り返し呼べます。Controllerの世代が終わった後のstale leaseを破棄しても、新しいownerや新しいleaseには影響しません。

## 倍率の決定

倍率は有限の0以上100以下です。実効値は次の規則で決まります。

```text
有効なleaseがない: 基準値
有効なleaseがある: 基準値 × 最小のlease倍率
```

反映後の実効値も0以上100以下である必要があります。この範囲を外れる要求は状態を変更せず拒否します。

例として、基準値1で`2`、`0.25`、`0`のleaseを順に取得すると、実効値は`2 → 0.25 → 0`です。`0`、`0.25`、`2`の順に解放すると、`0.25 → 2 → 1`へ戻ります。最小倍率を使うためpauseは常に優先され、fast-forwardはそれより小さいleaseがない場合だけ有効です。

## 状態と失敗理由

`TimeControlStatus`は次を読み取れます。

| property | 意味 |
|---|---|
| `IsControlling` | このControllerが現在の`Time.timeScale` ownerか |
| `Error` | 現在の失敗理由。健全な場合は`None` |
| `BaselineTimeScale` | owner取得時に保存した基準値 |
| `EffectiveMultiplier` | 有効なleaseから選ばれた最小倍率。leaseなしは1 |
| `EffectiveTimeScale` | 最後に書込み後の一致を確認できた値。異常検出時は読取った外部値 |
| `ActiveLeaseCount` | 現在のownerへ影響するlease数 |

`TimeControlError`の意味は次のとおりです。

| 値 | 意味 |
|---|---|
| `None` | 失敗なし |
| `InvalidMultiplier` | 倍率が有限の0以上100以下ではない |
| `EffectiveTimeScaleOutOfRange` | 基準値との積が実効値の上限100を超える |
| `MainThreadRequired` | `TryAcquire`をUnityのメインスレッド以外から呼んだ |
| `Busy` | 状態通知中に新しいleaseを取得しようとした |
| `OwnerAlreadyExists` | ほかの有効なControllerが`Time.timeScale`を所有している |
| `ControllerUnavailable` | Controllerが無効、破棄済み、未初期化、または所有期間を終了済み |
| `ApplicationExiting` | Play終了またはアプリケーション終了中 |
| `ExternalTimeScaleChanged` | 制御中に外部から`Time.timeScale`が変更された |
| `TimeScaleWriteFailed` | Unityの値を読取り、書込み、または書戻し確認できなかった |

## threadと通知

`TryAcquire`はUnityのメインスレッドから呼びます。`TimeScaleLease.Dispose`は別threadから呼べます。別threadでは解放要求だけを登録し、`Time.timeScale`の変更と`StatusChanged`は次のメインスレッド更新で行います。

`StatusChanged`の通知中に`TryAcquire`を呼ぶと`Busy`です。通知中の`Dispose`は遅延され、通知が終わった後に失われず反映されます。通知先の例外はほかの通知先と制御本体から分離されます。通知は過去の可変状態ではなく、その時点の`TimeControlStatus` snapshotとして扱ってください。

## 外部writerとの競合

Controllerは制御中に`Time.timeScale`が期待値と異なることを検出すると、`ExternalTimeScaleChanged`でfail-closed停止します。外部が設定した値は上書きせず、既存leaseを無効にします。停止したControllerは不活性のまま維持されるため、writerを1つへ整理した後にControllerを無効化し、再び有効化してください。

この方針は、毎frame互いの値を上書きし続ける不安定な競合を避けます。Time Controlを導入した範囲では、ほかの実装が`Time.timeScale`へ直接書かず、leaseを取得する形へまとめてください。

## 終了処理

健全なControllerを無効化または破棄すると基準値を復元し、全leaseを無効にします。外部変更を検出済みの場合は、その外部値を保持して上書きしません。Play Mode終了とアプリケーション終了では、新しい取得を拒否し、可能な範囲で同じ終了処理を行います。終了済みownerのleaseを後から破棄しても安全です。

## 非目標

- timer、cooldown、期限付きhit-stop、leaseの自動失効。
- `Time.fixedDeltaTime`の取得、変更、復元。
- input map、EventSystem、音声、Animator、物理設定の切替。
- network同期と永続化。
- Scene FlowまたはScreen Transitionとの自動連携。
- global singleton、常駐Manager、自動生成GameObject。
- 利用者をまたぐ公開`ReleaseAll`。

時間制限が必要な場合は、leaseを所有する利用側が非スケール時間で期限を管理し、自分のleaseだけを破棄します。pauseに連動させる入力、音声、animationなども、それぞれのownerが`StatusChanged`を参照して明示的に制御してください。

## 検証

EditModeテストは倍率検証、最小倍率の集約、上限、stale lease、重複解放を決定論的に確認します。PlayModeテストは実際の`Time.timeScale`を使い、owner競合、外部writer検出、無効化・破棄時の復元、通知再入、別threadからの解放を確認します。

**Time Control Basics** のimport済みsampleテストは、実Button callback、入れ子順序、pause中のUI応答、スケール時間と非スケール時間の差、`Release Owned`、必須表示要素を実時間deadline付きで確認します。後段のMono PlayerとIL2CPP Player gateでは時系列JSONと画面captureを使い、実行環境でも同じ順序と視覚差を確認します。
