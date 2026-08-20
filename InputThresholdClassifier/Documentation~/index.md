# Input Threshold Classifier 1.0.0

## 分解

- Input: 呼出順が明示された有限double sample
- State: immutableなrelease・press thresholdとmutableなpressed状態
- Output: sample後のpressed状態、Pressed・Released・None edge、明示error

入力device、Unity frame、時刻、random、global stateは境界外です。

## 将来問題を固定する規則

### 構成

`0 <= ReleaseThreshold < PressThreshold <= 1`だけを受理します。2値を分けることで、単一threshold付近のsmall noiseがpressed・releasedを毎sample反転させる問題を避けます。

### Released state

sampleを`[0,1]`へclampし、`value >= PressThreshold`なら`Pressed` edgeとpressed状態へ移ります。それ未満はreleasedを保持します。

### Pressed state

`value <= ReleaseThreshold`なら`Released` edgeとreleased状態へ移ります。それより大きければpressedを保持します。

両境界は状態変化側へinclusiveです。threshold間ではどちらの状態も保持されるため、同じsample値でも現在状態により結果が異なります。

## 状態の再構築

`TryCreate`へthresholdと`initialIsPressed`を渡すと完全な初期状態を再構築できます。実行中は`IsPressed`で観測し、`Reset(bool)`でthresholdを保ったまま復元できます。structをcopyした後の状態更新は互いに独立です。

## 失敗

default構成は`InvalidConfiguration`、NaN・Infinityは`NonFiniteInput`です。失敗結果は`Event.None`と変化前の`IsPressed`を返し、classifierの状態を変更しません。失敗結果と成功releasedを区別するため、必ず`Succeeded`か`Error`を確認します。

## 非目標

Input System読取、signed軸の絶対値化、repeat、tap・hold・multi-tap・chord・sequence、sample数debounce、tick・実時間、smoothing、callback、global service、file I/O、network transportは含めません。

## 検証

EditModeで構成境界、inclusive press・release、hysteresis保持、clamp、非有限値、default、Reset、state copy、golden sequenceを検証します。sampleとMono/IL2CPP Playerは960×600の5列・640×360の3+2列を実描画し、timeScale=0で同じ5操作を再現します。
