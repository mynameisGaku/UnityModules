# Input Multi Tap Classifier 1.0.0

## 分解

- Input: 非減少ulong tick、tap edgeの有無
- State: pending tap数、最後のtap tick、gap設定、最大tap数
- Output: immutable pending・確定event status、または明示error

Engine input、時刻、random、global stateは境界外です。

## Burst確定

- 最初のtapはpending count 1として待機する
- 最後のtapからmaximumGapTicks後まではinclusive windowとする
- tickがdeadlineを超えるとGapExpiredで現在countを確定する
- deadline超過tickのtapは古いburstの確定と新しいpending count 1を同時に返す
- maximumTapCountへ達すると待たずMaximumReachedで確定する

maximumGapTicksは正、maximumTapCountは2以上8以下です。deadline加算がulong上限を超える場合はulong.MaxValueへ飽和します。

## 時系列

同じtickの複数tapも受理します。現在tickより前はTickMovedBackwardで全state不変です。Snapshotは現在stateだけを返しtap・確定eventを消します。Resetはpendingを破棄し、新しいtimelineへ移ります。

## 非目標

- raw pressed状態からのtap edge生成・long press分類
- command IDを持つsequence・combo matching
- Input System・Legacy Input Manager・Unity時刻・binding
- effect callback・network同期・設定永続化

## 検証

EditModeでinclusive gap、最大数、late tap、新burst、逆行、overflow、snapshot、resetを検証します。sampleとMono/IL2CPP Playerは960×600の5列・640×360の3+2列を実描画し、timeScale=0でdouble→tripleの同じ5操作を再現します。
