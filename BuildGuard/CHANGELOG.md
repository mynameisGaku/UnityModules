# Changelog

このパッケージの変更履歴。書式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に、バージョン番号は [Semantic Versioning](https://semver.org/lang/ja/) に従います。

## [1.6.0] - 2026-08-26

### Added

- Project windowで直接選択した`Assets/`配下の保存済みSceneを、Build Profileへの登録や有効状態に関係なく検査する手動scan。
- 最大4,096件の選択asset候補から最大256件のSceneをpath順で重複除去し、`Use Current Selection`で固定するsnapshot flow。
- capture後に移動・削除されたSceneをstaleとして拒否し、partial resultを返さず再選択を案内する安全境界。

### Changed

- loaded Sceneは未保存のcurrent in-memory状態を検査し、closed Sceneはadditiveで一時的に開いて保存せず閉じる既存のScene走査を選択Sceneへ共有。
- build callback、build Scene scan、Missing Script修復、選択Prefab scan、Prefab structural override reviewの契約は変更せず維持。

## [1.5.0] - 2026-08-25

### Added

- active Build Profileで有効なSceneのPrefab structural overrideを一覧化する、build blockerとは独立したreview window。
- Added／Removed GameObject・Componentだけを最大1,000件のsnapshotとして安定表示するmanual review flow。
- finding選択前の再scanとidentity照合、loaded Sceneでのobject選択、closed Sceneを開いたままにしないScene asset案内。
- cancel／scan failure時のpartial result破棄、stale finding、表示上限、Scene open／active／dirty状態保全に対するEditor回帰検証。

### Changed

- package説明と利用手順へ、Property Modificationを除外し、Apply／Revert／保存やPlayer build停止を行わないreview境界を追加。

## [1.4.0] - 2026-08-22

### Added

- Project windowで選択したPrefabを一時展開し、Missing ScriptとMissing Object Referenceを一覧化する手動scan。
- Prefab Modeで対象GameObjectを開き、Missing ScriptだけをUndo付きで除去して未保存状態へ残す明示操作。
- path順、重複除去、cancel、Prefab非変更、navigation、Undo復元に対するEditor回帰検証。
- 壊れたPrefabを共有branchへ残さず試せるtext template。

### Changed

- 利用者向け名称を「プロジェクト不備確認・修復」へ広げ、SceneとPrefabの操作手順をREADME冒頭で分離。

## [1.3.0] - 2026-08-22

### Added

- manual scanのMissing Script結果から対象SceneとGameObjectを開き、missing MonoBehaviour slotだけを除去する明示操作。
- 除去前の確認、Hierarchy全体のUndo記録、Sceneを自動保存しないreview境界。
- 除去後のdirty状態とUndo復元、Missing Object Referenceを変更しないEditor回帰検証。

### Changed

- READMEとSample手順を、問題の検出から安全な修復確認まで一続きで分かる構成へ更新。

## [1.2.0] - 2026-08-22

### Added

- active Build Profileで有効なSceneをbuild前に手動scanするEditorWindow。
- Missing ScriptとMissing Object Referenceを同じ一覧へ表示し、対象SceneとGameObjectを開く操作。
- scanの取消、結果copy、閉じたSceneの一時読込、元のactive Scene復元に対するEditor回帰検証。

### Changed

- 自動build検査と手動scanが同じScene走査・階層path・ruleを共有する構造へ整理。
- READMEを、手動scan、自動build停止、担当範囲の順で利用手順が分かる構成へ更新。

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
