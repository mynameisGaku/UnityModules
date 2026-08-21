# Changelog

## [1.0.0] - 2026-08-21

### Added

- 最大32候補・各16factorを変更せずweighted meanで比較するstateless evaluator
- 0〜1 utility、正weight、候補/factor ID、安定した先頭tie-breakを持つ入力契約
- 入力順の全候補scoreとfactor別weighted utilityを再構築できるimmutable result
- null・件数・ID・重複・factor数・utility・weightを部分結果なしで区別するerror契約
- 最大境界、入力/結果不変、bit安定性、公開型面を検証するEditMode tests
- highest・weighted・tie・all lines・invalidとwide/narrow実Panelを確認するBasics sample
