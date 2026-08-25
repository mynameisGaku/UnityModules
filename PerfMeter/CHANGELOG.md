# Changelog

## [1.0.0] - 2026-08-26

### Added

- 容量1〜65536の有界リングバッファでframe timeを蓄える`FrameTimeSampler`と、平均、最小、最大、中央値、母標準偏差、平均fps、線形補間percentileの決定論的な統計。
- spike閾値秒の設定と、前回読出し以降の超過数をラッチして返す`SpikesSinceLastCheck`。
- 全統計を1つの取得タイミングで揃えた`FrameTimeSnapshot`と全field比較のEquals / GetHashCode。
- managed heapとProfiler reported heapを簡易取得する`MemoryProbe` / `MemorySnapshot`。
- Sceneに置くだけで毎frame計上とoverlay表示を行う`PerfMeterComponent`とBasics sample。
- 既知dt列の期待値検証（許容誤差1e-9）を含むEditMode test群と、人工spikeを検証するSamples PlayMode test。

### Boundaries

- GPU時間、draw call、Profiler marker別内訳、電力・温度推定は含みません。原因分析はUnity Profilerの役割です。
- `AddFrame`経路のGC確保なしは、constructorでの初回capacity確保を除く契約です。`PerfMeterComponent.OnGUI`の文字列整形は確保を伴います。
- `MemorySnapshot.ManagedBytes`は`GC.GetTotalMemory(false)`のmanaged heap瞬間値で、native heapを含みません。`ProfilerReportedBytes`はProfiler有効時のみ取得でき、無効時は`-1`です。どちらも総メモリ使用量ではありません。
- sampleが0件の場合、統計は正準値0を返します。percentileも空windowでは0を返し、失敗にはしません。
- singleton、static event、自動GameObject、自動起動は作りません。計測の開始・停止は利用側ownerが明示します。
