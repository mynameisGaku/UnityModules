# Input Direction Quantizer Basics

Sceneを開くと、radial dead zone `0.10`、4-way・8-way方向量子化を実Buttonで確認できます。

- `Radial (0.06, 0.08)`: inclusive半径`0.10`をneutral `(0,0)`へ変換
- `8-way (0.9, 0.1)`: right `(1,0)`
- `8-way (-0.7, 0.7)`: up-left `(-1,1)`
- `4-way tie (0.5,-0.5)`: exact tieをvertical `(0,-1)`
- `Reject NaN`: 非有限入力を拒否して最後の成功方向を保持

5 Buttonは960×600で1列、640×360で3+2列に収まります。Runtime assemblyはUI Toolkitへ依存しません。
