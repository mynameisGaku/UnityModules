# Input Vector Direction Limiter Basics

Sceneを開くと、明示step単位の2D方向回転制限を実Buttonで確認できます。

- `Turn 45°`: `(1,0)`から`(0,1)`へ45°回転
- `Turn twice`: 同じtargetへ2 step進みexact到達
- `Clockwise`: magnitude `0.5`のtargetへ時計回り45°回転
- `Opposite`: 180°tieを反時計回りで解決
- `Reject (0.8, 0.8)`: unit circle外targetを拒否して現在stateを保持

5 Buttonは960×600で1列、640×360で3+2列に収まります。Runtime assemblyはUI Toolkitへ依存しません。
