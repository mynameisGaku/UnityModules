# Changelog

このパッケージの変更履歴。書式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に、バージョン番号は [Semantic Versioning](https://semver.org/lang/ja/) に従います。

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
