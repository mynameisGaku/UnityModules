# Changelog

このパッケージの変更履歴。書式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に、バージョン番号は [Semantic Versioning](https://semver.org/lang/ja/) に従います。

## [1.1.0] - 2026-08-22

### Added

- Scene保存後に削除されたTexture、Material、Prefabなどを指すObject Reference fieldの検出。
- GameObject階層、Component型と順番、serialized property pathを示す統合失敗message。
- 削除済みRenderTextureとCameraの参照を使った決定論的なEditor回帰検証。

### Changed

- READMEを、用途、3分の導入手順、messageの読み方、対象外が先に分かる構成へ更新。
- 実装code、test code、診断message内の説明を英語へ統一。

## [1.0.0] - 2026-08-14

### Added

- 実際のPlayer build Scene一覧を毎回確認するbuild開始前preflight。
- Unityが処理する一時Sceneを確認する`IProcessSceneWithReport` callback。
- active・inactive階層とPrefab instanceを対象にしたMissing MonoBehaviour検出。
- Scene path、兄弟index付き階層path、GameObject別件数、合計件数を示す決定論的な失敗message。
- 元のScene開閉状態とactive Sceneを維持し、自動修復や保存を行わない検査境界。
- build可能Sceneと安全な失敗用text templateを含む **Build Guard Basics** サンプル。

### Notes

- Runtime code、一般asset scan、自動修復、project固有build policyは含みません。
