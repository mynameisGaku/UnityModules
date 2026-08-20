# Input Press Classifier 1.0.0

## 目的

tap・hold判定をframe数やEngine時刻へ直接結び付けると、polling頻度やReplay方法によって分類が変わります。本moduleは、利用側が明示したtickと押下boolだけから同じ結果を再現します。

## 分解

- Input: 非減少ulong tickと、そのtickでの押下bool
- State: 最新tick、押下edge tick、pressed状態、hold判定済みlatch
- Output: immutable edge・tap・hold status、または明示error

Unity input、時刻、random、Coroutine、singletonは境界外です。

## 作成

InputPressClassifier.TryCreate(holdThresholdTicks, initialTick, ...)はreleased状態を作ります。閾値0は分類境界を持てないためInvalidHoldThresholdです。閾値はulong最大値まで受理し、経過差の減算だけで比較するため加算overflowを起こしません。

## 押下とhold開始

releasedからpressedへ変わるとPressStarted=true、継続時間0です。以後のpressed sampleでtick差が閾値以上へ初めて到達するとHoldStarted=true、IsHolding=trueになります。保持中の後続sampleはhold開始を再発行しません。

## 解放分類

pressedからreleasedへ変わったsampleでReleased=trueになります。

- 継続差が閾値未満: Tapped=true
- hold開始済み: HoldCompleted=true
- sampleが閾値を飛び越えて解放: HoldStarted=trueかつHoldCompleted=true

解放後のSnapshotは過去の分類flagと継続時間を保持しません。次の押下edgeは新しい分類を開始します。

## 時系列

同じtickは受理しますが経過差は増えません。現在tickより前はTickMovedBackwardで、押下edge・latch・現在tickを変更しません。Resetは押下履歴を破棄して指定tickのreleased状態へ移ります。

## 非目標

- Input System、Legacy Input Manager、device polling
- repeat生成、複数command chord、順序combo、buffer
- double tap、charge量、効果callback、cancel理由
- key binding、永続化、network同期

## 検証

EditModeで閾値境界、tap、hold開始、sample jump、hold完了、再押下、逆行、reset、ulong最大値を検証します。import済みsampleは実PanelSettingsで960×600の5 Button 1列と640×360の3+2列、Mono/IL2CPP PlayerはtimeScale=0で同一分類結果を検証します。
