# Changelog

## [1.0.0] - 2026-08-21

### Added

- target magnitudeを即時反映し、明示stepごとの方向回転だけを制限する状態fulな2D processor
- 更新後成分、target magnitude、実適用・残りradian、prior方向・数値補正を返すimmutable result
- unit circle検証、180°反時計tie-break、zero state、不正target非変更、明示reset、subnormal保持
- 反時計・時計回り、2 step到達、正反対、入力拒否、wide/narrow実Panelを確認するBasics sample
