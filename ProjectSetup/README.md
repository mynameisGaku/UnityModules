# プロジェクト一括設定（Project Setup）

Unityの新規Projectで毎回行う設定を、1つのprofileからまとめてPreview・適用・復元するEditor専用ツールです。

`Project Settings`、Tag、Layer、Sorting Layer、Build Scenesを別々の画面で設定する手間を減らします。自動適用はせず、実際に変わる項目を確認してから実行できます。

## まず知りたいこと

| 質問 | 答え |
|---|---|
| 何が楽になる？ | Project設定、Tag/Layer、Build Scenesの順序を1つのprofileからまとめて設定できます。 |
| 勝手に変更される？ | されません。import時やUnity起動時には何も適用しません。 |
| 実行前に確認できる？ | `Preview changes`で変更前と変更後を一覧表示します。 |
| 失敗したら？ | Apply前にbackupし、書込後の検証に失敗した場合は可能な範囲で自動復元します。 |
| 後から戻せる？ | `Restore last backup`で最後のApply直前へ戻せます。 |
| Runtimeへ影響する？ | ありません。Player buildへRuntime assemblyを追加しません。 |

## 最短3手順

1. `Tools > Project Setup > Open`を開きます。
2. `New recommended profile`でprofile assetを作り、必要な項目だけ有効にします。
3. `Preview changes`で確認し、`Apply profile`を押します。

初期profileは、version controlで扱いやすい`Force Text`と`Visible Meta Files`だけを対象にします。それ以外は利用者が有効にするまで変更しません。

## できること

### Project設定

- Asset Serialization
- Version Control
- Enter Play Mode Options
- Color Space
- Run In Background
- Company Name
- Product Name
- Bundle Version

### 名前の一括登録

- Tag
- User Layer（slot 8から31）
- Sorting Layer

通常のApplyでは不足する名称だけを追加します。既存の名称、順序、Layer slot、Sorting Layer IDは変更しません。

### Build Scenes

- Scene Assetを選択して順番を保存する。
- 各SceneのEnabled状態を保存する。
- `Up`と`Down`で起動順を整理する。
- Scene移動後もGUIDから参照を解決する。
- 選択中のBuild Profileが独自Scene一覧を持つ場合はその一覧を、持たない場合はglobal一覧を設定する。

Build Scenesを有効にしたprofileは、一覧全体を表示順どおりに置き換えます。先頭SceneはPlayerの起動Sceneになるため、Enabledでなければ適用できません。

## Build Scenesの使い方

1. `Build Scenes` cardで`Apply this scene list`を有効にします。
2. `Add scene`で起動Sceneを追加します。
3. 必要なSceneを追加し、`Up`と`Down`で順番を決めます。
4. build対象外に残したいSceneだけ`Enabled`を無効にします。
5. Previewで現在の順序と変更後の順序を確認します。

`Capture current`を押すと、現在のProject設定、Tag/Layer、選択中Build Profileの実効Scene一覧をprofileへ取り込めます。

## Applyすると何が変わるか

1. 現在の対象値をsnapshotとして取得します。
2. `ProjectSettings/ProjectSetupLastBackup.json`へ保存します。
3. profileで有効な項目だけを書き込みます。
4. 現在値を再取得し、profileとの一致を検証します。
5. 一致しなければ、Apply前のsnapshotから復元を試みます。

差分がない場合はProject Settingsもbackup fileも変更しません。

### 注意が必要な変更

- Color SpaceはAssetの再importを発生させる場合があります。
- Enter Play ModeでDomain Reloadを無効にすると、利用側でstatic stateの初期化が必要です。
- Build Scenesは不足分の追加ではなく、profileの一覧へ完全に置き換えます。
- RestoreはApply直前へ戻すため、その後に追加したTag/Layer/Sceneを取り除く場合があります。

## 元に戻す

1. `Tools > Project Setup > Open`を開きます。
2. `Restore last backup`を押します。
3. 復元差分を確認して`Restore`を押します。

Build Scenesを復元する場合は、backup作成時と同じBuild Profileを選択してください。別のBuild Profileへ切り替わっている場合は、誤った一覧を書き換えないよう復元を停止します。

backupはUTF-8 BOMなしのJSONです。schema v3はProject設定、TagManager全体、Build SceneのGUID・順序・Enabled状態・保存先を保持します。schema v1/v2も読み取れますが、そのversionに存在しない項目は復元しません。

## profileを別Projectで使う

作成した`ProjectSetupProfile.asset`をversion controlへ追加し、別Projectの同じwindowで選択します。Scene参照はGUIDを使うため、Scene Assetと`.meta` fileも同じGUIDで共有してください。

## 対象外

- PhysicsやLayer collision matrix
- Scene Assetそのものの作成
- packageの導入・更新
- folder template
- Play Modeやbuild時の自動適用

packageの導入・更新には`Tools > Module Manager > Open`を使います。SceneのRuntime切替にはScene Flowを使います。

## よくある問題

### Applyが押せない

profile未選択、入力値不正、参照切れScene、重複Scene、無効な先頭Scene、Play Mode中、script compile中、または差分なしが主な原因です。window上部のstatusとPreviewのerrorを確認してください。

### 既存のTagやLayerが消えないか

通常のApplyでは消えません。profileにあり、現在存在しない名称だけを追加します。削除や並べ替えはUnity標準のProject Settingsで明示的に行ってください。

### Build Scenesの既存項目が消えないか

Build Scenesをprofileで有効にすると、既存一覧をprofileの内容へ置き換えます。Apply前にPreviewを確認してください。直前の一覧はbackupから復元できます。

### backupをversion controlへ入れるべきか

`ProjectSettings/ProjectSetupLastBackup.json`は直前復元用のローカル作業fileです。共有するのはprofile assetです。

## インストール

Package Managerの`Add package from git URL...`へ次を入力します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/ProjectSetup#project-setup-v1.2.0
```

## 対応環境

- Unity 6000.5.7f1以降
- Editor専用
- 追加のregistry package依存なし
