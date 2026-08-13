# Changelog

このパッケージの変更履歴。書式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に、バージョン番号は [Semantic Versioning](https://semver.org/lang/ja/) に従います。

## [1.0.1] - 2026-08-14

### Fixed

- 最小UPM構成でもUI Toolkit型を解決できるよう、組込UI Toolkitモジュール `com.unity.modules.uielements` 1.0.0を明示的な依存関係として追加。

## [1.0.0] - 2026-08-14

### Added

- UI Toolkitの単色オーバーレイで実行するCoverとReveal。
- 色、時間、補間方法、非スケール時間を明示する要求型。
- 0秒から3600秒までを受理し、長時間でも進捗精度を保つ時間計算。
- 状態、進捗、結果、失敗理由を値として確認できる実行契約。
- 同時要求をBusyとして拒否する直列化と、通知先例外の分離。
- Controllerの無効化、破棄、アプリケーション終了に合わせた終了処理。
- 実Panel上の配置、不透明度、停止中の進行を検証するPlayModeテストと、Playerでの画面読取検証。
- Cover、Reveal、自動デモを実画面で確認できる **Screen Transition Basics** サンプル。

### Notes

- Scene読込、入力ロック、音声、Addressables、global singleton、実行後cancel、利用側Panel Settingsの変更は含みません。
