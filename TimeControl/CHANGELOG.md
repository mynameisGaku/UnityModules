# Changelog

このパッケージの変更履歴。書式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に、バージョン番号は [Semantic Versioning](https://semver.org/lang/ja/) に従います。

## [1.0.0] - 2026-08-14

### Added

- 0以上100以下の相対倍率を所有できる`TimeScaleLease`。
- 複数leaseの最小倍率を基準値へ掛け、pause・slow motion・単独fast-forwardを両立するController。
- 基準値、実効倍率、実効値、lease数、制御可否、失敗理由を値として確認できる状態契約。
- stale leaseと重複`Dispose`を無害化するowner世代管理。
- 別threadおよび通知中の`Dispose`をメインスレッド更新へ直列化する終了処理。
- 通知先例外の分離と、通知中の新規取得を`Busy`として拒否する再入制御。
- 外部の`Time.timeScale`書込みを検出し、外部値を保持して停止する競合処理。
- Controllerの無効化、破棄、アプリケーション終了に合わせた基準値復元とlease無効化。
- Pause・Slow・Fast・入れ子デモと2種類の時間経過を実画面で確認できる **Time Control Basics** サンプル。
- 実時間deadlineでpause中の操作と、960x600および640x600でのButton配置を確認するPlayModeテスト。
- Basics sampleの実行環境を明示するUnity組込みUI Toolkit module依存。

### Notes

- 第三者package、timer、期限付きhit-stop、`Time.fixedDeltaTime`、入力、音声、Animator、物理、network、永続化、Scene Flow・Screen Transition連携、global singletonは含みません。
