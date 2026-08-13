# Changelog

このパッケージの変更履歴。書式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に、
バージョン番号は [Semantic Versioning](https://semver.org/lang/ja/) に従う。

## [1.0.0] - 2026-08-13

### Added

- 型、データ版、UTC 保存時刻を含む JSON 保存形式と、`SaveService` による同期保存、読み込み、削除。
- 主データまたはバックアップが存在する名前を、固定済みの読み取り専用一覧で返す `SaveSlotListResult`。
- `SaveOperationResult`、`SaveLoadResult<T>`、`SaveError` による例外に依存しない失敗処理。
- SHA-256 チェックサムによる偶発的な破損検出。暗号化、認証、改ざん防止は対象外。
- 一時ファイルへの耐久書き込み、可能な環境での単一置換、直前データの 1 世代バックアップを行う `FileSaveStorage`。
- 主データの破損または消失時に、検証済みバックアップを読み込んで主ファイルへ戻す自動復旧。
- 型不一致、データ版不一致、保存形式版不一致を破損と区別し、保存ファイルを変更せずに返す読込処理。
- 文字、数字、ハイフン、アンダースコアを 64 文字まで許可し、予約名と経路文字を拒否する `SaveSlot`。
- `ISaveStorage` と `ISaveSerializer` による保存先と変換形式の差し替え。
- 外部依存のない `UnityJsonSaveSerializer` と、対応する保存ルート型を事前検証する標準構成。
- 同期処理、同一スロットの非並行契約、`FileSaveStorage` の WebGL Player と tvOS Player 非対応を記載した利用文書。
- 設定済み Scene を Play し、再生回数とコイン数が再起動を跨いで残ることを確認できる **Save System Basics** サンプル。
- 実ファイル、置換失敗、バックアップ復旧、結果型の境界を検証する EditMode テスト。
