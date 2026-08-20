# Changelog

## [1.0.0] - 2026-08-20

### Added

- format version 1の型付きfield recordとSHA-256 fingerprint
- null、bool、32/64-bit整数、single、double、UTF-8 string、bytes
- field id・型・長さ・操作順を含むcanonical形式
- 1 MiB既定 / 16 MiB最大の有界builder
- failure時不変、Reset、Dispose、hex/byte roundtrip
- Engine非依存Runtimeとgolden vector EditMode tests
- Build、Damage、Move、Replay、Resetを確認できるresponsive UI Toolkitサンプル
- 実PanelSettings上の960x600 / 640x360 geometry検証

