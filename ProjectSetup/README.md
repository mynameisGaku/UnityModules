# プロジェクト初期設定（Project Setup）

## 30秒で分かる説明

新しいUnity Projectを作るたびに、`Project Settings`を開いて同じ値を設定し直す作業を減らすEditor専用ツールです。

Asset Serialization、Version Control、Enter Play Mode、Color Space、Player情報に加え、Tag、Layer、Sorting Layerを一つのprofileへ保存します。現在値との差分を一覧で確認してから適用し、直前の状態へ復元できます。

## できること

- 複数のProject Settingsを一つのprofile assetへ保存する。
- 現在値とprofileの差分だけを設定名・変更前・変更後でPreviewする。
- 変更前の全対象値を`ProjectSettings/ProjectSetupLastBackup.json`へ保存してから適用する。
- 適用後の値を読み直し、profileと一致しない場合は失敗として扱う。
- 最後のbackupをPreviewし、確認後にまとめて復元する。
- profileごとに対象設定を有効・無効にし、不要な設定へ触れない。
- よく使うTag、Layer、Sorting Layerを1行1名称でまとめて登録する。
- TagManagerへ適用するときは不足する名称だけを追加し、既存の名称・順序・IDを維持する。
- Layerの空きslot不足、重複名、不正な名称をProject Settingsへ書き込む前に検出する。

## 使わない方がよい場合

- Physics matrixやLayer間の衝突設定を変更したい場合。このmoduleは名称の初期登録だけを扱います。
- Build ProfileのScene一覧を管理したい場合。Scene切り替えとBuild対象の検査はSceneFlowとBuild Guardの責務です。
- packageを導入したい場合。用途別のpackage追加にはModule Installerを使ってください。
- 起動時に自動で設定を変更したい場合。このmoduleは利用者の明示操作なしにProject Settingsを書き換えません。

## 3分で試す

1. Package Managerの`Add package from git URL...`へ次を入力します。

   ```text
   https://github.com/mynameisGaku/UnityModules.git?path=/ProjectSetup#project-setup-v1.1.0
   ```

2. `Tools > Project Setup > Open`を開きます。
3. `New recommended profile`を押します。
4. 保存先を選びます。初期状態ではForce TextとVisible Meta Filesだけが対象です。
5. Tag、Layer、Sorting Layerを登録する場合は、対象cardを有効にして名称を1行ずつ入力します。
6. 必要な項目だけを有効にし、`Preview changes`を押します。
7. 変更内容を確認し、`Apply profile`を押します。

## 最小コード

Runtime APIはありません。C#を書く必要はなく、Editor windowとprofile assetだけで完結します。

別Projectで同じ設定を使う場合は、作成した`ProjectSetupProfile.asset`をversion controlで共有し、同じwindowから選択します。

## 実行するとどうなるか

- Previewには、実際に変わる設定だけが固定順で表示されます。
- Apply前に直前の全対象値がbackupされます。
- Apply後はProject Settingsの現在値を再取得して一致を確認します。
- `Restore last backup`を使うと、最後にApplyする直前の状態へ戻ります。
- 差分がない場合はProject Settingsとbackup fileを変更しません。
- 通常のApplyはTag、Layer、Sorting Layerを削除・改名・並べ替えません。不足分だけ追加します。
- Restoreはbackup時点のTagManager配列とSorting Layer IDを正確に戻します。

## よくある問題

### Applyが押せない

profileが未選択、入力値が不正、Play Mode中、script compile中、またはPreview対象の差分がない場合はApplyできません。window上部のstatusを確認してください。

### Color Space変更に時間がかかる

UnityはColor Space変更時にAssetを再importする場合があります。変更内容をPreviewし、作業時間を確保してから実行してください。

### Enter Play Modeを速くしたらstatic stateが残る

Domain Reloadを無効にすると、static fieldやstatic eventを利用側が明示的に初期化する必要があります。profileでは既定でこの設定を対象外にしています。

`Use custom reload options`を有効にする場合は、Domain ReloadまたはScene Reloadの少なくとも一方を無効化対象に選んでください。何も選ばない組合せはPreviewで不正として表示されます。

### backupをversion controlへ入れるべきか

`ProjectSettings/ProjectSetupLastBackup.json`は直前復元用のローカル作業fileです。Project固有の共有設定はprofile assetをversion controlへ追加してください。

### 既存のTagやLayerが消えないか

`Apply profile`では消えません。profileに書いた名称のうち、現在存在しないものだけを追加します。削除や順序変更が必要な場合はUnity標準のProject Settingsで明示的に行ってください。

`Restore last backup`だけは、Apply直前の状態へ戻すため、直前backup以降に追加した名称を取り除く場合があります。復元内容は実行前にPreviewされます。

## 詳しい契約

- Editor専用で、Player buildへRuntime assemblyを追加しません。
- profileで無効な項目は読み取り比較には使えても、Applyでは変更しません。
- Applyは現在値のsnapshot、backup保存、設定書込、再読取検証の順に実行します。
- 書込または検証に失敗した場合は、取得済みsnapshotを使って可能な範囲で元へ戻します。
- backup fileはUTF-8 BOMなしのJSONです。一時fileへflushしてから同じfolderの最終fileへ置き換えます。
- backup schema v2はTag、Layer、Sorting Layerの名称・slot・順序・IDを保存します。v1 backupも読み取れますが、TagManagerは復元対象にしません。
- profile asset、backup、Project Settingsの変更はmain threadのEdit Modeだけで実行します。

## 対応環境

- Unity 6000.5.7f1以降
- Editor専用
- 追加のregistry package依存なし
