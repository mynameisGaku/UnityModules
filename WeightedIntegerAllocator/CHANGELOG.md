# Changelog

## [1.0.0] - 2026-08-21

### Added

- 最大32 entryへ整数総量を非負整数weight比で配るstateless allocator
- 64-bit整数積・切り捨てbase・largest remainder・入力順tie-breakによる合計保存
- zero weight、zero total、最大1,000,000,000 total/weightを明示する入力契約
- input順のweight・base・剰余・追加1 unit・最終配分を再構築できるimmutable result
- null・件数・total・ID・重複・weight・zero total weightを部分結果なしで区別するerror契約
- 最大積、合計保存、端数順位、入力/結果不変、公開型面を検証するEditMode tests
- equal・weighted・remainder・zero weight・zero totalとwide/narrow実Panelを確認するBasics sample
