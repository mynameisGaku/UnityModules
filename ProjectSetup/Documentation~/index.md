# プロジェクト初期設定（Project Setup）

Project Settingsの現在値とprofileを比較し、差分確認、backup、適用、復元を一つのEditor windowで行います。

## 最短手順

1. `Tools > Project Setup > Open`を開きます。
2. `New recommended profile`でprofile assetを作ります。
3. 対象にする設定と希望値を選びます。
4. `Preview changes`で変更前後を確認します。
5. `Apply profile`を押します。

## 対象設定

- Asset Serialization
- Version Control
- Enter Play Mode Options
- Color Space
- Run In Background
- Company Name
- Product Name
- Bundle Version

各項目はprofile側で個別に無効化できます。無効な項目は適用しません。

## 安全性

- import時やUnity起動時の自動適用はありません。
- 差分がない場合はfileを書きません。
- Apply直前の全対象値を`ProjectSettings/ProjectSetupLastBackup.json`へ保存します。
- 書込後に値を再取得し、一致を検証します。
- `Restore last backup`は復元内容をPreviewしてから実行します。
- Color Space変更はAssetの再importを発生させる場合があります。

## 責務外

TagManager、Layer、Physics matrix、Build Profile、package導入、Scene作成、folder templateは扱いません。公開APIだけで安全に読み書きできるProject Settingsへ範囲を限定しています。
