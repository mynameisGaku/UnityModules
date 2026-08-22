# プロジェクト一括設定（Project Setup）

Unityの新規Projectで毎回行う設定を、1つのprofileからまとめてPreview・適用・復元するEditor専用ツールです。

`Assets`配下の基本フォルダー、Runtime／Editor／test asmdef、`Project Settings`、C#のRoot Namespaceと改行方式、複製時の命名規則、Play Modeの開始Scene、条件付きコンパイル記号、Tag、Layer、Sorting Layer、Build Scenesを別々の画面やscriptで設定する手間を減らします。自動適用はせず、実際に変わる項目を確認してから実行できます。

## まず知りたいこと

| 質問 | 答え |
|---|---|
| 何が楽になる？ | 基本フォルダーとasmdefの作成、Project設定、C#生成時の既定値、複製名、Play Modeの開始Scene、条件付きコンパイル記号、Tag/Layer、Build Scenesを1つのprofileからまとめて設定できます。 |
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

## 目的から選ぶ

| やりたいこと | 有効にするcard |
|---|---|
| 新規Projectの基本フォルダーを作る | `Project Folders` |
| Runtime、Editor、EditMode test、PlayMode testをasmdefで分離する | `Script Assemblies` |
| どのSceneからでもBootstrap SceneでPlayする | `Play Mode Start Scene` |
| Playerへ入れるSceneと順序をそろえる | `Build Scenes` |
| Tag、Layer、compile記号を手入力せず追加する | `Tags`、`Layers`、`Scripting Define Symbols` |
| 直前の一括変更を戻す | `Restore last backup` |

## できること

### Project設定

- Asset Serialization
- Version Control
- Enter Play Mode Options
- Play Mode Start Scene
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

### 条件付きコンパイル記号

- 現在選択中のbuild targetへ必要なScripting Define Symbolsをまとめて追加する。
- 既存の記号を削除せず、profileにあって現在不足している記号だけを追加する。
- 記号ごとの追加予定をPreviewで確認する。
- Apply直前の記号一覧と対象build targetをbackupし、同じtargetへ正確に復元する。

`DEVELOPMENT_TOOLS`や`USE_STEAMWORKS`のような機能切替を、`Player Settings`を開いてProjectごとに手入力する作業を減らせます。

### C#生成時の既定値

- Root NamespaceをProjectごとに統一する。
- Unityから新規作成するC# scriptの改行方式を`OS Native`、`Unix`、`Windows`から選ぶ。
- 空のRoot Namespaceも明示的に適用し、Unity標準のnamespaceなしへ戻す。

asmdefに個別のRoot Namespaceが設定されているscriptではasmdef側が優先されます。この項目はProject全体の既定値をそろえる用途です。

### 複製時の命名規則

- GameObject複製時のsuffixを`Prefab (1)`、`Prefab.1`、`Prefab_1`から選ぶ。
- 連番の最小桁数を1から9でそろえる。3なら`Prefab_001`のようになります。
- Asset複製時に番号の前へspaceを入れるか選ぶ。

3項目は`Duplicate Naming` cardの1つのtoggleでまとめて所有します。既存GameObjectやAssetは改名せず、Apply後の複製名だけが変わります。

### Projectの基本フォルダー

- `Assets/Art`、`Assets/Audio`、`Assets/Prefabs`、`Assets/Scenes`、`Assets/Scripts`、`Assets/Settings`を推奨候補として用意する。
- Projectごとに必要な`Assets/...` pathへ変更できる。
- 親フォルダーがない場合も、Previewへ表示して浅い階層から作成する。
- 既にあるフォルダーやAssetには触れない。

初期profileでは無効です。`Project Folders` cardの`Create missing folders`を有効にした時だけ、不足フォルダーを作成します。

### Runtime／Editor／test asmdefの作成

- Runtime用の`Game.asmdef`とEditor専用の`Game.Editor.asmdef`をまとめて作る。
- Editor asmdefからRuntime asmdefへの参照と`Editor` platform制限を自動設定する。
- 任意で`Game.Tests.asmdef`と`Game.PlayMode.Tests.asmdef`も作り、必要なRuntime／Editor参照と`TestAssemblies`参照を自動設定する。
- assembly名、Runtime folder、Editor folderをProjectごとに変更する。
- 既存asmdefや既存fileを上書きしない。
- 同じfolderに別のasmdefがある場合は、実行前のPreviewで停止する。

