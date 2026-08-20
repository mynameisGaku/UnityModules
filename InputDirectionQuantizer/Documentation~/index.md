# Input Direction Quantizer 1.0.0

## 分解

- Input: 有限double horizontal・vertical
- State: immutable radial dead zone・direction mode
- Output: immutable `(Horizontal, Vertical)`または明示error

入力device、Unity frame、時刻、random、global stateは境界外です。

## 将来問題を固定する規則

### Dead zone

各成分を`[-1,1]`へclampした後、`x*x + y*y <= DeadZone*DeadZone`をneutralとします。成分ごとの四角いdead zoneではなくradial円です。

### FourWay

`abs(x) > abs(y)`ならhorizontal、それ以外はverticalです。exact tieはverticalへ固定し、曖昧なplatform依存分岐を残しません。

### EightWay

三角関数は使わず、固定定数`DiagonalThreshold = 0.4142135623730951`との比率比較だけでsectorを決めます。

- `abs(y) <= abs(x) * threshold`: horizontal cardinal
- `abs(x) <= abs(y) * threshold`: vertical cardinal
- それ以外: diagonal

両境界はcardinal側へinclusiveです。

## 結果

`Succeeded`がtrueなら`Horizontal`と`Vertical`を利用できます。両方0なら`IsNeutral`、両方非0なら`IsDiagonal`です。失敗結果の成分も0なので、成功neutralとの区別には必ず`Succeeded`か`Error`を確認します。

## 非目標

Input System読取、1軸多段階量子化、analog magnitude、normalization、smoothing、hysteresis、binding、command ID、global service、file I/O、network transport、Replay再生は含めません。

## 検証

EditModeでradial境界、4象限、4-way tie、8-way cardinal・diagonal、sector境界、clamp、非有限値、default、equalityを検証します。sampleとMono/IL2CPP Playerは960×600の5列・640×360の3+2列を実描画し、timeScale=0で同じ5操作を再現します。
