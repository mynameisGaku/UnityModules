# Changelog

このpackageの主な変更を記録します。

## [1.0.0] - 2026-08-20

### Added

- xoshiro256**とSplitMix64 seed展開をversion 1として固定した`DeterministicRandomStream`
- 保存・復元可能な256-bit状態とalgorithm version
- 64/32-bit、bool、`[0,1)`浮動小数、偏りのない半開区間整数
- reference golden vector、範囲、不正入力時状態不変を扱う純粋EditMode検証
- UInt64、D20、double、State Replay、Reset Seedを確認できるresponsive UI Toolkitサンプル
- 実PanelSettings上の960x600 / 640x360 geometry検証
