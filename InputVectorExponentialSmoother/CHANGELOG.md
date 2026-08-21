# Changelog

## [1.0.0] - 2026-08-21

### Added

- 明示stepごとにtarget差の一定割合を適用する状態fulな2D exponential smoother
- 更新後成分、実適用差分、残差、exact到達状態を返すimmutable result
- 不正target非変更、明示reset、subnormal丸め停滞の観測、状態再構築検証
- golden反復列、factor 1、入力拒否、wide/narrow実Panelを確認するBasics sample
