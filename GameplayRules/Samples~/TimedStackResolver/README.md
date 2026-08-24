# Timed Stack Resolver Basics

時限effectの現在状態と追加状態を実Buttonで解決する設定済みSceneです。

1. Add + Refreshは2/50へ2/30を適用し、stackを3へclampしてdurationを30へ更新します。
2. Add + Extendは1/20へ1/15を適用し、stackを2、durationを30へclampします。
3. Maximumは3/40と2/60から3/60を選びます。
4. Replaceは3/40を1/10へ置き換えます。
5. Inactive → Activeは0/0へ2/25を適用してactive化します。

表示の`STACK CLAMPED`と`DURATION CLAMPED`で、上限が実際に適用されたかを確認できます。UI Toolkitの実Button callbackを使い、960×600の1列と640×360の3+2列をPlayMode testで検証します。
