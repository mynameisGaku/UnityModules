# Resource Cost Evaluator Basics

5種類の複数resource costを実Buttonで評価する設定済みSceneです。

1. `Payable`はgold 100→75、mana 40→30を返します。
2. `Shortage`はgoldを支払えてもmanaが7不足し、全体を支払不可として返します。
3. `Missing`は残量未登録のticket costをavailable 0・deficit 4として返します。
4. `Zero Cost`は残量未登録でもcost 0を支払可能として返します。
5. `Invalid`は重複cost IDを`DuplicateCostId`として拒否し、入力配列を変更しません。

UI Toolkitの実Button callbackを使い、960×600の1列と640×360の3+2列をPlayMode testで検証します。
