# モジュール管理アシスタント（Module Manager）

> Package version: 1.4.9

## 30秒で分かる説明

Unity Package ManagerへGit URLを1件ずつ貼り、導入済みmoduleの公開versionを手作業で調べて差し替える作業を減らすEditor専用ツールです。

`Tools > Module Manager > Open`を開くと、普段使う機能を「Project Maintenance」「Scene and UI」「Game Services」「Input Support」の4つにまとめて表示します。各cardの`Quick guide`で、向いている状況、導入後の最初の操作、変更される範囲を確認してから公開tagをまとめて導入できます。

決定論的simulationと細かなゲーム計算は、通常の4用途と混同しないよう`Specialized collections`へ折りたたんでいます。個別moduleは互換用の詳細一覧に残し、固定公開版のREADMEを`Read guide`から開けます。

## できること

- 普段使う4つのworkflowから、必要なmodule群をまとめて導入する。
- `Project Maintenance`からTexture import設定の差分確認・一括適用ツールを導入する。
- `Project Maintenance`から、確認済み計画だけを新しい出力folderへ実行する「ビルド実行アシスタント」を導入する。
- `Scene and UI`の6件から、複数Sceneの作業構成を保存・Preview・切り替える「シーン作業セット」を導入する。
- 2つの専門向けcollectionは折りたたみ表示に分離する。
- 各workflowの用途、最初の操作、変更範囲をwindow内で確認する。
- 43個の公開moduleを詳細一覧から1件ずつ導入し、固定tagのREADMEを開く。
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

