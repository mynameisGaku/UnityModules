# Input Radial Dead Zone Basics

Sceneを開くと、inner `0.10`・outer `1.00`のradial dead zone補正を実Buttonで確認できます。

- `Inner (0.06, 0.08)`: inclusiveなmagnitude `0.10`をzeroへ補正
- `Mid (0.55, 0)`: magnitudeを`0.50`へ線形remap
- `Outer (0, 1)`: inclusiveなouter境界をunitへ補正
- `Over-range (3, 4)`: 方向を保って`(0.60, 0.80)`へ正規化
- `Reject NaN`: 非有限入力を拒否して最後の成功出力を保持

5 Buttonは960×600で1列、640×360で3+2列に収まります。Runtime assemblyはUI Toolkitへ依存しません。
