# Input Assist 2.0.0

## 目的

入力補正の小さな処理を個別packageとして選ぶ手間を減らし、2D入力とボタンgestureの一般的な組合せを1つの導入単位へまとめます。2.0.0では、Unity向けの`float`+`deltaTime` APIに加えて、割り当てなしの`double`・tick契約を同梱します。

## 2つの契約

| | Unity向けAPI | 割り当てなしAPI |
|---|---|---|
| namespace | `InputAssist` | `InputDeadZones` / `InputResponse` / `InputSmoothing` / `InputFiltering` / `InputMixing` / `InputDirectionQuantization` / `InputQuantization` / `InputThresholding` / `InputPressing` / `InputRepeating` / `InputMultiTapping` |
| 代表型 | `InputVectorFilter`、`InputButtonTracker` | `InputRadialDeadZone`、`AxisQuantizer`、`InputRepeatTracker`ほか |
| 数値 | `float` / `Vector2` | `double` / `ulong` tick |
| 時間 | 呼び出し側が渡す`deltaTime`（秒） | 呼び出し側が持つ単調増加tick |
| Inspector | `[Serializable]`で直接編集 | 非対応 |
| 割り当て | 処理器はclass、更新中の追加割り当てなし | 値型中心、更新中の追加割り当てなし |

MonoBehaviourへ設定を出して`Time.deltaTime`で回すならUnity向けAPI、fixed tick simulationやReplayで隠れた時刻依存を避けるなら割り当てなしAPIを選びます。同じruntime/backendと同じ入力列では再現できますが、`Math.Sqrt`などを使うため端末・backendをまたぐbit一致は保証しません。2系統は独立実装で互いを呼びません。

`InputAssist.InputDirectionMode`と`InputDirectionQuantization.InputDirectionMode`は別namespaceの別enumとして共存します。片方だけを`using`するか、完全修飾名で参照してください。

## 依存方向

```text
Input System / Legacy Input / AI / Replay
                    ↓
   raw Vector2 / bool / deltaTime   |   raw double / bool / tick
                    ↓
 InputVectorFilter / InputButtonTracker  |  allocation-free value types
                    ↓
       filtered value / direction / events / status
                    ↓
             Gameplay / UI / Character
```

どちらの契約も`Time`、Input System、singleton、Scene状態を読みません。同じ初期状態、入力列、経過時間（tick）から同じ結果を作れます。

## InputVectorFilter

1. radial inner・outer dead zone
2. normalized magnitude response curve
3. explicit delta timeによるrise・fall rate limit
4. neutral / 4-way / 8-way direction classification

`RiseSpeed`または`FallSpeed`が0の場合、その方向のrate limitを無効化してtargetへ即時追従します。

## InputButtonTracker

- edge: `Pressed`, `Released`
- duration: `HoldStarted`
- repeat: `Repeated`, `RepeatCount`
- short-press burst: `TapCompleted`, `TapCount`

repeatの大きな時間jumpは1回の更新につき32件へ制限します。残りは次回以降へ持ち越されるため、異常なframe停止で無制限loopしません。

## 割り当てなしAPI

| namespace | 主な型 | 役割 |
|---|---|---|
| `InputDeadZones` | `InputRadialDeadZone`、`InputRadialDeadZoneResult` | 内外radial dead zoneの再mapping |
| `InputResponse` | `InputVectorResponseCurve`、`InputVectorResponseMode`、`InputVectorResponseResult` | 方向を保つmagnitude curve |
| `InputSmoothing` | `InputVectorSlewLimiter`、`InputVectorDirectionLimiter` | 1 stepあたりの最大変化量・最大旋回角 |
| `InputFiltering` | `InputVectorExponentialSmoother`、`InputVectorExponentialResult` | step単位の指数low-pass |
| `InputMixing` | `InputVectorWeightedMixer`、`InputVectorContribution`、`InputVectorMixResult` | 正規化重み付き平均 |
| `InputDirectionQuantization` | `InputDirectionQuantizer`、`InputDirectionMode` | 4方向・8方向への量子化 |
| `InputQuantization` | `AxisQuantizer`、`InputQuantizationResult` | 軸値→符号付き整数step |
| `InputThresholding` | `InputThresholdClassifier`、`InputThresholdEvent` | hysteresis付きpressed判定 |
| `InputPressing` | `InputPressClassifier`、`InputPressStatus` | tap / holdの分類 |
| `InputRepeating` | `InputRepeatTracker`、`InputRepeatStatus` | 初回delayとrepeat間隔、tick jumpのcatch-up |
| `InputMultiTapping` | `InputMultiTapClassifier`、`InputMultiTapStatus`、`InputMultiTapCompletionReason` | single〜N連tapの確定 |

生成は`TryCreate(...)`、評価は`Process` / `Quantize` / `TryPush`です。非有限値、範囲外、tick順序違反は暗黙clampせず、型ごとのerror enumで返します。

## 状態の再構築

- `InputVectorFilter.Reset()` / `TryReset(Vector2, out error)`
- `InputButtonTracker.Reset()`
- `InputRepeatTracker.Reset(tick)`ほか、tick契約の処理器はtimelineの開始位置を明示して戻します。

Scene切替、Replay seek、testのarrange時に明示的に状態を戻せます。

## 吸収した旧package

2.0.0で次の12packageをInput Assistへ吸収しました。公開済みtagとUPM識別子は旧配布単位を継続利用する入口として残し、C#のnamespace、型名、member、動作は変更していないためsource / API互換です。

`com.studiogaku.input-radial-dead-zone`、`com.studiogaku.input-vector-response-curve`、`com.studiogaku.input-vector-slew-limiter`、`com.studiogaku.input-vector-exponential-smoother`、`com.studiogaku.input-vector-direction-limiter`、`com.studiogaku.input-vector-weighted-mixer`、`com.studiogaku.input-direction-quantizer`、`com.studiogaku.input-quantizer`、`com.studiogaku.input-threshold-classifier`、`com.studiogaku.input-press-classifier`、`com.studiogaku.input-repeat`、`com.studiogaku.input-multi-tap-classifier`

runtime assembly名は変わるためbinary互換ではありません。旧runtime assembly名を自作`asmdef`の`references`に書いている場合は`InputAssist.Runtime`へ置き換え、旧assemblyを参照するprecompiled DLLは再buildしてください。旧12 assemblyは`noEngineReferences: true`でしたが、統合先は`Vector2`・`Mathf` APIも収容するためUnityEngineを参照します。UnityEngine非参照assemblyが必要な場合は旧tagを継続利用してください。旧packageとInput Assist 2.0.0以降は同時導入できません。

## 非目標

Input Actionの購読、device pairing、rebind、Action Map停止、入力record、command comboは扱いません。呼び出し側または専用moduleへ残し、入力値の補正とgesture判定へ責務を限定します。
