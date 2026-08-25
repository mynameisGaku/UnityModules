# Changelog

## [1.0.0] - 2026-08-26

### Added

- 1つのprefabの生成と再利用を所有する`PrefabPool`。spawn、release、preload、trim、disposeを明示的なownerが操作する。
- idleからの取出し順序を`PoolReuseOrder`のLifo / Fifoで選ぶ契約。Lifoは最後の返却を、Fifoは最古の返却を再利用する。
- 同時アクティブ数の上限とidle保持数の上限を持つ`PrefabPoolSettings`。全field比較による値等価と既定値`Default`を提供する。
- 失敗理由を区別する`PoolError`。null参照、管理外instance、二重返却、アクティブ上限、破棄済みpool、外部破壊、負のpreload / trim数を報告する。
- 所属pool、世代番号、返却済み状態を読み取れる`PooledInstanceMarker`。
- `ActiveCount`、`IdleCount`、`CreatedTotalCount`、`ReusedTotalCount`、`ReleasedTotalCount`、`SpawnedTotalCount`の統計カウンタ。
- 空のSceneで動作確認できるObject Pool Basics sample。

### Boundaries

- アクティブ上限の判定は新規生成が必要な場合だけ行う。idle再利用は常に許可され、事前preload後に上限を超える取出しがあり得る。
- `MaximumActiveCount=0`は無制限を意味し、上限なしで新規生成を続ける。
- poolは取り出し中のinstanceの寿命を保証しない。外部で破壊されたinstanceは返却時に`InstanceExternallyDestroyed`として報告する。
- `Dispose`はidleだけを破壊する。取り出し中のinstanceは生存し、後から返却すると`PoolDisposed`になる。
- prefabの破壊済み判定は`Instantiate`した同一app domain内の挙動に従い、scene unload時の一括破壊を個別に検出しない。
- singleton、自動GameObject生成、自動縮小、自動破棄、main thread以外からの操作保証は含まない。
