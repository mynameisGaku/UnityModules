# Object Pool 1.0.0

Object Poolは、1つのprefabのinstanceをidleとして保持し再利用することで`Instantiate`と`Destroy`の発生を減らします。導入と最短のSample手順はpackage直下の[README](../README.md)を参照してください。

## 必要環境

- Unity 6000.5.7f1以降。
- Runtime参照: `ObjectPool.Runtime`。
- 依存packageなし。依存Unity組込みmoduleなし。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/ObjectPool#object-pool-v1.0.0
```

## ownership

`PrefabPool`は寿命が明確なownerだけが生成する部品です。static singleton、service locator、自動GameObject生成、自動Scene探索は提供しません。wave管理、weapon制御、演出制御など、instanceの要求元を束ねるcomponentがpoolをnewして持ち、不要になった時点で`Dispose`してください。

複数ownerが同じprefabへ個別のpoolを持つこと自体は問題ありません。ただしidleはpool間で共有されないため、同じprefabのidleが複数箇所に滞留します。必要ならowner間で1つのpool instanceを渡してください。

全public操作はUnity main threadから呼びます。main thread以外からの呼出しは保証しません。

## poolの作成

```csharp
var settings = new PrefabPoolSettings(
    maximumActiveCount: 100,
    maximumIdleCount: 32,
    initialPreloadCount: 8,
    reuseOrder: PoolReuseOrder.Fifo);
var pool = new PrefabPool(prefab, settings);
```

constructorはGameObjectを生成しません。事前生成が必要なら`PreloadInitial`か`Preload`を明示的に呼びます。省略時は`PrefabPoolSettings.Default`（上限なし、最大idle 128、preload 0、Lifo）です。

設定は変更不能です。負のcountや未定義の`PoolReuseOrder`は`ArgumentOutOfRangeException`で失敗します。等価比較は全field比較で行われ、`==`と`!=`が使えます。

## spawn契約

| 条件 | 動作 |
| --- | --- |
| idleに有効なinstanceがある | `ReuseOrder`に従い取り出す。位置、回転、親、活性状態を適用。`Generation++` |
| idleが空でアクティブ数が上限未満 | `Object.Instantiate`で新規生成 |
| idleが空でアクティブ数が上限以上 | `ActiveLimitReached`で失敗 |
| 破棄済み | `PoolDisposed`で失敗 |

取り出したinstanceには`PooledInstanceMarker`が付いています。手動で外したり、markerだけを破壊したりしないでください。

### アクティブ上限の適用範囲

上限判定は新規生成が必要な場合だけ行います。idle再利用は常に許可されます。そのためpreload後にidle在庫が多い状態では、取出しによってアクティブ数が`MaximumActiveCount`を超えることがあります。この上限は「新規GameObjectの増殖防止」であり、同時存在数の厳密な保証ではありません。厳密な同時数管理が必要なら、取出し側で`ActiveCount`を確認してからspawnしてください。

### 再利用順序

| `PoolReuseOrder` | 取り出すinstance | 向いている用途 |
| --- | --- | --- |
| `Lifo`（既定） | 最後に返却されたもの | 直前まで使っていたinstanceのcache再利用 |
| `Fifo` | 最も古く返却されたもの | 全instanceを均等に回す運用、見た目の分散 |

返却順序と取り出し順序は決定論的です。乱数や時刻に依存しません。

## release契約

`TryRelease(GameObject instance, out PoolError error)`は次の順で判定します。

| 判定 | 結果 |
| --- | --- |
| C#参照としてnull | `NullInstance` |
| pool破棄済み | `PoolDisposed` |
| Unity破壊済み（外部Destroy後） | `InstanceExternallyDestroyed` |
| marker不在または`PoolId`不一致 | `ForeignInstance` |
| markerが返却済み | `AlreadyReleased` |
| 上記以外 | 成功。非活性化してidleへ返却 |

成功時、idleが`MaximumIdleCount`を超える分は`Object.Destroy`します。つまり`MaximumIdleCount=0`なら毎回破壊し、cache効果は得られません。

返却時に位置や親は変更しません。非活性化だけを行います。

## 整理契約

- `Preload(int count, out int createdCount, out PoolError error)` — 既存idleと合わせて`MaximumIdleCount`まで生成する。打ち切りでも成功（true）で、実績を`createdCount`へ返す。負値は`NegativePreloadCount`。
- `PreloadInitial(out int createdCount, out PoolError error)` — `Settings.InitialPreloadCount`でのpreload。
- `TrimIdle(int count, out PoolError error)` — 古くからあるidleから指定数を破壊する。戻り値intが実績数。負値は`NegativeTrimCount`。
- `ClearIdle()` — idleを全破壊し、破壊数を戻り値で返す。
- `Dispose()` — idleを全破壊し、操作を禁止する。冪等。

「古い」はList先頭、すなわち最も長くidleとして残っているものを意味し、`ReuseOrder`設定に依存しません。

## dispose契約

`Dispose`はidleだけを破壊します。取り出し中のinstanceは生存し、sceneへ残ります。それらの返却は`PoolDisposed`で失敗します。破壊したいなら、`Dispose`前に全ownerへ返却させるか、owner自身が残存instanceを破壊してください。

破棄済みpoolに対する`TrySpawn`、`TryRelease`、`Preload`、`TrimIdle`は`PoolDisposed`で失敗します。`ClearIdle`は0を返します。

## 統計

| member | 意味 |
| --- | --- |
| `ActiveCount` | 取り出されているinstance数 |
| `IdleCount` | idleとして保持している数。外部破壊済みの死んだentryは除外した実数 |
| `CreatedTotalCount` | 新規生成累積。preloadを含む |
| `ReusedTotalCount` | 再利用累積 |
| `ReleasedTotalCount` | 返却成功累積。preloadとtrimによる破壊は含まない |
| `SpawnedTotalCount` | `CreatedTotalCount + ReusedTotalCount` |

統計は単調増加または現在値の報告のみを行い、減算やreset APIは提供しません。

## errors

| error | 意味 |
| --- | --- |
| `None` | 成功 |
| `NullInstance` | C#参照としてのnull引数 |
| `ForeignInstance` | marker不在、または他pool発のinstance |
| `AlreadyReleased` | 二重返却 |
| `ActiveLimitReached` | 新規生成が必要だがアクティブ上限に到達 |
| `PoolDisposed` | 破棄済みpoolへの操作 |
| `InstanceExternallyDestroyed` | 管理下instanceの外部破壊 |
| `NegativePreloadCount` | 負のpreload数 |
| `NegativeTrimCount` | 負のtrim数 |

失敗時、out引数のinstanceはnull、count系は0へ設定されます。例外はsettings検証（constructor）だけです。操作系の失敗は例外ではなく`PoolError`で報告します。

## 非目標

singleton、自動GameObject生成、自動縮小、自動破棄、非同期処理、main thread以外からの操作、複数prefab混在、pool間共有、音声やUIなど特定domainの再生管理、instanceの永続化は1.0.0の対象外です。

## 検証方針

EditMode testでは、同一instance再取得（Lifo）、Fifo順序、アクティブ上限、外部破壊検知、他pool混線、二重release、preloadの負値と打ち切り、trim / clearの実績数、dispose後挙動、settings等価性と不正値throw、統計カウンタ整合を検証します。PlayMode sample testではcontrollerの各操作メソッドを直接呼び、統計変化とGUI表示用status文字列を検証します。

本packageは[MIT License](../LICENSE.md)です。外部依存は[Third-Party Notices](../Third-Party%20Notices.txt)を参照してください。
