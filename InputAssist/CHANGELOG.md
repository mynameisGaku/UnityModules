# Changelog

## [1.0.0] - 2026-08-22

### Added

- 2D入力のradial dead zone、response curve、rise・fall rate limit、4-way・8-way方向判定を`InputVectorFilter`へ統合。
- press、release、hold、repeat、single・multi-tapを`InputButtonTracker`へ統合。
- 明示delta time、状態reset、失敗時の状態維持、無制限repeat catch-up防止を実装。
- 実Buttonとresponsive geometryを確認できるInput Assist Basics sampleを追加。
