# Changelog

## [1.0.0] - 2026-08-21

### Added

- 正のcommand id、明示`ulong` tick、固定容量、inclusive retention windowを持つEngine非依存buffer
- 重複commandのFIFO記録・peek・消費、期限切れ削除、容量不足・tick逆行error契約
- 容量・tick・retention境界、重複順序、失敗時非変更、clear/resetのEditMode検証
- Jumpの期限内消費、Dashの期限切れ、wide/narrow実Panel sample検証
