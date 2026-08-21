# Changelog

## [1.0.0] - 2026-08-21

### Added

- 最大32件の明示数値条件を変更せず全件評価するstateless evaluator
- 6種類の比較、許容差境界、signed delta・absolute delta・成立可否を持つ入力順のimmutable line
- null・件数・ID・非有限値・比較・許容差・重複・結果範囲外を部分結果なしで区別するerror契約
- 全比較境界、混在、最大件数、最大有限値、overflow、入力不変、bit安定性、公開型面のEditMode tests
- all pass・mixed・tolerance・strict・invalidとwide/narrow実Panelを確認するBasics sample
