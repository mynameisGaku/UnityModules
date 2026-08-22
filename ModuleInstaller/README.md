# モジュール管理アシスタント（Module Manager）

## 30秒で分かる説明

Unity Package ManagerへGit URLを1件ずつ貼り、導入済みmoduleの公開versionを手作業で調べて差し替える作業を減らすEditor専用ツールです。

`Tools > Module Manager > Open`を開き、「プロジェクト整理」「Scene・UI」「ゲーム判定・計算」などの目的別セットを1回選ぶと、対応する公開tagをまとめて導入できます。導入済みmoduleに古いversionがあれば、一覧で確認して1回の操作で更新できます。

## できること

- 用途別の6セットから、必要なmodule群をまとめて導入する。
- 40個の公開moduleを詳細一覧から1件ずつ導入する。
- 既に導入済みのpackageを自動で除外する。
- 導入済みのcatalog moduleを調べ、古いversionだけを固定済み公開tagへまとめて更新する。
- 最新version、catalogより新しいversion、独自versionは自動で上書きしない。
- `Assets/Modules/<Folder>`に同じmoduleのcopyがある場合、assembly重複を避けるため導入前に停止する。
- `main`や`dev`ではなく、一覧に固定した公開tagのGit URLだけをPackage Managerへ渡す。
- package追加によるdomain reload後も、同じUnity session内で導入結果を確認する。
- 各buttonを`Install N`、`Installed`、`Resolve conflict`へ切り替え、次に必要な操作を示す。

## 使わない方がよい場合

- packageを削除したい場合。意図しない依存関係の削除を避けるため、削除はPackage Managerで個別に行います。
- 独自forkや別repositoryのpackageを管理したい場合。URLの任意入力は扱いません。
- `Assets/Modules`へsource copyする運用を続けたい場合。その場合はこのツールから同じmoduleをUPM導入しないでください。

## 3分で試す

1. Unityの`Window > Package Management > Package Manager`を開きます。
2. `Add package from git URL...`へ次を入力します。

   ```text
   https://github.com/mynameisGaku/UnityModules.git?path=/ModuleInstaller#module-installer-v1.3.5
   ```

3. `Tools > Module Manager > Open`を開きます。
4. 最初は`Project Maintenance`を確認します。
5. cardに並ぶmodule名と追加件数を確認し、`Install 5`のように表示されたbuttonを押します。
6. Package Managerの解決とscript reloadが終わるまで待ちます。
7. 導入済みmoduleの更新がある場合は、上部の`Update N`に対象名とversionが表示されます。内容を確認してbuttonを押します。

## 最小コード

Runtime APIはありません。C#を書く必要はなく、Editor windowの操作だけで完結します。

1件だけ導入したい場合は、window下部の`Advanced: install one module`を開き、対象行の`Install`を押します。

## まずどれを選ぶか

普段は40件の個別一覧を先に読む必要はありません。やりたい作業に最も近いセットを1つ選び、cardに表示されたmodule名と件数を確認してください。

| やりたいこと | 最初に見るセット |
|---|---|
| 新しいProjectのC# namespace・改行方式、条件付きコンパイル記号、Build Scenes、Play Mode開始Scene、壊れた参照、Asset整理をまとめて扱う | `Project Maintenance` |
| Scene切り替え、画面fade、safe area、pause、起動順を整える | `Scene and UI` |
| save、音声、不具合reportを用意する | `Game Services` |
| 入力の補助やGameplay入力の一時停止を追加する | `Input Support` |

`Deterministic Simulation`と`Game Rules and Math`は、決定論的simulationや細かな計算部品が本当に必要な場合にだけ選びます。`Advanced: install one module`は既存projectとの互換や、必要なmoduleが明確な場合の入口です。

## 実行するとどうなるか

- `Packages/manifest.json`へ、選択したmoduleのtag固定Git URLが追加されます。
- `Packages/packages-lock.json`へ、解決したcommit SHAと依存関係が記録されます。
- 導入済みpackageは再追加されません。
- 更新時は、導入済みversionがcatalogの公開versionより古いpackageだけに固定tag URLを再指定します。
- 同じversion、より新しいversion、数値として比較できない独自versionは変更しません。
- `Assets/Modules`に同名folderがある場合はmanifestを変更せず、解消方法をwindowへ表示します。
- Package Managerが失敗した場合は、最初の失敗内容を表示して処理を終了します。無限再試行はしません。

## 用途別セット

| セット | 含まれる用途 |
|---|---|
| Project Maintenance | Project Settings、C# Root Namespace、新規script改行方式、複製時の命名規則、条件付きコンパイル記号、Tag・Layer、Build Scenes、Play Mode開始Scene、Inspector整理、debug描画、Scene・Prefab不備修復、Asset参照・名前整理 |
| Scene and UI | Scene切り替え、画面fade、safe area、ゲーム時間、起動手順 |
| Game Services | save data、音声再生、不具合report |
| Input Support | stick・button補助、Gameplay入力の一時停止 |
| Deterministic Simulation | 固定step、再現乱数、state照合、replay、canonical data、固定小数点、handle |
| Game Rules and Math | resource、能力値、条件、選択、配分、stack、定期処理、damage、threat |

Project Maintenanceに含まれる「プロジェクト一括設定」はv1.6.0へ固定しています。C# Root Namespace、新規scriptの改行方式、複製時のGameObject・Asset命名規則、条件付きコンパイル記号、Player Build Scenes、EditorのPlay Mode開始Sceneを別々の項目として同じprofileから適用・復元できます。Root Namespaceと改行方式は今後生成するC# fileへ、複製命名は今後の複製操作へだけ適用され、既存source・既存GameObject・既存Assetは書き換えません。条件付きコンパイル記号は現在の記号を削除せず、不足分だけを追加します。Play Mode開始Sceneを空欄にすれば、現在開いているSceneから始めるUnity標準動作を維持します。

## よくある問題

### `Assets/Modules/... already exists`と表示される

同じassemblyをAssets copyとUPM packageの両方から読み込むと、型やasmdefが重複します。どちらの導入方法を使うか決め、UPMへ移行する場合は既存copyをversion controlで退避・削除してから再実行してください。

### package追加後にwindowが閉じた

Package Managerの解決でdomain reloadが起きる場合があります。`Tools > Module Manager > Open`から再度開いてください。導入・更新queueは同じUnity session内で復元されます。

### 一部だけ導入したい

`Advanced: install one module`から個別に選んでください。既存の細分化packageは互換入口として残っていますが、新規導入では目的別セットを推奨します。

### packageを削除したい

Package Managerから対象packageを個別にRemoveしてください。このversionは意図しない一括削除を避けるため、削除機能を持ちません。

## 詳しい契約

- Editor専用で、Player buildへRuntime assemblyを追加しません。
- module一覧・folder・tag・Git URLはpackage内の固定catalogです。
- bundle導入と一括更新は`Client.AddAndRemove`へ対象URLだけを一括で渡し、削除要求は渡しません。
- unknown package、Assets copy競合、既存処理中はPackage Managerを呼びません。
- 進行中の選択は`SessionState`に保持し、Unity再起動後まで永続化しません。
- package追加の成否はPackage Managerが返す結果に従います。repositoryへのnetwork接続とGitが必要です。

## 対応環境

- Unity 6000.5.7f1以降
- Editor専用
- 追加のregistry package依存なし
