# Changelog

## [1.0.0] - 2026-08-21

### Added

- 正の一意IDで最大32件の有限modifierをID昇順へ保持するstateful stack
- Flat合計、加算percent合計、乗算factor積を固定3 stage式で評価する決定論的processor
- add・update・remove・base変更・clearの前後値、stage合計、件数、対象IDを返すimmutable result
- 重複・容量・非有限値・overflowをstate非変更で拒否する明示error契約
- 3 stage合成、factor更新、重複拒否、wide/narrow実Panelを確認するBasics sample
