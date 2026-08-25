# オブジェクト再利用（Object Pool）

## 30秒で分かる説明

Object Poolは、弾、エフェクト、敵のような繰り返し出現するprefabの`Instantiate`と`Destroy`を減らすRuntimeモジュールです。使わなくなったinstanceをpool内部にidleとして保持し、次の要求では破壊せずに再利用します。spawnとreleaseはすべて明示的な呼出しで、poolを作ったownerがいつ生成し、いつ返却し、いつ捨てるかを決めます。

自動起動するsingletonや自動縮小はありません。wave管理やweapon制御のように寿命が明確な1つのownerが`PrefabPool`をnewして保持し、不要になった時点で`Dispose`します。

対応versionはUnity 6000.5.7f1以降です。

## できること

- `TrySpawn`でidleの再利用または上限内の新規生成を行い、`TryRelease`でidleへ返却する。
- 再利用順序を`PoolReuseOrder.Lifo`（最後の返却を優先）と`Fifo`（最古の返却を優先）で選ぶ。
- `MaximumActiveCount`で新規生成に上限を付け、超過要求を例外ではなく`PoolError.ActiveLimitReached`として受け取る。
- `MaximumIdleCount`で保持数を制限し、超過分は返却時に自動破壊する。`Preload`で事前生成、`TrimIdle` / `ClearIdle`で明示整理できる。
- 失敗理由がすべて`PoolError`列挙で分かる。null参照、管理外instance、二重返却、外部破壊、破棄済みpoolを区別する。
- 所属pool、世代番号、返却済み状態を`PooledInstanceMarker`経由で読み取れる。
- 統計カウンタ（生成累積、再利用累積、返却累積、アクティブ数、idle数）でpoolの健康状態を確認できる。

## 使わない方がよい場合

生成コストが問題にならない低頻度な配置には過剰です。通常のUI要素や1回きりの演出は直接`Instantiate`してください。

また、このmoduleは以下を行いません。これらが必要なら別の仕組みを用意してください。

- idleの自動縮小、自動破棄、scene unloadからの自動救済。
- pool間の共有や、複数prefabを1つのpoolへ混在させる構成。1 pool = 1 prefabです。
- main thread以外からの操作、非同期破壊、instanceの永続化。

## 3分で試す

1. Unity 6000.5.7f1以降のprojectで、Package Managerの`Add package from git URL...`へ次を指定します。

   ```text
   https://github.com/mynameisGaku/UnityModules.git?path=/ObjectPool#object-pool-v1.0.0
   ```

2. Package ManagerのSamplesから`Object Pool Basics`をimportします。
3. 空のSceneを作り、空のGameObjectを追加して`ObjectPoolBasicsController`を取り付けます。prefab未設定なら起動時にCube primitiveを使います。
4. Playして`Spawn One`を数回押し、その後`Release Oldest`、`Preload x10`、`Clear Idle`を押します。画面に統計カウンタが出ます。

利用側にasmdefがある場合は`ObjectPool.Runtime`を参照します。依存packageはありません。

## 最小コード

```csharp
using ObjectPool;
using UnityEngine;

public sealed class BulletOwner : MonoBehaviour
{
    private PrefabPool _pool;

    private void Awake()
    {
        _pool = new PrefabPool(_bulletPrefab, new PrefabPoolSettings(
            maximumActiveCount: 100,
            maximumIdleCount: 32,
            initialPreloadCount: 8,
            reuseOrder: PoolReuseOrder.Fifo));
        _pool.PreloadInitial(out _, out _);
    }

    public void Fire(Vector3 position, Quaternion rotation)
    {
        if (_pool.TrySpawn(position, rotation, null, out var bullet, out var error))
        {
            // bulletを動かす。役目が終わったら返却する。
            _pool.TryRelease(bullet, out _);
        }
        else
        {
            Debug.LogError($"spawn失敗: {error}");
        }
    }

    private void OnDestroy()
    {
        _pool?.Dispose();
    }

    [SerializeField] private GameObject _bulletPrefab;
}
```

ownerの`OnDestroy`で`Dispose`します。idleは破壊されますが、取り出し中のinstanceは生存するため、必要ならowner側で先に返却してください。

## 実行するとどうなるか

`Spawn One`を押すたびにSceneへCubeが出現し、統計の`Active`と`SpawnedTotal`が増えます。`Release Oldest`を押すと最も古いCubeが非活性化され、`ReleasedTotal`が増えて`Active`が減ります。もう一度`Spawn One`を押すと同じCubeが元の位置に戻り、`Reused`が増えます。Consoleへ新規生成のログは出ません。ここがpool導入の効果です。

`Preload x10`は先に10個のidleを用意します。直後のspawnは新規`Instantiate`を呼ばずidleから取出すため、`Created`は増えず`Reused`だけが増えます。

