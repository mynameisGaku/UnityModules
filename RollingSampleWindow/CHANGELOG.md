# Changelog

## [1.0.0] - 2026-08-21

### Added

- 容量1〜32の有限sampleをoldest-first FIFOで保持するwindow
- 満杯時のoldest退避と追加前後snapshotを返すimmutable result
- count・min・max・mean・oldest・newestを持つobservable snapshot
- 非有限sampleと範囲外indexをstate非変更で拒否するerror契約
- wrap・extreme finite値・退避後再集計・公開型面のEditMode tests
- 容量3への4追加、clear、wide/narrow実Panelを確認するBasics sample
