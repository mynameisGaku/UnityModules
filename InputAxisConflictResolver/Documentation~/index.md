# Input Axis Conflict Resolver 1.0.0

## 分解

- Input: 非減少ulong tick、negativePressed、positivePressed
- State: 両入力の直前状態と最新押下edge tick
- Output: immutable edge・競合・解決値status、または明示error

Engine input、時刻、random、global stateは境界外です。

## Policy

- Neutral: 競合は0
- NegativeWins: 競合は-1
- PositiveWins: 競合は1
- LastPressedWins: より新しい押下edge側。同一tickは0

single入力はpolicyに関係なくその方向を返します。winnerが解放され反対側が保持中なら反対側へfallbackします。

## 時系列

同じtickは受理します。現在tickより前はTickMovedBackwardで全state不変です。Snapshotは現在stateだけを返しedge・変更flagを消します。Resetは新しいneutral timelineへ移ります。

## 非目標

- analog dead zone・量子化・平滑化
- Input System・Legacy Input Manager・binding
- 4方向・8方向vector合成
- effect callback・network同期・永続化

## 検証

EditModeで4 policy、edge、tie、release fallback、逆行、snapshot、resetを検証します。sampleとMono/IL2CPP Playerは960×600の5列・640×360の3+2列を実描画し、timeScale=0で同じ5操作を再現します。
