# Changelog

このパッケージの変更履歴。書式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に、バージョン番号は [Semantic Versioning](https://semver.org/lang/ja/) に従います。

## [1.0.0] - 2026-08-14

### Added

- 利用側が明示追加するcontextとbreadcrumbの有界保持。
- ownerが有効な間だけUnityのWarning、Error、Assert、Exceptionを取得するlive subscription。
- 明示操作時だけ`persistentDataPath`配下へ保存する決定論的JSON report。
- reasonをfile pathへ使用せず、専用directoryからの逸脱を防ぐ保存境界。
- 同一directoryの一時fileを使い、成功時に一時fileを残さない書出し処理。
- 状態件数、直近結果、保存path、owner再作成を実画面で確認できる **Diagnostics Context Basics** サンプル。
- 実Button callback、JSON parse、path封じ込め、一時file不在、worker warning、PanelSettingsの実RenderTextureを使った960x600と640x360の配置を確認するPlayModeテスト。
- 640x360ではtitle、説明、privacy、件数、状態、操作Buttonの文字と余白を縮め、全表示をcard安全領域内へ保つresponsive配置。
- Basics sampleの実行環境を明示するUnity組込みUI Toolkit module依存。

### Notes

- 自動的なdevice・user・hardware識別情報収集、crash後のreport生存保証、upload、暗号化、圧縮、retention、global singletonは含みません。
