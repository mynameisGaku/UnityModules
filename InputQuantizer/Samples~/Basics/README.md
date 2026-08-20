# Input Quantizer Basics

Sceneを開くと、dead zone `0.10`、片側8段階の1軸量子化を実Buttonで確認できます。

- `Dead Zone +0.05`: inclusive dead zone内をcommand `0`へ変換
- `Quantize +0.55`: 正入力をcommand `+4`へ変換
- `Quantize -0.55`: 負入力をcommand `-4`へ対称変換
- `Clamp +2`: 入力を`+1`へclampしてcommand `+8`を生成
- `Reject NaN`: 非有限入力を拒否し、最後の成功値を保持

Runtime moduleはUI Toolkitへ依存しません。UI Toolkitはこのサンプル表示だけに使います。
