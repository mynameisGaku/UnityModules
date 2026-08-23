# ビルド実行アシスタント（Build Assistant）

## 30 秒で分かる説明

Unity のデスクトップ向け Standalone ビルドを、設定確認から実行結果の保存まで上から順に進める Editor 専用モジュールです。

`Preview Build` で現在の設定と入力内容を記録し、内容を確認した同じ計画だけを `Build Confirmed Plan` で実行します。確認後に設定やアセットが変わった場合はビルドを開始せず、もう一度確認するよう案内します。

## 何ができるか

- Unity の現在有効な `Build Profile`、シーン、`Build Options`、出力先を一画面で確認できます。
- ビルドごとに新しい実行フォルダーを作り、既存ファイルや過去の実行結果を上書きしません。
- ビルド結果、所要時間、警告・エラー数、容量の内訳、前回との容量差を最大 20 件保存します。
- 選択した結果を新しい JSON ファイルとして明示的に書き出せます。既存の JSON は上書きしません。
- Unity のビルド設定や `Build Profile` を自動で切り替えません。

## 対応範囲

- Unity `6000.5.7f1`
- Editor 専用
- デスクトップ向け Standalone のクライアントビルド
- `Windows 64-bit`、`macOS`、`Linux 64-bit`
- `BuildPipeline.IsBuildTargetSupported` が有効な、インストール済みのプラットフォームモジュール

Dedicated Server、モバイル、Web、コンソールは 1.0.0 の対象外です。`Server` サブターゲットは確認画面を作る前に拒否します。

`ProfilerUserSettings.customConnectionID` を使う `BuildOptions.CustomConnectionID` は 1.0.0 の対象外です。値が設定されている場合は、実際のビルド内容と確認表示がずれないよう `Preview Build` の時点で拒否します。

## 使い方

Unity メニューの `Tools > Build Assistant > Open` を開き、①から⑤まで上から順に進めます。

![Build Assistant の操作順](Documentation~/build-assistant-guide.png)

画像は Unity `6000.5.7f1` の実画面です。①と②を設定してから③で入力を固定し、④で内容を確認したあと、⑤でビルドと結果保存を行います。

<details>
<summary>注釈のない実画面も確認する</summary>

Preview 後の確認画面です。

![Preview 後の Build Assistant](Documentation~/build-assistant-ready.png)

IL2CPP Release ビルド完了後の結果・履歴・JSON 書き出し画面です。

![ビルド完了後の Build Assistant](Documentation~/build-assistant-result.png)

</details>

### ① `Profile`

現在有効な `Build Profile` と Unity Editor が現在選んでいる `Editor Active Target` を確認します。変更する場合は `Open Build Profiles` から Unity の画面を開き、変更後に Build Assistant へ戻ります。カスタム `Build Profile` が実際に使う対象は③で取得し、④の `Target` に表示します。

カスタム `Build Profile` を使用する場合は、Project 内に保存済みで、有効な GUID と依存関係ハッシュを持つ必要があります。実際のビルド対象とサブターゲットは、そのカスタム `Build Profile` の値を基準にします。

### ② `Output`

`Output Root` に出力先の基準フォルダーを入力するか、`Browse` で選択します。

指定できるのは次のどちらかです。

- ローカルドライブ上にすでに存在するフォルダー
- ローカルドライブ上にすでに存在するフォルダーの、未作成の直下フォルダー 1 つ

OS がローカルの固定ファイルシステムとして報告する場所だけを利用できます。UNC パス、ネットワークドライブ、割り当てドライブ、判定できないファイルシステムは利用できません。`Assets`、`Packages`、`ProjectSettings`、`Library`、`Temp`、`Logs`、`obj` の内部や、シンボリックリンク、ジャンクションなどの再解析ポイントを通る場所も利用できません。

### ③ `Preview`

`Preview Build` を押します。この操作はフォルダーやファイルを作らず、Unity の設定も変更しません。

確認用の計画には、現在の次の情報を記録します。

- `Build Profile` の識別情報、対象、サブターゲット、`Scripting Backend`、有効な定義、`Build Options`
- 有効・無効を含むビルドシーンの順序、GUID、依存関係ハッシュ
- `ProjectSettings`、`Library/BuildProfiles`、`Library/EditorUserBuildSettings.asset` の内容
- `AssetDatabase` の読み込み済みアセット全体の変更世代
- `Packages/manifest.json` と `Packages/packages-lock.json`
- `Assets/StreamingAssets` 内のファイル内容
- 出力先の正規化後のパスと、新しく作る実行フォルダー・プレイヤーのパス

安全側に判定するため、ビルドと直接関係しない読み込み済みアセットや別のプラットフォーム設定を変更した場合でも、確認済み計画が古いと判定されることがあります。その場合はもう一度 `Preview Build` を押してください。

### ④ `Confirm`

`Profile`、`Target`、`Scripting Backend`、`Build Options`、シーン、出力先を確認します。問題がなければ `I reviewed the profile, scenes, options, and output paths.` をオンにします。

カスタム `Build Profile` が持つ圧縮方式や開発用設定は確認画面と履歴に含まれます。Unity が `BuildPlayerWithProfileOptions` でプロファイルから適用する値なので、追加の上書き値として重ねて渡しません。

### ⑤ `Build / Result / Export`

`Build Confirmed Plan` を押すと、実行直前に③の記録と現在の状態を再比較します。変更があれば `StalePlan` として停止し、Unity のビルド処理は呼び出しません。

問題がなければ `BA-yyyyMMdd-HHmmss-8hex` 形式の新しい実行フォルダーを排他的に作り、その中へビルドします。既存の同名フォルダーがある場合や、別の処理が先に作った場合は上書きせず停止します。Windows ではビルド中に実行フォルダーが移動・置換されないよう、削除を許可しないハンドルも保持します。Unix 系では OS が報告するマウント種別を使ってローカルの固定ファイルシステムだけを許可し、通常の同名作成競合を防ぎます。ただし、ビルド中に外部処理がファイルシステムを再マウントしたり、権限を使って意図的に置換したりする状況までは保証しません。

`History and JSON Export` では保存済みの結果を選び、`Export Selected Result as New JSON` から新しい JSON を作成できます。書き出し先にも②と同じローカル固定ファイルシステム・再解析ポイント・Unity 管理フォルダーの安全条件を適用します。

## 履歴と比較

履歴は Project の `Library/BuildAssistant` に保存され、Git 管理対象にはしません。保存件数は新しい順に 20 件です。ビルド中に Unity が終了した場合は、残った実行状態を次回読み込み時に `Interrupted` として記録します。自動的な再ビルドは行いません。

容量差の比較対象は、プロファイルの識別情報、対象、サブターゲット、`Scripting Backend`、有効な `Build Options` が一致する直近の成功結果です。シーン内容の変更だけでは比較対象から外しません。

ビルド成功と履歴保存成功は別々に報告します。ビルドが成功しても履歴を書き込めなかった場合は、成功したビルド結果を保ったまま保存エラーを表示します。

## スクリプトから使う場合

公開入口は `BuildAssistant.Editor.BuildAssistantService` です。

- `Preview(string absoluteOutputRoot)`
- `Build(BuildAssistantPlan plan)`
- `LoadHistory()`
- `ExportJson(BuildAssistantHistoryEntry entry, string absolutePath)`

`BuildAssistantPlan` と履歴・結果の公開データは作成後に内容が変わらない値として渡され、コレクションも呼び出し側から書き換えられません。`Preview` で得た計画は一度のビルドだけに使用してください。

より詳しい失敗条件と保存形式は [Documentation](Documentation~/index.md) を参照してください。
