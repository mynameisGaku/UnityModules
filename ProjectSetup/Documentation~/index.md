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
- Tags
- User Layers（slot 8から31）
- Sorting Layers

各項目はprofile側で個別に無効化できます。無効な項目は適用しません。Tag、Layer、Sorting Layerは1行1名称で入力し、通常のApplyでは不足分だけを追加します。

## 安全性

- import時やUnity起動時の自動適用はありません。
- 差分がない場合はfileを書きません。
- Apply直前の全対象値を`ProjectSettings/ProjectSetupLastBackup.json`へ保存します。
- 書込後に値を再取得し、一致を検証します。
- `Restore last backup`は復元内容をPreviewしてから実行します。
- Color Space変更はAssetの再importを発生させる場合があります。
- TagManagerの変更前には重複名、文字数、Layerの空きslot数を検証します。
- Tag、Layer、Sorting Layerの既存項目は通常のApplyで削除・改名・並べ替えません。
- backup schema v2はTagManager全体を保存し、Restore時には名称、slot、順序、Sorting Layer IDを正確に戻します。

## 責務外

Physics matrix、Build Profile、package導入、Scene作成、folder templateは扱いません。TagとLayerは名称の初期登録だけを扱い、Physicsやcollisionの設定には触れません。
