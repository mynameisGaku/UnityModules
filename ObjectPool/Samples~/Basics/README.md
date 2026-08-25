# Object Pool Basics

このsampleにscene fileは含まれません。空のSceneを1つ作り、空のGameObjectへ`ObjectPoolBasicsController`を追加してPlayしてください。prefab fieldが未設定なら、起動時にCube primitiveを生成してpoolへ登録します。

Play中の操作と表示は次のとおりです。

- `Spawn One` — instanceを1つ取り出し、controller直下へ配置します。続けて押すと横一列に並びます。
- `Release Oldest` — 最も古くspawnしたinstanceをidleへ返却します。返却済みinstanceは非活性化して保持されます。
- `Preload x10` — idleを10個まで事前生成します。直後の`Spawn One`は新規生成せずidleから取出すため、`Created`が増えず`Reused`だけが増えます。
- `Clear Idle` — idleを全て破壊します。

上部のstatus行には`Active`、`Idle`、`Created`、`Reused`、`Released`、`SpawnedTotal`、`Disposed`が表示されます。spawn→release→spawnを繰り返すと`Reused`だけが増え、`Created`が変わらないことで再利用を確認できます。

controllerはownerとして`PrefabPool`を1つ持ち、OnDestroyで`Dispose`します。取り出し中のinstanceはDispose後も生存するため、実projectではowner側の終了手順に合わせて先に返却してください。
