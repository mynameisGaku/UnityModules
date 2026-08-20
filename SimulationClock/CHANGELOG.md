# Changelog

このpackageの主な変更を記録します。

## [1.0.0] - 2026-08-20

### Added

- 明示整数時間を連続固定step範囲へ変換する`FixedStepClock`
- 保存・復元可能な設定、状態、結果、エラー値
- catch-up上限と明示的なstep/tick drop結果
- 同一入力再現、端数、overflow、Resetを扱う純粋EditMode検証
- 16ms、33ms、500ms hitch、Replay、Resetを確認できるresponsive UI Toolkitサンプル
- 実PanelSettings上の960x600 / 640x360 geometry検証
