# Changelog

このpackageの主な変更を記録します。

## [1.0.0] - 2026-08-24

### Added

- 再現可能シミュレーションに必要な7つのRuntimeを1つの配布単位へ統合
- 固定step時計`FixedStepClock`（旧 `com.studiogaku.simulation-clock`）
- 保存・復元可能な乱数列`DeterministicRandomStream`（旧 `com.studiogaku.deterministic-random`）
- 決定的SHA-256状態ハッシュ`StateFingerprintBuilder`（旧 `com.studiogaku.state-fingerprint`）
- 有界コマンド記録`ReplayTapeBuilder` / `ReplayTapeReader`（旧 `com.studiogaku.replay-tape`）
- 正規化バイナリ`CanonicalPayloadWriter` / `CanonicalPayloadReader`（旧 `com.studiogaku.canonical-payload`）
- 検査付きQ16.16固定小数点`Fixed32`（旧 `com.studiogaku.fixed-point`）
- 世代検査付き識別子`GenerationHandlePool`（旧 `com.studiogaku.generational-handle`）
- 7つのRuntime assemblyを`DeterministicSimulation.Runtime`へ統合
- 7つのEditMode test assemblyを`DeterministicSimulation.Editor.Tests`へ統合
- 統合前の7サンプルSceneを`Samples~/<旧モジュール名>`として同梱

### Changed

- 配布単位を変更し、C#の名前空間、型名、メンバー、動作は統合前と同一のsource / API互換を維持
- runtime assembly名が変わるためbinary互換ではない。自作asmdefのReferences変更と、旧assemblyを参照するprecompiled DLLの再buildが必要
- 旧7packageのGit tagと`com.studiogaku.*`識別子は旧配布単位を継続利用する入口として有効

### Migration

統合前の7packageから移行する場合、Package Managerで旧packageを削除し、
`com.studiogaku.deterministic-simulation` を追加します。
`using SimulationClock;` などの名前空間と呼び出しcodeは変更不要です。自作asmdefのReferencesを`DeterministicSimulation.Runtime`へ変更し、旧assemblyを参照するprecompiled DLLは再buildしてください。