初期profileでは無効です。asmdefを使わないProjectには何も作成しません。

## Runtime／Editor／test asmdefをまとめて作る

1. `Script Assemblies` cardで`Create missing Runtime and Editor assemblies`を有効にします。
2. `Assembly name`へ`Game`や`Studio.Game`を入力します。
3. Runtime codeを置くfolderと、その配下のEditor code用folderを指定します。
4. test assemblyも必要な場合は`Include EditMode and PlayMode test assemblies`を有効にし、test root folderを指定します。
5. Previewで新規asmdefと不足folderを確認し、Applyします。

既定値では`Assets/Scripts/Game.asmdef`と`Assets/Scripts/Editor/Game.Editor.asmdef`を作ります。test assemblyを有効にすると、`Assets/Tests/EditMode/Game.Tests.asmdef`と`Assets/Tests/PlayMode/Game.PlayMode.Tests.asmdef`も追加します。EditMode側は`Game`と`Game.Editor`を参照してEditorだけでcompileされ、PlayMode側は`Game`を参照します。assembly名には`.`で区切ったC#識別子を使用してください。

Restoreが削除するのは、直前のApplyが作成し、内容が作成時から変わっていないasmdefだけです。利用者が編集したasmdef、Apply前からあったfile、別名のasmdefは削除しません。asmdefを削除した後に空になった作成folderだけを続けて削除します。

## 基本フォルダーをまとめて作る

1. `Project Folders` cardで`Create missing folders`を有効にします。
2. 1行に1つ、`Assets/Scripts/Runtime`のようなpathを入力します。
3. Previewで作成対象を確認してApplyします。
4. 不要になった場合は`Restore last backup`を実行します。

Restoreが削除するのは、直前のApplyが作成し、復元時点でも空のフォルダーだけです。既存フォルダー、利用者がfileや子フォルダーを追加した場所、Applyより前から存在した場所は削除しません。

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

## どのSceneを開いていてもBootstrapからPlayする

1. `Play Mode Start Scene` cardで`Apply this setting`を有効にします。
2. `Start Scene`へBootstrapやEntry Sceneを指定します。
3. Previewで`Currently open Scenes -> Assets/...`の差分を確認します。
4. Apply後は、別のSceneを編集中でもPlayすると指定Sceneから開始します。

Scene欄を空にしてApplyすると、固定開始Sceneを解除し、現在開いているSceneからPlayする通常動作へ戻ります。Scene参照はGUIDで保存するため、version control上で同じ`.meta`を保ったまま移動しても追従します。

`Play Mode Start Scene`はEditorでPlayを押した時だけ使います。Playerの起動Sceneとbuild対象は`Build Scenes`で別に設定します。

## 条件付きコンパイル記号の使い方

1. `Scripting Define Symbols` cardで`Apply this setting`を有効にします。
2. `Required symbols`へ、1行に1つずつ必要な記号を入力します。
3. Previewで現在の記号と追加後の記号を確認します。
4. Applyすると、選択中のbuild targetに不足する記号だけを追加します。

使用できる文字は英字、数字、underscoreです。先頭に数字は使えません。最大64個、1記号64文字までです。

## C#生成時の既定値をそろえる

1. `Root Namespace` cardで適用を有効にし、`Studio.Game`のようなnamespaceを入力します。
2. `New Script Line Endings` cardで適用を有効にし、チームの改行方式を選びます。
3. Previewで現在値と変更後を確認してApplyします。

Root Namespaceは`.`で区切ったC#識別子だけを受け付けます。空欄を適用するとRoot Namespaceを解除します。改行方式はApply後に新しく作成したC# scriptへ適用され、既存fileの改行は書き換えません。

## 複製名をProject全体でそろえる

1. `Duplicate Naming` cardで`Apply these settings`を有効にします。
2. `GameObject suffix`で括弧、dot、underscoreから形式を選びます。
3. `Minimum number digits`で連番の最小桁数を1から9で指定します。
4. Asset copy番号の前へspaceを入れるか選び、PreviewしてApplyします。

