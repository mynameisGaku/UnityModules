# Changelog

## [1.0.0] - 2026-08-24

### Added

- 19個の独立moduleを1つの導入単位へ統合した`Gameplay Rules`。resource・cost、能力値補正、重み付き選択と整数按分、curveとtier、標本統計と傾向推定、charge・定期tick・時限stack・stack移送、数値条件・utility score・敵対度・damage軽減の判定を1つのassemblyで提供する
- 単一runtime assembly `GameplayRules.Runtime`と単一EditMode test assembly `GameplayRules.Tests`
- 統合前の各moduleに対応する19個のBasics sample

### Changed

- 統合前の19個のUPM識別子を`com.studiogaku.gameplay-rules`へ集約した。旧識別子と公開済みtagは互換入口として引き続き利用できる
- 各sampleのassembly definitionが参照するruntime assemblyを`GameplayRules.Runtime`へ変更した

### 統合した旧package

- `com.studiogaku.resource-meter` (Resource Meter)
- `com.studiogaku.resource-cost-evaluator` (Resource Cost Evaluator)
- `com.studiogaku.stat-modifier-stack` (Stat Modifier Stack)
- `com.studiogaku.weighted-choice-table` (Weighted Choice Table)
- `com.studiogaku.weighted-integer-allocator` (Weighted Integer Allocator)
- `com.studiogaku.piecewise-linear-curve` (Piecewise Linear Curve)
- `com.studiogaku.rolling-sample-window` (Rolling Sample Window)
- `com.studiogaku.sample-statistics` (Sample Statistics)
- `com.studiogaku.linear-trend-estimator` (Linear Trend Estimator)
- `com.studiogaku.threshold-tier-table` (Threshold Tier Table)
- `com.studiogaku.charge-cooldown` (Charge Cooldown)
- `com.studiogaku.periodic-tick-planner` (Periodic Tick Planner)
- `com.studiogaku.timed-stack-resolver` (Timed Stack Resolver)
- `com.studiogaku.stack-transfer-planner` (Stack Transfer Planner)
- `com.studiogaku.numeric-requirement-evaluator` (Numeric Requirement Evaluator)
- `com.studiogaku.utility-score-evaluator` (Utility Score Evaluator)
- `com.studiogaku.stable-score-selector` (Stable Score Selector)
- `com.studiogaku.damage-mitigation-evaluator` (Damage Mitigation Evaluator)
- `com.studiogaku.threat-score-resolver` (Threat Score Resolver)

### Compatibility

- C#名前空間、型名、member名は統合前と同一である。既存codeの編集は不要
