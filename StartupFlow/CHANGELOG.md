# Changelog

## [1.0.0] - 2026-08-20

### Added

- `IStartupStep` の `Order → Id` 決定論的直列実行。
- step進捗、全体進捗、terminal結果、失敗step、完了件数。
- 利用側tokenとApplication終了tokenを結合した協調cancel。
- callback例外隔離、Busy再入防止、completion continuation分離。
- Success、Failure、Slow、Cancel、Resetを確認できる **Startup Flow Basics** sample。
- 960×600と640×360の実PanelSettings geometry回帰検証。
