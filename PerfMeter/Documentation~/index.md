# Perf Meter Documentation

実行速度計測（Perf Meter）の詳細な契約。利用者の概要把握は[README](../README.md)を参照してください。

## 型の構成

| 型 | 役割 |
| --- | --- |
| `FrameTimeSampler` | 有界リングバッファと統計の中核。純粋class。 |
| `FrameTimeSnapshot` | 1つの取得タイミングで揃えた統計。readonly struct。 |
| `PerfMeterError` | 失敗理由のenum。 |
| `MemoryProbe` / `MemorySnapshot` | 簡易メモリ取得のstatic入口と結果。readonly struct。 |
| `PerfMeterComponent` | 毎frame計上とoverlay表示を行う任意利用のMonoBehaviour。 |

名前空間は`PerfMeter`です。samples側のみ`PerfMeter.Samples`を使います。

## FrameTimeSampler

### 生成と寿命

- constructor `FrameTimeSampler(int capacityFrames)` は容量1〜65536のみを受け付け、範囲外は`ArgumentOutOfRangeException`を送出します。
- 内部配列（sample用＋sort scratch用）はconstructorでのみ確保します。以降の`AddFrame`や統計読出しで新規確保しません。
- singletonではなく、寿命が明確なownerが1 instanceを持ちます。複数ownerが同じinstanceを共有する場合、呼出しはUnity main threadへ直列化してください。
- `Dispose()`後は`AddFrame`、`SetSpikeThreshold`、`TryGetPercentile`が`SamplerDisposed`で失敗し、統計propertyは空windowの正準値0へ戻ります。`Reset()`と2回目の`Dispose()`は安全なno-opです。

### AddFrame

`bool AddFrame(double deltaTimeSeconds, out PerfMeterError error)`

| 入力 | 結果 |
| --- | --- |
| NaN / ±Infinity | false + `NonFiniteValue`。状態不変。 |
| 負値 | false + `NegativeValue`。状態不変。 |
| 0 / -0 | 成功として計上する。同一frame二重計上などの用途。 |
| 正値 | 成功。容量満杯なら最古のsampleを上書きする。 |

spike閾値が0より大きい場合、成功した追加だけを対象に「入力値 > 閾値」を評価し、`_pendingSpikes`と`TotalSpikes`を増やします。

### 統計の定義

現在window（oldest→newest）に対して都度計算し、同じ挿入列なら常に同じ値を返します。

- `Average`: Σdt ÷ n。加算はoldest→newestの順です。
- `Minimum` / `Maximum`: window内の最小・最大。
- `Median`: 昇順中央値。偶数件は中央2件の平均。実装はpercentile(50)と同一のsort経路です。
- `StandardDeviation`: sqrt(Σ(x−μ)² ÷ n)。母標準偏差です（不偏分散ではありません）。
- `AverageFps`: n ÷ Σdt。windowが空またはΣdt=0のときは0。
- `TryGetPercentile(p)`: rank=(n−1)×p÷100の位置を隣接2件で線形補間します。p=100はMaximum、p=50はMedianと一致します。p∈(0,100]外または非有限はfalse + `InvalidPercentile`です。
- sampleが0件の場合、これらはすべて正準値0を返します（`TryGetPercentile`もtrue + 0）。読む前に`SampleCount`を確認してください。

### Spike計数

- `SetSpikeThreshold(seconds, out error)`: 非有限は`NonFiniteValue`、負値は`InvalidThreshold`、0は判定無効。設定済みの計数は変わりません。
- `SpikesSinceLastCheck()`: 前回呼出し以降のthreshold超過数を返し0へ戻します。閾値0では常に0を返します。
- 閾値を後から変えても過去frameの再評価は行いません。評価は`AddFrame`時点の閾値で確定します。

### Reset

buffer、pending spike、`TotalSpikes`を初期化します。容量と閾値の設定は保持します。

## MemoryProbe / MemorySnapshot

- `CaptureMemorySnapshot(int currentFrame)`: managed heap=`GC.GetTotalMemory(false)`（強制collectionなし）、Profiler reported heap=`Profiler.usedHeapSizeLong`（`Profiler.enabled`時のみ、それ以外-1）を返します。frame番号は呼び出し側が渡します（例: `Time.frameCount`）。
- `CaptureMemorySnapshot()`: frame番号不明版。`CapturedAtFrame = -1`になります。
- 数値は簡易的な目安です。mono/IL2CPPでmanaged heapの意味が異なり、nativeヒープは含みません。総メモリ使用量の保証はありません。

## PerfMeterComponent

- Inspector: `capacityFrames`(既定600)、`showOverlay`(既定true)、`spikeThresholdSeconds`(既定0.033)。
- `Awake`でsamplerを構築します。Inspector値が範囲外でも許可範囲へ丸めて構築するため、component単体では例外を出しません。
- `Update`で`Time.deltaTime`を計上し、spikeラッチを読んでoverlay用に保持します。
- `OnGUI`はGUI.Labelのみでoverlayを描きます。色は通常白、直前frameが閾値超の間は赤です。文字列整形は毎描画で確保を伴います（表示専用経路であり、計測本体のGC契約には含まれません）。
- singleton化しません。sceneへ置いた者だけが動き、複数置けば独立に動きます。
- public wrapper: `ResetStats()`、`SetSpikeThreshold(double)`、`Sampler` getter。

## テスト範囲

- EditMode（`PerfMeter.Editor.Tests`）
  - 既知dt列（60fps相当1/60×10＋spike 0.1）に対するAverage、Min、Max、Median、StdDev、Percentile95の期待値一致。許容誤差1e-9。
  - 容量超過時の最古上書きを統計で検証。
  - percentile境界（p=0、負、NaN、±Infinity、100超→error、p=50=Median、p=100=Max）。
  - AddFrameのNonFinite / Negativeエラーと状態不変。
  - SpikesSinceLastCheckのラッチ（2回呼出しで2回目0）と閾値0の常時0。
  - Snapshot等価比較（全field）。
  - Dispose後のエラーと正準値。
  - AddFrame経路のGC確保ゼロ（`GC.GetAllocatedBytesForCurrentThread`差分）。
- Samples PlayMode（`PerfMeter.Samples.Runtime.Tests`）
  - `HeavyFrame()`直後のframeでLast≥0.05秒を確認。
  - `ResetStats()`後SampleCountが0へ戻ることを確認。
