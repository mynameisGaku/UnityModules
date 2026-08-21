# Input Assist Basics

## 確認できること

- `Neutral` / `Soft Right` / `Diagonal`: 同じ`InputVectorFilter`でdead zone、curve、rate limit、directionを確認する。
- `Tap`: press・release・single tap完了を確認する。
- `Hold + Repeat`: hold開始と複数repeatを確認する。
- `Reset`: vectorとbutton gestureの状態を同時に初期化する。

## 手順

1. `InputAssistBasics.unity`を開く。
2. Play Modeへ入る。
3. vector用Buttonを押し、橙色のraw markerと水色のfiltered marker、Direction表示を比較する。
4. gesture用Buttonを押し、Events、Taps、Repeatsを確認する。
5. Game Viewを960×600と640×360へ変更し、cardとButtonが画面内に収まることを確認する。

このsampleはInput Systemへ依存せず、入力列を実Buttonから直接処理器へ渡します。実gameではInput Action callback、旧Input Manager、AI、Replayなどから同じAPIを呼び出してください。
