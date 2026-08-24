# Input Vector Exponential Smoother Basics

Sceneを開くと、明示step単位の2D exponential smoothingを実Buttonで確認できます。

- `Half step`: factor `0.50`で`(0,0)`から`(1,0)`へ1step進み`(0.50,0)`
- `Second step`: 同じtargetへ2step進み`(0.75,0)`
- `Diagonal`: `(0.60,0.80)`へ1step進み`(0.30,0.40)`
- `Factor 1`: `(-0.50,0.50)`へexact到達
- `Reject (2, 0)`: 範囲外targetを拒否して現在状態を保持

5 Buttonは960×600で1列、640×360で3+2列に収まります。Runtime assemblyはUI Toolkitへ依存しません。
