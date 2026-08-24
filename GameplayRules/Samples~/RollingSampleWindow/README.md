# Rolling Sample Window Basics

容量3のFIFO窓を設定済みのSceneです。

1. `Add 10`、`Add 20`、`Add 30`で窓を満杯にします。
2. `Add 40 · evict 10`でoldest 10の退避と20・30・40のwindowを確認します。
3. snapshotはmin 20、max 40、mean 30、oldest 20、newest 40です。
4. `Clear window`で容量3を保った空状態へ戻します。

UI Toolkitの実Button callbackを使い、960×600の1列と640×360の3+2列をPlayMode testで検証します。
