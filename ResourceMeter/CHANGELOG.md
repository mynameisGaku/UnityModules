# Changelog

## [1.0.0] - 2026-08-21

### Added

- immutable capacity内で回復・部分消費・全量必須消費を処理する小さなstate holder
- 前後値、要求・実適用・未適用delta、全量適用、空・満杯・境界遷移を返すimmutable result
- 有限値・範囲・policy検証、不足時の明示結果、不正入力非変更、明示reset
- 回復、2種類の不足消費、exact消費、負amount拒否、wide/narrow実Panelを確認するBasics sample
