# Input Command Buffer Basics

Sceneを開くと、button edgeを明示simulation tickの短い期間だけ保持して消費する流れを実Buttonで確認できます。

- `Buffer Jump @100`: command id 1をtick 100へ記録
- `Advance @101`: 期限内のtick 101へ進め、Jumpを保持
- `Consume Jump`: 最も古いJumpをFIFOで消費
- `Buffer Dash @101`: command id 2をtick 101へ記録
- `Expire @104`: retention +2を超えるtick 104へ進め、Dashを期限切れにする

Runtime moduleはUI Toolkitへ依存しません。UI Toolkitはこのサンプル表示だけに使います。
