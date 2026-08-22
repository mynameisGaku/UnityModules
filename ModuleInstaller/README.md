# モジュール導入アシスタント（Module Installer）

## 30秒で分かる説明

Unity Package ManagerへGit URLを1件ずつ貼り、似た名前の小さなpackageから必要なものを選ぶ作業を減らすEditor専用ツールです。

`Tools > Module Installer > Open`を開き、「プロジェクト整理」「Scene・UI」「ゲーム判定・計算」などの目的別セットを1回選ぶと、対応する公開tagをまとめて導入します。既存packageの型や名前空間は変更しないため、公開済みmoduleとの互換性も保てます。

## できること

- 用途別の6セットから、必要なmodule群をまとめて導入する。
- 40個の公開moduleを詳細一覧から1件ずつ導入する。
- 既に導入済みのpackageを自動で除外する。
- `Assets/Modules/<Folder>`に同じmoduleのcopyがある場合、assembly重複を避けるため導入前に停止する。
- `main`や`dev`ではなく、一覧に固定した公開tagのGit URLだけをPackage Managerへ渡す。
- package追加によるdomain reload後も、同じUnity session内で導入結果を確認する。
- 各buttonを`Install N`、`Installed`、`Resolve conflict`へ切り替え、次に必要な操作を示す。

## 使わない方がよい場合

- packageを更新・削除したい場合。このversionは新規導入だけを扱います。
- 独自forkや別repositoryのpackageを管理したい場合。URLの任意入力は扱いません。
- `Assets/Modules`へsource copyする運用を続けたい場合。その場合はこのツールから同じmoduleをUPM導入しないでください。

## 3分で試す

1. Unityの`Window > Package Management > Package Manager`を開きます。
2. `Add package from git URL...`へ次を入力します。

   ```text
   https://github.com/mynameisGaku/UnityModules.git?path=/ModuleInstaller#module-installer-v1.1.0
   ```

3. `Tools > Module Installer > Open`を開きます。
4. 最初は`Project Maintenance`を確認します。
5. cardに並ぶmodule名と追加件数を確認し、`Install 5`のように表示されたbuttonを押します。
6. Package Managerの解決とscript reloadが終わるまで待ちます。

## 最小コード

Runtime APIはありません。C#を書く必要はなく、Editor windowの操作だけで完結します。

1件だけ導入したい場合は、window下部の`Advanced: install one module`を開き、対象行の`Install`を押します。

## 実行するとどうなるか

- `Packages/manifest.json`へ、選択したmoduleのtag固定Git URLが追加されます。
- `Packages/packages-lock.json`へ、解決したcommit SHAと依存関係が記録されます。
- 導入済みpackageは再追加されません。
- `Assets/Modules`に同名folderがある場合はmanifestを変更せず、解消方法をwindowへ表示します。
- Package Managerが失敗した場合は、最初の失敗内容を表示して処理を終了します。無限再試行はしません。

## 用途別セット

| セット | 含まれる用途 |
|---|---|
| Project Maintenance | Project初期設定、Inspector整理、debug描画、Scene・Prefab不備修復、Asset参照・名前整理 |
| Scene and UI | Scene切り替え、画面fade、safe area、ゲーム時間、起動手順 |
| Game Services | save data、音声再生、不具合report |
| Input Support | stick・button補助、Gameplay入力の一時停止 |
| Deterministic Simulation | 固定step、再現乱数、state照合、replay、canonical data、固定小数点、handle |
| Game Rules and Math | resource、能力値、条件、選択、配分、stack、定期処理、damage、threat |

## よくある問題

### `Assets/Modules/... already exists`と表示される

同じassemblyをAssets copyとUPM packageの両方から読み込むと、型やasmdefが重複します。どちらの導入方法を使うか決め、UPMへ移行する場合は既存copyをversion controlで退避・削除してから再実行してください。

### package追加後にwindowが閉じた

Package Managerの解決でdomain reloadが起きる場合があります。`Tools > Module Installer > Open`から再度開いてください。導入済みpackageは自動で除外されます。

### 一部だけ導入したい

`Advanced: install one module`から個別に選んでください。既存の細分化packageは互換入口として残っていますが、新規導入では目的別セットを推奨します。

### packageを削除したい

Package Managerから対象packageを個別にRemoveしてください。このversionは意図しない一括削除を避けるため、削除機能を持ちません。

## 詳しい契約

- Editor専用で、Player buildへRuntime assemblyを追加しません。
- module一覧・folder・tag・Git URLはpackage内の固定catalogです。
- bundle導入は`Client.AddAndRemove`へ追加URLだけを一括で渡し、削除要求は渡しません。
- unknown package、Assets copy競合、既存処理中はPackage Managerを呼びません。
- 進行中の選択は`SessionState`に保持し、Unity再起動後まで永続化しません。
- package追加の成否はPackage Managerが返す結果に従います。repositoryへのnetwork接続とGitが必要です。

## 対応環境

- Unity 6000.5.7f1以降
- Editor専用
- 追加のregistry package依存なし
