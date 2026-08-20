# Changelog

## [1.0.0] - 2026-08-21

### Added

- 有限1軸入力、dead zone、片側段階数からsigned short commandを生成するimmutable quantizer
- 範囲外入力のclamp、half-away-from-zero丸め、非有限入力・不正設定の明示結果
- Engine非依存の境界値・対称性・隣接浮動小数点値の契約検証
- golden入力列、非有限値の非破壊拒否、wide/narrow実Panel sample検証