Apply後に複製したGameObjectとAssetだけへ反映されます。既に存在する名前は変更しません。

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
- Play Mode Start Sceneを指定すると、現在開いているSceneの代わりにそのSceneを読み込んでPlayします。
- Scripting Define Symbolsを変更すると、Unityがscriptを再コンパイルし、設定によってはDomain Reloadが発生します。
- Root Namespaceを変更すると、Unityが生成する`.csproj`のRoot Namespaceが変わります。asmdef固有のRoot Namespaceは変更しません。
- New Script Line EndingsはApply後に作成するC# scriptだけへ影響し、既存scriptを一括変換しません。
- Duplicate NamingはApply後の複製名だけへ影響し、既存GameObjectやAssetを一括改名しません。
- Project Foldersは不足フォルダーだけを作成します。既存file、既存フォルダー、既存Assetの名前や場所は変更しません。
- Script Assembliesは新しいasmdefをimportするため、Unityがscriptを再コンパイルします。既存asmdefや同名fileは上書きしません。
- Build Scenesは不足分の追加ではなく、profileの一覧へ完全に置き換えます。
- RestoreはApply直前へ戻すため、その後に追加したTag/Layer/Sceneを取り除く場合があります。

## 元に戻す

1. `Tools > Project Setup > Open`を開きます。
2. `Restore last backup`を押します。
3. 復元差分を確認して`Restore`を押します。

Build Scenesを復元する場合は、backup作成時と同じBuild Profileを選択してください。別のBuild Profileへ切り替わっている場合は、誤った一覧を書き換えないよう復元を停止します。

Scripting Define Symbolsを復元する場合は、backup作成時と同じbuild targetを選択してください。別のtargetへ切り替わっている場合は、誤ったtargetを書き換えないよう復元を停止します。復元時はApply直前の記号一覧へ正確に戻すため、Apply後に手動追加した記号も取り除く場合があります。

backupはUTF-8 BOMなしのJSONです。schema v9はProject設定、Applyが作成したフォルダーとasmdefのpath・内容hash、Root Namespace、新規scriptの改行方式、複製時の命名規則、Play Mode Start Scene、Scripting Define Symbolsと対象build target、TagManager全体、Build SceneのGUID・順序・Enabled状態・保存先を保持します。schema v1からv8も読み取れますが、そのversionに存在しない項目は復元しません。

## profileを別Projectで使う

作成した`ProjectSetupProfile.asset`をversion controlへ追加し、別Projectの同じwindowで選択します。Scene参照はGUIDを使うため、Scene Assetと`.meta` fileも同じGUIDで共有してください。

## 対象外

- PhysicsやLayer collision matrix
- Scene Assetそのものの作成
- packageの導入・更新
- Play Modeやbuild時の自動適用

packageの導入・更新には`Tools > Module Manager > Open`を使います。SceneのRuntime切替にはScene Flowを使います。

## よくある問題

### Applyが押せない

profile未選択、入力値不正、参照切れScene、重複Scene、無効な先頭Scene、Play Mode中、script compile中、または差分なしが主な原因です。window上部のstatusとPreviewのerrorを確認してください。

条件付きコンパイル記号を変更した直後は再コンパイルが始まるため、完了するまで次のApplyやRestoreを待ってください。

### 既存のTagやLayerが消えないか

通常のApplyでは消えません。profileにあり、現在存在しない名称だけを追加します。削除や並べ替えはUnity標準のProject Settingsで明示的に行ってください。

### Build Scenesの既存項目が消えないか

Build Scenesをprofileで有効にすると、既存一覧をprofileの内容へ置き換えます。Apply前にPreviewを確認してください。直前の一覧はbackupから復元できます。

### backupをversion controlへ入れるべきか

`ProjectSettings/ProjectSetupLastBackup.json`は直前復元用のローカル作業fileです。共有するのはprofile assetです。

## インストール

Package Managerの`Add package from git URL...`へ次を入力します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/ProjectSetup#project-setup-v1.9.0
```

## 対応環境

- Unity 6000.5.7f1以降
- Editor専用
- 追加のregistry package依存なし
