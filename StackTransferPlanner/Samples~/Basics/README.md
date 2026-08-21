# Stack Transfer Planner Basics

同じitem種のsource／destination stackを実Buttonで計画する設定済みSceneです。

1. Fullは10 available・10 roomに対する9要求を全量移します。
2. Partialは8要求をdestination room 7で制限します。
3. Source limitは8要求をsource available 3で制限します。
4. Destination limitは8要求をdestination room 4で制限します。
5. Zero requestは全明細をdelta 0で返します。

UI Toolkitの実Button callbackを使い、960×600の1列と640×360の3+2列をPlayMode testで検証します。
