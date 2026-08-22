# プロジェクト一括設定（Project Setup）1.7.0

基本フォルダー、Project設定、C#生成時のRoot Namespaceと改行方式、複製時の命名規則、Play Modeの開始Scene、条件付きコンパイル記号、Tag/Layer、Build Scenesをprofile化し、差分確認、backup、適用、復元を1つのEditor windowで行います。

## 最短手順

1. `Tools > Project Setup > Open`を開きます。
2. `New recommended profile`でprofile assetを作ります。
3. 必要な設定だけを有効にします。
4. `Preview changes`で変更前後を確認します。
5. `Apply profile`を押します。

import時やUnity起動時には適用しません。

## 対象

- Asset Serialization
- Version Control
- Enter Play Mode Options
- Play Mode Start Scene
- Scripting Define Symbols
- Root Namespace
- New Script Line Endings
- Duplicate Naming（GameObject suffix、連番桁数、Asset copy spacing）
- Project Folders（`Assets/...`配下の不足フォルダー）
- Color Space
- Run In Background
- Company Name
- Product Name
- Bundle Version
- Build Scenesの順序とEnabled状態
- Tags
- User Layers
- Sorting Layers

Build Scenesは選択中Build Profileの実効一覧を扱います。独自一覧を使うBuild Profileではそのprofile assetを、global一覧を継承するprofileではglobal一覧を更新します。

Play Mode Start Sceneは、どのSceneを編集中でもPlay時に読み込むEditor専用の開始Sceneです。空欄は現在開いているSceneを使う通常動作を表します。Playerの起動Sceneとbuild対象はBuild Scenesで別に管理します。

Scripting Define Symbolsは、現在選択中のbuild targetに不足する記号だけを追加します。既存の記号は維持します。変更時はscriptの再コンパイルが発生します。

Root NamespaceはUnityが生成するC# projectの既定namespaceを設定します。asmdefに個別のRoot Namespaceがある場合はasmdef側が優先されます。New Script Line EndingsはApply後に新しく作成するC# scriptだけへ反映し、既存fileは変更しません。

Duplicate Namingは、GameObject複製時のsuffix形式と連番の最小桁数、Asset複製番号のspaceを1つの設定として扱います。Apply後に複製した対象だけへ反映し、既存名は変更しません。

Project Foldersは、profileに列挙した`Assets/...`配下の不足フォルダーと親フォルダーだけを作成します。初期profileでは無効です。Restoreは直前のApplyが作成したフォルダーを深い階層から確認し、空のものだけを削除します。既存フォルダーや内容が追加されたフォルダーは削除しません。

## 安全性

- 各項目はprofile側で個別に無効化できます。
- Previewには実際に変わる項目だけを固定順で表示します。
- Apply前に`ProjectSettings/ProjectSetupLastBackup.json`へ保存します。
- 書込後に値を再取得し、一致を検証します。
- 失敗時はApply前のsnapshotから復元を試みます。
- Restoreも差分をPreviewしてから実行します。
- Build Profileがbackup時から変わった場合、Build Scenesの復元を停止します。
- build targetがbackup時から変わった場合、Scripting Define Symbolsの復元を停止します。
- backup schema v8はProject Foldersの作成履歴、Root Namespace、新規scriptの改行方式、Duplicate Naming、Play Mode Start Scene、Scripting Define Symbols、TagManager、Build Scenesを含みます。

通常のApplyではTag、Layer、Sorting Layerの既存項目を削除・改名・並べ替えません。Build Scenesはprofileの一覧へ完全に置き換えるため、順序とEnabled状態をPreviewで確認してください。

## 対象外

Physics matrix、Layer collision、Scene Asset作成、package導入、自動適用は扱いません。
