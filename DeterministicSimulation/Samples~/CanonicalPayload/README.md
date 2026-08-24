# Canonical Payload Basics

Sceneを開くと、明示schemaの値をcanonical bytesへEncodeし、同じ順序でDecodeできます。

- `Encode Schema`: `int32 -10`、`single 1.25`、`string 移動🚀`、3 bytes、`bool true`を30 bytesへ変換
- `Decode Payload`: 全fieldとpayload末尾を確認
- `Corrupt Copy`: string lengthだけを壊し、reader位置を進めず拒否することを確認
- `Rebuild Same`: 同じ入力から同一byte列を再構築
- `Reset`: 有効な空payloadへ戻す

Runtime moduleはUI Toolkitへ依存しません。UI Toolkitはこのサンプル表示だけに使います。
