# Changelog

## [1.0.0] - 2026-08-25

### Added

- Input Systemのglobalな実入力から最後に操作された表示familyを追跡する契約。
- Keyboard / Mouse、Xbox、PlayStation、Switch、generic gamepad、Touch、Unknownの表示family。
- Input System layout名の厳密一致overrideと、未判定時の明示的なfallback。
- 空layout、重複layout、定義外familyを黙って無視しない設定検証。

### Boundaries

- Input System 1.20.0とUnity UI Toolkit module 1.0.0へ依存します。
- glyph asset、rebind、device pairing、入力消費、player別追跡、manufacturer文字列による推測は含みません。