## よくある問題

- **spawnしたinstanceを`Destroy`してしまった** — pool管理外の破壊です。その参照を`TryRelease`すると`InstanceExternallyDestroyed`になります。instanceの破壊はpoolへ任せ、外すときは`TryRelease`を呼んでください。
- **別のpoolから借りたinstanceを返却した** — `ForeignInstance`になります。markerの`PoolId`で照合しているため、混線は検出できます。
- **同じinstanceを二度返却した** — 二度目は`AlreadyReleased`です。返却成功後に参照を持ち回さないでください。
- **`MaximumActiveCount=2`なのに3個出したい** — idleが空でアクティブ数が上限に達していると`ActiveLimitReached`で失敗します。上限を上げるか、返却を待つか、`0`で無制限にしてください。idle再利用は常に許可されるため、preload済みの場合は上限を超える取出しがあり得ます。
- **`Dispose`したのにinstanceが残っている** — 仕様です。`Dispose`はidleだけを破壊します。取り出し中のinstanceへの返却は`PoolDisposed`で失敗します。
- **他モジュールとの違い** — AudioControlのvoice poolのような音声再生管理は含みません。純粋なprefab instanceの再利用だけを行います。

## 詳しい契約

### 所有と寿命

`PrefabPool`はstatic singleton、自動GameObject生成、自動登録を作りません。ownerがconstructorで生成し、`IDisposable.Dispose`で終了させます。全public操作はUnity main threadから呼んでください。1 pool = 1 prefabです。

### spawn契約

| 条件 | 動作 |
| --- | --- |
| idleに有効なinstanceがある | `ReuseOrder`に従って取り出し、位置/親/活性状態を適用する。`Generation++`、`ReusedTotal++` |
| idleが空でアクティブ数が上限未満 | 新規`Instantiate`。`CreatedTotal++` |
| idleが空でアクティブ数が上限以上 | `ActiveLimitReached`で失敗。instanceはnull |
| 破棄済み | `PoolDisposed`で失敗 |

`MaximumActiveCount=0`は無制限です。上限判定は新規生成が必要なときだけ行うため、idle再利用によって一時的にアクティブ数が設定を超えることがあります（preload後に発生し得ます）。

### release契約

| 入力 | 結果 |
| --- | --- |
| C# null参照 | `NullInstance` |
| 外部で破壊されたinstance | `InstanceExternallyDestroyed` |
| markerなし、または`PoolId`不一致 | `ForeignInstance` |
| 返却済みinstance | `AlreadyReleased` |
| 破棄済みpool | `PoolDisposed` |
| 正常 | 非活性化してidleへ返却。idleが`MaximumIdleCount`を超える分は破壊 |

### 整理契約

- `Preload(count, out createdCount, out error)` — 既存idleと合わせて`MaximumIdleCount`までで打ち切り、実績を`createdCount`で返す。負値は`NegativePreloadCount`。
- `PreloadInitial(out createdCount, out error)` — `Settings.InitialPreloadCount`でpreloadする便利メソッド。自動生成はしないためownerが明示的に呼びます。
- `TrimIdle(count, out error)` — 古いidleから指定数を破壊し、実績数を戻り値intで返す。負値は`NegativeTrimCount`。
- `ClearIdle()` — idleを全破壊し、破壊数を戻り値で返す。
- `Dispose()` — idleを全破壊し、以降の操作を`PoolDisposed`にする。冪等。

### 公開API

- `PrefabPool` — constructor `(GameObject prefab, PrefabPoolSettings settings = null)`。`TrySpawn` × 2、`TryRelease`、`Preload`、`PreloadInitial`、`TrimIdle`、`ClearIdle`、`Dispose`。
- `PrefabPoolSettings` — `MaximumActiveCount`、`MaximumIdleCount`、`InitialPreloadCount`、`ReuseOrder`を持つ変更不能class。全field比較による`Equals` / `==` / `!=`と`Default`を提供する。負値や未定義enum値はconstructorが`ArgumentOutOfRangeException`を送出する。
- `PoolReuseOrder` — `Lifo`、`Fifo`。
- `PooledInstanceMarker` — `PoolId`、`Generation`、`IsReleased`の読み取り専用view。
- `PoolError` — 上表の失敗理由。

### テスト範囲

EditMode testがspawn→release→spawnの同一instance再取得、Fifo順序、アクティブ上限、外部破壊検知、他pool混線、二重release、preload打ち切り、trim/clear実績数、dispose後挙動、settings等価性と不正値、統計整合を検証します。PlayMode sample testがcontroller操作と統計表示を検証します。

詳細な契約は[Documentation](Documentation~/index.md)を参照してください。

本packageは[MIT License](LICENSE.md)です。外部依存は[Third-Party Notices.txt](Third-Party%20Notices.txt)を参照してください。
