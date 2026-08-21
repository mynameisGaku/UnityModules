# Input Vector Weighted Mixer Basics

Sceneを開くと、複数2D sourceの正規化加重平均を実Buttonで確認できます。

- `Equal blend`: `(1,0)×1`と`(0,1)×1`から`(0.50,0.50)`
- `Player 75%`: weight `0.75 / 0.25`から`(0.75,0.25)`
- `Zero ignored`: 2件中zero weightを計算から外しactive count 1
- `Empty`: 0件からneutralな成功結果
- `Reject weight`: 2件目のweight `1.50`をindex 1付きで拒否

5 Buttonは960×600で1列、640×360で3+2列に収まります。Runtime assemblyはUnity APIへ依存しません。
