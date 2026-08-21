# Changelog

## [1.0.0] - 2026-08-21

### Added

- 正のIDと有限thresholdを最大32件まで昇順保持するtable
- 現在tier・次tier・inclusive境界・0〜1 progressを返すimmutable evaluation
- ID／threshold重複、容量超過、非有限値、空tableをstate非変更で拒否するerror契約
- 順不同挿入、削除、clear、極端な有限範囲、公開型面のEditMode tests
- Bronze・Silver・Goldへ5値を評価し、wide/narrow実Panelを確認するBasics sample
