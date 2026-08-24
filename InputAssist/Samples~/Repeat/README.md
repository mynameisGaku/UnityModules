# Input Repeat Basics

Sceneを開くと、pressed状態と明示simulation tickから初回・保持repeatを計算する流れを実Buttonで確認できます。

- `Press @100 · Initial`: 押下edgeで初回trigger 1
- `Hold @102 · Wait`: initial delay 3 tick前なのでtrigger 0
- `Hold @103 · Repeat`: delay境界で最初のrepeat 1
- `Hold @110 · Catch Up`: tick 105、107、109で未発行だったrepeat 3
- `Release @111`: 解放edgeを報告してrepeat状態を破棄

Runtime moduleはUI Toolkitへ依存しません。UI Toolkitはこのサンプル表示だけに使います。
