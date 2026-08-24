# Fixed Point Basics

Sceneを開くと、signed Q16.16の生成、四則演算、overflow拒否を実Buttonで確認できます。

- `Set 1.5`: ratio `3 / 2`からraw `98304`を生成
- `Add -0.25`: raw `-16384`をchecked加算
- `Multiply 2`: 64-bit中間値でraw `163840`へ乗算
- `Divide 4`: 0方向丸めでraw `40960`、値`0.625`へ除算
- `Guard Overflow`: MaxValue + raw 1を拒否し、現在値を保持

Runtime moduleはUI Toolkitへ依存しません。UI Toolkitはこのサンプル表示だけに使います。
