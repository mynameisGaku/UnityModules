# Input Vector Response Curve Basics

Sceneを開くと、入力`(0.30, 0.40)`・magnitude `0.50`へ4種類のresponse curveを適用できます。

- `Linear`: magnitude `0.50`、出力`(0.30, 0.40)`
- `Squared`: magnitude `0.25`、出力`(0.15, 0.20)`
- `Cubic`: magnitude `0.125`、出力`(0.075, 0.10)`
- `Smooth Step`: 中央値を維持してmagnitude `0.50`
- `Reject (1, 1)`: 単位円外入力を拒否して最後の成功出力を保持

5 Buttonは960×600で1列、640×360で3+2列に収まります。Runtime assemblyはUI Toolkitへ依存しません。
