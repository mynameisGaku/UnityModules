# Input Radial Dead Zone 1.0.0

## 分解

- Input: 有限double horizontal・vertical
- State: immutable inner・outer境界だけ。処理履歴を持たない
- Output: immutableな補正済みhorizontal・vertical・magnitudeまたは明示error

入力device、Unity frame、時刻、random、global stateは境界外です。

## 将来問題を固定する規則

### Inner

入力のradial magnitudeが`InnerDeadZone`以下なら、inclusiveで補正済み成分とmagnitudeを0にします。成分ごとの四角いdead zoneではありません。

### Continuous remap

innerより大きくouterより小さいmagnitudeは次式で0..1へ線形remapします。

```text
outputMagnitude = (inputMagnitude - InnerDeadZone) / (OuterDeadZone - InnerDeadZone)
outputVector = inputDirection * outputMagnitude
```

### Outerとover-range

入力magnitudeが`OuterDeadZone`以上なら、inclusiveで同じ方向のunit vectorを返します。各componentを個別にclampしないため、`(3,4)`は`(0.6,0.8)`になります。

極端に大きい有限入力は最大絶対成分で先に比率化し、`x*x + y*y`を直接計算しません。このためoverflowしても方向を失いません。

## 結果

`Succeeded`がtrueなら`Horizontal`、`Vertical`、`Magnitude`を利用できます。`IsZero`は成功したzero結果だけでtrueです。失敗結果も各数値が0なので、成功zeroとの区別には必ず`Succeeded`か`Error`を確認します。

## 非目標

Input System読取、方向量子化、1軸段階化、smoothing、hysteresis、binding、command ID、global service、file I/O、network transport、Replay再生は含めません。

## 検証

EditModeで構成境界、inner・outer inclusive、線形remap、方向保持、over-range、最大有限値、subnormal、非有限値、default、equalityを検証します。sampleとMono/IL2CPP Playerは960×600の5列・640×360の3+2列を実描画し、timeScale=0で同じ5操作を再現します。
