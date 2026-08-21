# Changelog

## [1.0.0] - 2026-08-21

### Added

- 正の一意IDで最大32件の有限positive weightをID昇順へ保持するtable
- 明示`[0, 1)` sampleを半開累積区間へ写す決定論的selection
- 選択ID・sorted index・ticket・区間・totalを返すimmutable result
- 追加・更新・除去・clearの前後weight・total・件数を返す変更result
- 重複・容量・無効値・overflowをstate非変更で拒否する明示error契約
- weight 6・3・1とsample 0.65・0.95、wide/narrow実Panelを確認するBasics sample
