# Input Vector Slew Limiter Basics

最大vector差`0.25 / step`を実Buttonで確認します。

- `1 Step`: `(0,0)`から`(1,0)`へ1 step進み`(0.25,0)`
- `2 Steps`: 同じtargetへ2 step進み`(0.50,0)`
- `Diagonal`: `(0.6,0.8)`方向へ`(0.15,0.20)`
- `Reach`: 上限内の`(0.1,0.1)`へexact到達
- `Reject`: 範囲外targetを拒否して現在値を保持

5 Buttonは960×600で1列、640×360で3+2列です。RuntimeはUI Toolkitへ依存しません。
