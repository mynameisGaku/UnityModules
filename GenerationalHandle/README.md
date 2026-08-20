# Generational Handle

明示capacity内のslotを決定論的に割り当て、解放後に残った古い参照をgenerationで拒否するEngine非依存moduleです。

runtime object、simulation entity、短命なresourceなどへ小さなhandleを渡しつつ、同じslotを再利用した新しいentryとの取り違えを防ぎたい場合に使います。

## 導入

Package Managerの`Add package from git URL...`へ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/GenerationalHandle#generational-handle-v1.0.0
```

または`GenerationalHandle`を`Assets/Modules/`へcopyします。

## 最小例

```csharp
var pool = new GenerationHandlePool(128);

if (!pool.TryAcquire(out var first, out var acquireError)) return;
pool.Release(first);

pool.TryAcquire(out var reused, out _);
// first  = Slot 0 / Generation 1
// reused = Slot 0 / Generation 2
// pool.IsActive(first)  == false
// pool.IsActive(reused) == true
```

## 固定契約

- slotは0開始で、未使用slotを昇順に割り当てる
- 解放済みslotから常に最小番号を選ぶ
- generationは1開始で、有効handleを解放した時だけ増える
- 解放済みまたは別generationのhandleは`StaleHandle`
- defaultまたは未割当slotのhandleは`InvalidHandle`
- generationが`uint.MaxValue`のslotは解放後にretireし、0へwrapしない
- capacity到達時は既存stateを変えず`CapacityReached`

同じcapacityと同じAcquire／Release列から、同じhandle列を再現します。Poolはthread-safeではありません。1つの明確なownerから操作してください。

## engine境界

Runtime assemblyはUnityEngineを参照しません。`GenerationHandle`をGameObjectやcomponentへ対応付けるregistryは利用側が所有します。scene unload、save、network同期などの寿命規則も利用側の責務です。

## 他moduleとの組合せ

Replay Tapeのcommand payloadへslotとgenerationを記録すれば、再生時に古い参照を検出できます。Canonical Payloadへはslotをint32、generationをuint32として明示順で書けます。どのmoduleにもhard dependencyしません。

## 非目標

- global registry、singleton、GameObject自動検索
- objectやpayloadの保持
- persistence、network authority、distributed ID
- thread-safe allocation、lock-free pool
- generationの強制reset、wrap後の再利用

## Sample

Package Managerから`Generational Handle Basics`をimportすると、設定済みSceneで`0:1`の解放、`0:2`への再利用、古い`0:1`の非破壊拒否を確認できます。
