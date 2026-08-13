# Changelog

このパッケージの変更履歴。書式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に、
バージョン番号は [Semantic Versioning](https://semver.org/lang/ja/) に従う。

## [1.0.0] - 2026-08-14

### Added

- 完全な Scene Asset path を保持し、Editor では GUID から移動後の path を修復する `SceneReference`。
- Single・Additive読込、有効Scene切替、Unloadを1件ずつ実行する `SceneFlowService`。
- 開始前条件と完了後の実Scene状態を検査し、想定内の失敗を `SceneFlowResult` と `SceneFlowError` で返す操作契約。
- `SceneFlowStatus`、`StatusChanged`、`Finished` による進捗・状態・完了通知と、購読者単位の例外隔離。
- active Build Profileの実効Scene Listに対する登録・有効状態を表示するPropertyDrawer。
- 同名Scene、重複要求、active・last SceneのUnload、外部Scene変更、終了処理を検証するテスト。
- 3つの設定済みSceneでSingle・Additive・SetActive・Unloadを確認できる **Scene Flow Basics** サンプル。

### Notes

- 外部依存、global singleton、常駐Manager、fade UI、Addressables、偽のcancel、手動activation待機は含みません。
