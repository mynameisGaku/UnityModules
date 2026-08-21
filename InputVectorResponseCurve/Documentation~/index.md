# Input Vector Response Curve 1.0.0

## 分解

- Input: 単位円内の有限double horizontal・vertical
- State: immutableな`InputVectorResponseMode`だけ。処理履歴を持たない
- Output: immutableな処理済みhorizontal・vertical・magnitudeまたは明示error

入力device、Unity frame、時刻、random、global stateは境界外です。同じmodeと同じ入力は同じ結果になります。

## Curve

入力vectorの方向を変えず、0以上1以下のradial magnitudeだけを次式で変換します。

```text
Linear:     outputMagnitude = magnitude
Squared:    outputMagnitude = magnitude * magnitude
Cubic:      outputMagnitude = magnitude * magnitude * magnitude
SmoothStep: outputMagnitude = magnitude * magnitude * (3 - 2 * magnitude)
outputVector = inputDirection * outputMagnitude
```

`(0.3, 0.4)`の入力magnitudeは`0.5`です。Linearは`(0.3, 0.4)`、Squaredは`(0.15, 0.2)`、Cubicは`(0.075, 0.1)`を返します。SmoothStepの中央値は`0.5`のため、同じ入力を返します。

## 入力境界

zeroは方向計算を行わずzeroを返します。unit magnitudeはすべてのcurveで同じunit vectorを返します。magnitudeが1を超える入力は暗黙にclampせず`InputOutOfRange`で拒否します。これにより正規化責務を利用側またはInput Radial Dead Zoneへ残します。

NaNかInfinityを含む場合は`NonFiniteInput`です。magnitudeは最大絶対成分で先に比率化して求めるため、入力検証と方向計算を同じ決定論的な経路で行います。

## 結果

`Succeeded`がtrueなら`Horizontal`、`Vertical`、`Magnitude`を利用できます。`IsZero`は成功したzero結果だけでtrueです。失敗結果も各数値が0なので、成功zeroとの区別には必ず`Succeeded`か`Error`を確認します。

## 非目標

Input System読取、dead zone、入力正規化、時間依存smoothing、slew制限、方向量子化、1軸段階化、binding、command ID、global service、file I/O、network transport、Replay再生は含めません。

## 検証

EditModeで4 mode、zero・unit境界、golden vector、方向保持、単位円外、subnormal、非有限値、default、equalityを検証します。sampleとMono/IL2CPP Playerは960×600の5列・640×360の3+2列を実描画し、4 modeと拒否後の成功値保持を再現します。