Unityの`Window > Package Management > Package Manager`を開き、`Add package from git URL...`へ次を入力します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/ModuleInstaller#module-installer-v1.4.9
```

Package Managerの解決後、`Tools > Module Manager > Open`を開き、次の順番で操作します。

① やりたい作業に合うworkflowを選びます。画像では`Scene and UI`を選んでいます。

② card上部の概要を読み、`Quick guide`を開いて`Use when`、`Start here`、`Change scope`を確認します。

③ `Quick guide`の下に並ぶmodule名と追加件数を確認します。

④ card最下部の`Install N`を押し、Package Managerの解決とscript reloadが終わるまで待ちます。

⑤ 1件だけ導入する場合は、さらに下の個別一覧を開きます。

導入済みmoduleの更新は、window上部に表示される対象名とversionを確認してから`Update N`を押します。

![Module Managerの操作順](Documentation~/module-manager-guide.png)

<details>
<summary>実際のModule Manager画面を確認する</summary>

`Scene and UI`の`Quick guide`を開いた実画面です。概要、用途、導入する6件、`Install 6`の順で上から確認できます。

![Module Managerの実画面](Documentation~/module-manager-window.png)

</details>

## 最小コード

Runtime APIはありません。C#を書く必要はなく、Editor windowの操作だけで完結します。

1件だけ導入したい場合は、window下部の`Advanced: read about or install one module`を開きます。先に`Read guide`で固定公開版のREADMEを確認し、必要な場合だけ`Install`を押します。

## まずどれを選ぶか

普段は43件の個別一覧を先に読む必要はありません。やりたい作業に最も近いworkflowを1つ選び、`Quick guide`とcardに表示されたmodule名を確認してください。

| やりたいこと | 最初に見るworkflow | 導入後の最初の操作 |
|---|---|---|
| 新しいProjectの基本フォルダー、Player識別子、build方式、.NET API範囲、managed code削除強度、IL2CPP生成方針、C#生成規則、Build Scenes、Texture import設定、壊れた参照、Asset整理、desktop向けbuildをまとめて扱う | `Project Maintenance` | 設定とAssetを確認した後、`Tools > Build Assistant > Open`でbuild計画をpreviewする |
| 編集作業ごとの複数Scene構成、Scene切り替え、画面fade、safe area、pause、起動順を整える | `Scene and UI` | `Tools > Scene Workspace > Open`でProfileを選び、Scene構成を設定して`Preview Changes`を押す |
| save、音声、不具合reportを用意する | `Game Services` | 最初に使うserviceのsampleをimportし、明示的なownerを1つ作る |
| 入力の補助やGameplay入力の一時停止を追加する | `Input Support` | Input Assist Basicsで入力値を確認してから必要なmapだけ設定する |

`Deterministic Simulation`と`Game Rules and Math`は、決定論的simulationや細かな計算部品が本当に必要な場合にだけ選びます。`Advanced: read about or install one module`は既存projectとの互換や、必要なmoduleが明確な場合の入口です。

## 変更される範囲

- `Packages/manifest.json`へ、選択したmoduleのtag固定Git URLが追加されます。
- `Packages/packages-lock.json`へ、解決したcommit SHAと依存関係が記録されます。
- 導入済みpackageは再追加されません。
- 更新時は、導入済みversionがcatalogの公開versionより古いpackageだけに固定tag URLを再指定します。
- 同じversion、より新しいversion、SemVerとして比較できない独自versionは変更しません。`preview`などのprereleaseはSemVer順で公開versionと比較します。
- `Assets/Modules`に同名folderがある場合はmanifestを変更せず、解消方法をwindowへ表示します。
- Package Managerが失敗した場合は、最初の失敗内容を表示して処理を終了します。無限再試行はしません。
- workflowを導入しただけでは、Project Settings、Scene、Prefab、Asset importerを変更しません。
- 「アセット設定チェック」は、利用者が対象・共通設定・Standalone/Android/iOS設定を選び、`Preview`で差分を確認して`Apply`したTexture importerだけを再importします。
- 「ビルド実行アシスタント」は、確認済み計画を実行した時だけ選択した出力先へ新しい実行folderを作り、`Library/BuildAssistant`へ最大20件の履歴を保存します。JSONは利用者が保存先を選んで明示的に書き出した場合だけ作成します。
- 「シーン作業セット」は、`Create New Profile`を選んだ場合だけ`Assets`以下へ`SceneWorkspaceProfile`を作ります。Profileを編集するか現在の構成をCaptureした場合は選択Profileを変更済みにしますが、自動保存しません。`Preview Changes`はSceneを変更せず、`Review and Confirm`後に`Switch Workspace`した場合だけEditorで開くScene、順番、Loaded、Activeを変更します。Dirty Scene、無題Scene、欠損Scene、重複Sceneなどがあれば変更前に停止し、Sceneの保存や変更破棄は行いません。

## 用途別workflow

| 普段使うworkflow | 含まれる用途 |
|---|---|
| Project Maintenance | 基本フォルダー、asmdef、`.gitignore`、`.gitattributes`、Project Settings、build target別Application Identifier・Scripting Backend・API Compatibility Level・Managed Stripping Level・IL2CPP Code Generation、C# Root Namespace、新規script改行方式、複製時の命名規則、条件付きコンパイル記号、Tag・Layer、Build Scenes、Play Mode開始Scene、Texture共通設定とStandalone/Android/iOS override、Inspector整理、debug描画、Scene・Prefab不備修復、Asset参照・名前整理、確認済みdesktop build、容量差・履歴・JSON書き出し |
| Scene and UI | Editorの複数Scene作業構成、Scene切り替え、画面fade、safe area、ゲーム時間、起動手順 |
| Game Services | save data、音声再生、不具合report |
| Input Support | stick・button補助、Gameplay入力の一時停止 |

次の2つは`Specialized collections`内にあります。要件が明確な場合だけ開いてください。

| 専門向けcollection | 含まれる用途 |
|---|---|
| Deterministic Simulation | 固定step、再現乱数、state照合、replay、canonical data、固定小数点、handle |
| Game Rules and Math | resource、能力値、条件、選択、配分、stack、定期処理、damage |

Project Maintenanceに含まれる「プロジェクト一括設定」はv1.15.0へ固定しています。新規Projectでよく使う基本フォルダー、Runtime・Editor・test用asmdef、Unity向け`.gitignore`と`.gitattributes`をまとめて作成できます。既存fileは上書きせず、復元時もこのツールが作成して内容が変わっていないfileだけを削除します。利用者が編集したfileや、Assetを追加したフォルダーは残します。build target別Application Identifier・Scripting Backend・API Compatibility Level・Managed Stripping Level・IL2CPP Code Generation、Project Settings、C# Root Namespace、新規scriptの改行方式、複製時のGameObject・Asset命名規則、条件付きコンパイル記号、Player Build Scenes、EditorのPlay Mode開始Sceneも同じprofileから適用・復元できます。

同じworkflowに含まれる「アセット設定チェック」はv1.1.0へ固定しています。Textureの共通設定に加え、Standalone・Android・iOSのOverride、最大size、圧縮方針を対象ごとに比較します。設定scopeを選んで`Preview`した時点では変更せず、表示された差分を確認して`Apply`した場合だけ選択済みTexture importerを更新・再importします。Preview後に対象が変わった場合は適用を中止します。

同じworkflowに含まれる「ビルド実行アシスタント」はv1.0.0へ固定しています。Unityのdesktop向けStandalone buildを、次の順で実行します。

`Tools > Build Assistant > Open`を開き、次の5区分を上から順に進めます。

① `Profile`で現在のBuild ProfileとEditor Active Targetを確認します。変更が必要な場合は`Open Build Profiles`からUnityの画面を開きます。

② `Output`でbuildを保存する絶対pathのroot folderを選びます。既存の実行結果を上書きするpathは指定しません。

③ `Preview`の`Preview Build`でScene、Build Options、Scripting Backend、出力先を記録します。この時点ではfolderやfileを作らず、Unityの設定も変更しません。

④ `Confirm`でpreview結果を読み、問題がなければ確認欄をオンにします。

⑤ `Build / Result / Export`の`Build Confirmed Plan`で確認済みの同じ計画だけを実行します。状態が変わっていればbuildを開始せず、previewのやり直しを案内します。完了後に結果、容量差、履歴を確認し、必要な結果だけをJSONへ書き出します。

Scene and UIに含まれる「シーン作業セット」はv1.0.0へ固定しています。複数Sceneの順番、Loaded、Activeを作業用Profileとして保存し、現在との差を確認してからEditorのScene構成を切り替えます。RuntimeのScene遷移は扱いません。

`Tools > Scene Workspace > Open`を開き、次の5区分を上から順に進めます。

① `Workspace Profile`で既存のProfileを選ぶか、`Create New Profile`で`Assets`以下へ新しく作ります。

② `Scene Setup/Capture`でSceneを希望順に並べ、LoadedとActiveを設定します。現在開いている構成を使う場合は`Capture Current Setup Into Profile`で取り込みます。編集またはCaptureはProfileを変更済みにしますが、自動保存しません。

③ `Preview Changes`で現在との差を確認します。この時点ではSceneを開閉せず、順番、Loaded、Activeも変更しません。

④ `Review and Confirm`で閉じる、開く、読み込む、読み込み解除、並べ替える、Activeにする変更を確認し、確認欄をオンにします。Preview後に現在の構成またはProfileが変わった場合は③からやり直します。

⑤ `Switch Workspace/Result`の`Switch Workspace`で、確認済みの同じ計画だけを1回適用します。結果欄では`Apply`と`Rollback`を分けて確認できます。Dirty Scene、無題Scene、欠損Scene、重複Sceneなどがある場合はSceneを変更する前に停止し、未保存変更を自動で保存・破棄しません。

## よくある問題

### `Assets/Modules/... already exists`と表示される

同じassemblyをAssets copyとUPM packageの両方から読み込むと、型やasmdefが重複します。どちらの導入方法を使うか決め、UPMへ移行する場合は既存copyをversion controlで退避・削除してから再実行してください。

### package追加後にwindowが閉じた

Package Managerの解決でdomain reloadが起きる場合があります。`Tools > Module Manager > Open`から再度開いてください。導入・更新queueは同じUnity session内で復元されます。

### 一部だけ導入したい

`Advanced: read about or install one module`から個別に選んでください。`Read guide`はcatalogと同じ公開tagのREADMEを開きます。既存の細分化packageは互換入口として残っていますが、新規導入では4つのworkflowを推奨します。

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
