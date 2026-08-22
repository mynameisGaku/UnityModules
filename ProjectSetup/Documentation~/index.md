# プロジェクト一括設定（Project Setup）

Project設定、Tag/Layer、Build Scenesをprofile化し、差分確認、backup、適用、復元を1つのEditor windowで行います。

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

## 安全性

- 各項目はprofile側で個別に無効化できます。
- Previewには実際に変わる項目だけを固定順で表示します。
- Apply前に`ProjectSettings/ProjectSetupLastBackup.json`へ保存します。
- 書込後に値を再取得し、一致を検証します。
- 失敗時はApply前のsnapshotから復元を試みます。
- Restoreも差分をPreviewしてから実行します。
- Build Profileがbackup時から変わった場合、Build Scenesの復元を停止します。
- backup schema v3はTagManagerとBuild Scenesを含みます。

通常のApplyではTag、Layer、Sorting Layerの既存項目を削除・改名・並べ替えません。Build Scenesはprofileの一覧へ完全に置き換えるため、順序とEnabled状態をPreviewで確認してください。

## 対象外

Physics matrix、Layer collision、Scene Asset作成、package導入、folder template、自動適用は扱いません。
