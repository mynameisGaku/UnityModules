# Drawing Basics

描けるものを一通り出すサンプルです。

## 使い方

1. Package Manager から **Drawing Basics** を Import する。
2. Import先の **DrawingBasics** Scene を開く。
3. Play する。

スクリプトだけ試したい場合は、空の GameObject に **Drawing Basics Sample** を付けても同じ内容を確認できる。

| 見るところ | 内容 |
|---|---|
| 3 軸 | `Draw.Axis`。赤が右、緑が上、青が前 |
| 黄色い矢印 | `Draw.Arrow`。`Target` を刺すとそちらを向く |
| 水色の球・緑のカプセル・回る紫の箱 | `Draw.Sphere` / `Draw.Capsule` / `Draw.Box` |
| 伸び縮みする円 | `Draw.Circle`。半透明の色も使える |
| 橙のらせん | `Draw.Path`。`thickness: 3` で太くしている |
| 距離の文字 | `Draw.Text`。ゲームビューにのみ出る |
| `See Through Walls` を切り替える | `Draw.Scope(depthTest: false)`。壁の向こうでも見えるようになる |
| 2 秒ごとに出る赤い十字 | `duration: 1f` で 1 秒残る。出す瞬間だけ積んでいる |

## 補足

`DrawTimedMarker` が「毎フレームではなく、2 秒ごとに 1 回だけ `duration` 付きで積む」形に
なっているのは意図的です。`Update` の中で `duration` 付きの描画を毎フレーム呼ぶと、
消えるより速く積み上がって上限に達します。
