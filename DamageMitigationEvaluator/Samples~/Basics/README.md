# Damage Mitigation Evaluator Basics

5種類のdamage軽減を実Buttonで評価する設定済みSceneです。

1. `Flat`は100から固定25を軽減して75を返します。
2. `Ratio`は100へ25%軽減を適用して75を返します。
3. `Ordered`は100へflat 20、続いてratio 25%を適用して60を返します。
4. `Clamp`は100へflat 120を要求し、実適用100・最終0として返します。
5. `Invalid`は重複layer IDを`DuplicateLayerId`として拒否し、入力配列を変更しません。

UI Toolkitの実Button callbackを使い、960×600の1列と640×360の3+2列をPlayMode testで検証します。
