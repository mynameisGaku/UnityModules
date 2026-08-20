# Changelog

## [1.0.0] - 2026-08-21

### Added

- `RTAP` format version 1の16-byte headerとcanonical little-endian record
- 非減少tick、同tick追加順、0以外のcommand id、opaque payload
- 1 MiB / 65,536 entries既定、16 MiB / 1,000,000 entries最大の有界builder
- 完全検証parse、immutable value、独立reader、payload copy
- failure時不変、非消費Build、Reset、idempotent Dispose
- Engine非依存Runtimeとcanonical golden EditMode tests
- Record、Build、Replay、Resetを確認できるresponsive UI Toolkitサンプル
- 実PanelSettings上の960x600 / 640x360 geometry検証
