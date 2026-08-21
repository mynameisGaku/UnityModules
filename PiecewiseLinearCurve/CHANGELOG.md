# Changelog

## [1.0.0] - 2026-08-21

### Added

- 最大32個の有限pointを一意Xの昇順へ保持するcurve
- 完全一致・線形補間・範囲外clampを明示結果へ返す評価契約
- lower/upper point・index・補間率・clamp状態を持つimmutable result
- 追加・更新・除去・clearの前後Y・件数を持つ変更result
- 非有限値・重複・容量をstate非変更で拒否するerror契約
- 3 pointと2 query、wide/narrow実Panelを確認するBasics sample
