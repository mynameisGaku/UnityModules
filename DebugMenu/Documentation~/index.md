# Debug Menu 1.0.0

## 構成

Debug Menu は次の 3 層で構成されます。

- `DebugMenuRoot` / `DebugPage` / `DebugElement`: 描画に依存しないメニュー状態。
- `DebugMenuController` / `DebugMenuView`: MonoBehaviour の入口と UI Toolkit 表示。
- `DebugMenuSettings` / `DebugMenuFavorites` / `DebugMenuHistory` / `DebugMenuSearch`: 値保存や補助機能。

ランタイム asmdef は `DebugMenu.Runtime` です。内部で `Containers.Runtime` の `FastList<T>`、`RingBuffer<T>`、`UndoRedoStack<T>` などを使うため、Containers 1.0.0 が必要です。Input System は任意で、コンパイル時の依存はありません。

公開対象および検証済み環境は Unity 6000.5.7f1 の Windows Editor です。`package.json` も `unity: 6000.5`、`unityRelease: 7f1` を最小条件として宣言します。他の Unity バージョン、OS、Player プラットフォームは未検証です。

## 導入

フォルダ配置では `Containers`、`DebugMenu` の順に `Assets/Modules` へコピーし、利用側 asmdef から `DebugMenu.Runtime` を参照します。

Git UPM では次の順で追加します。

1. `https://github.com/mynameisGaku/UnityModules.git?path=/Containers#dev`
2. `https://github.com/mynameisGaku/UnityModules.git?path=/DebugMenu#dev`

`1.0.0` の公開前は `dev` を開発確認に使います。公開後は配布ページに記載された固定リリースタグ、またはコミットIDへ両方の参照を揃えてください。製品プロジェクトの固定依存には更新可能なブランチを使いません。

Unity で **Tools > Debug Menu > Add To Scene** を実行すると、`Assets/Settings` に PanelSettings とランタイムテーマを用意し、シーンへ `DebugMenuController` を配置します。Play Mode の `F1` で開閉します。

## 登録ライフサイクル

`DebugMenuController.Awake` は `DebugMenu.Runtime` を直接参照するアセンブリから `[DebugMenuRegister]` の付いたメソッドを集め、`Order` の昇順で一度だけ呼びます。形は `static void Method(DebugMenuRoot)` に限られます。1 メソッドの例外はログへ出し、残りの登録を続けます。

```csharp
[Preserve]
[DebugMenuRegister(Order = 20)]
private static void Register(DebugMenuRoot menu)
{
    var page = menu.AddPage("Network");
    page.Watch("State", () => NetworkStateName);
}
```

IL2CPP では登録メソッドが静的参照されないため、各メソッドへ `[Preserve]` を付けます。登録をまとめた型やアセンブリを `link.xml` で保存する運用でも構いませんが、サンプルは影響範囲の狭いメソッド単位を採用しています。

## ページ

- `DebugMenuRoot.AddPage`: 最上位ページを追加する。最初のページが初期表示になる。
- `DebugMenuRoot.MoveRootPage`: 最上位ページを循環する。子ページ履歴は閉じる。
- `DebugPage.AddChildPage(..., DebugAttachMode.Page)`: 決定で別画面へ移動する行を置く。
- `DebugPage.AddChildPage(..., DebugAttachMode.Inline)`: 子ページの内容を同じ一覧へ展開する。
- `DebugMenuRoot.PushPage` / `PopPage` / `PopToRoot`: ページ履歴を明示的に操作する。

既定操作は `[` / `]`、またはヘッダーの左右ボタンで前後の最上位ページへ移動します。`Esc` は子ページから 1 段戻り、最上位ではメニューを閉じます。

## 行 API

全ての生成拡張は追加した行を返します。ページ直下と `Group` 内の `DebugElement` のどちらでも同じ名前を使えます。

| API / 型 | 値と操作 |
|---|---|
| `Group` | 子行を束ね、決定またはダブルクリックで折り畳む。 |
| `Separator` | 選択しても処理を行わない区切り。 |
| `Action` | 決定時に `Action` を呼ぶ。保存対象外。 |
| `Bool` | 決定または左右で真偽値を反転する。 |
| `Int` | `WithRange`、`WithStep`、整数の直接入力。 |
| `Float` | `WithRange`、`WithStep`、`WithDigits`、Invariant Culture の直接入力。 |
| `Enum<TEnum>` | enum 宣言順の候補を左右送り、または展開一覧から選ぶ。 |
| `Choice` | 文字列配列の候補を左右で選ぶ。 |
| `Text` | 文字列を直接入力する。 |
| `Path` / `FilePath` / `FolderPath` | string getter / setterへ接続し、インラインブラウザーまたは値欄のダブルクリックでパスを編集する。存在確認と拡張子制限は任意。 |
| `IntArray` / `FloatArray` | `IList<int>` / `IList<float>` を index ごとの既存数値行として展開する。 |
| `Color` | 16 進入力、HSV 面、色相帯、任意のアルファ帯。`ShowAlpha = false` は常に不透明にする。 |
| `Vector` / `DebugVector` | `Vector` 拡張は Vector3、`DebugVector.Of` は Vector2 / Vector3 を追加する。公開コンストラクタでは 2〜4 成分を扱える。 |
| `Watch` | 関数の戻り値を毎フレーム表示する。編集・保存はしない。 |
| `Graph` | 表示中に数値標本を採取し、直近 N 件を折れ線表示する。編集・保存はしない。 |

共通設定は `Describe`、`WithUnit`、`WarnOutside`、`WithShortcut`、`WithSaveKey` です。`Graph` では `SampleInterval`、`AutoScale`、`Min`、`Max`、`Digits`、`HeightRatio` も設定できます。

```csharp
page.FolderPath("Output", () => OutputPath, value => OutputPath = value)
    .WithExistingPathRequired();
page.IntArray("Enemy IDs", EnemyIds).WithRange(0, 9999);
page.FloatArray("Blend", BlendWeights).WithRange(0f, 1f).WithStep(0.05f);
```

`Path` の決定操作はインラインブラウザーを展開します。Fileモードはサブフォルダーと拡張子に合うファイル、Folderモードはサブフォルダーと `Use This Folder` を表示します。`[..] Parent` で親へ移動でき、列挙例外はエラー行として表示します。

配列の親行は保存対象外で、`[index]` の子行だけが保存されます。外側で `IList<T>` の長さを変えた場合も子行を安全に増減し、入れ子に配置した場合も表示行数を自動更新します。

## 入力

### キーボード

| キー | 動作 |
|---|---|
| `F1` | メニューの開閉 |
| `↑` / `↓` | 行移動 |
| `←` / `→` | 現在値を 1 刻み変更 |
| `PageUp` / `PageDown` | 10 行移動 |
| `Enter` | 決定、展開、または Int / Float / Text の入力開始 |
| `Esc` | 入力破棄、子ページから戻る、最上位で閉じる |
| `[` / `]` | 前後の最上位ページ |
| `F` | お気に入りの切り替え |
| `R` | 現在行を既定値へ戻す |
| `Ctrl+F` | 全体検索を開いて検索語の入力を始める |
| `Ctrl+Z` / `Ctrl+Y` | 値変更の Undo / Redo |

方向キーは押しっぱなしで反復し、一定時間後に加速します。`InputProvider` を差し替える場合も `DebugMenuInputRepeater` が同じ反復を適用します。

### マウス

- 行のクリックで選択、同じ行のダブルクリックで決定する。
- Bool のチェック欄は左クリック 1 回で即座に切り替える。
- Color の色見本は左クリック 1 回で HSV 編集を展開し、値欄のダブルクリックで 16 進入力を始める。
- Int / Float / Text は行または値欄、Vector は値欄のダブルクリックで入力する。
- `−` / `+` で 1 刻み変更する。
- 範囲付き Int / Float のスライダーはクリックとドラッグに対応する。
- Color は HSV 面、色相帯、アルファ帯をドラッグする。
- マウスホイールで UI Toolkit の一覧（`ListView`）をスクロールする。
- 行の星をクリックしてお気に入りを切り替える。
- ヘッダーの左右ボタンで最上位ページを循環し、子ページでは戻るボタンで親へ戻る。
- メニュー上の右クリックは、子ページでは親へ戻り、最上位ページではメニューを閉じる。

入力欄は `Enter` またはフォーカス移動で確定し、`Esc` で破棄します。入力文字、選択背景、カーソルは別々のテーマ色で描きます。解釈できない入力は値へ反映せず、文字を警告色へ変えます。

### ゲームパッド

- 方向パッドまたは左スティックで行移動と値変更を行う。
- Southで決定、Eastで取消を行う。
- 左右ショルダーで前後の最上位ページへ移動する。
- Startでメニューを開閉する。

### Input System

Input System パッケージは任意です。`DebugMenuKeyboard` と既定ゲームパッド入力は `Unity.InputSystem.Keyboard` / `Gamepad.current` をリフレクションで検出し、利用できれば機種差を吸収した標準配置で読みます。利用できない場合は旧 `UnityEngine.Input` のキーボード、`Horizontal` / `Vertical`、一般的な十字キー軸名、Xbox互換の JoystickButtonを安全に試します。コンパイル時の Input System 参照はありません。既定のキーボード状態とゲームパッド状態は論理和で合成します。

ゲーム固有の入力を使う場合は、ビュー生成前に `DebugMenuController.InputProvider` へ `Func<DebugMenuInputState>` を渡します。`PreviousPage` と `NextPage` も状態へ含められます。差し替えた `InputProvider` は表示中のメニュー操作用で、`F1` のトグル読み取りは置き換えません。独自デバイスから開く場合は `DebugMenuRoot.Toggle()` を呼びます。

## ショートカット

`WithShortcut(KeyCode)` を付けた行は、表示中のページに関係なく登録済みの全ページと接続された子ページから探され、最初に一致した行の決定処理が呼ばれます。Page モードの子ページリンクへ付けた場合は、そのリンク先を開きます。循環参照のあるページ構成も一度ずつ走査します。同じキーを複数行へ割り当てないでください。ショートカットはメニューが閉じていても動作するため、製品ビルドへ残す項目を明示的に選別してください。

## 保存

Controller の **Persist Values** が有効なら、起動時に読み込み、破棄時に保存します。既定の `DebugMenuFileStorage` は次のフォルダーを使います。

```text
Application.persistentDataPath/DebugMenu/
```

保存対象は Bool、Int、Float、Enum / Choice、Text、Path、Color、Vector、数値配列の各 index など値を持つ行です。Action、Group、数値配列の親、Watch、Graph は対象外です。キーを省略するとページと親子の表示名から生成されます。改名・移動後も同じ値を引き継ぐ行には `WithSaveKey` で安定キーを指定します。

`DebugMenuSettings(IDebugMenuStorage, string, DebugMenuSettingsFormat)` を直接作ると、ファイル、`DebugMenuPlayerPrefsStorage`、独自ストレージを選べます。形式はJSON、Text、Binaryから選び、読み込み時は中身から自動判別します。`SaveAs` / `LoadFrom` は任意パスへ原子的に書き出し、または読み込みます。保存形式は改ざん防止や暗号化を目的としていません。

`Settings`ページにはプロファイル名、形式、任意ファイルパス、Save / Load / Delete / Save As / Load From / Copy Menu Text / Reset Allがあります。保存済みプロファイルは一覧から適用できます。保存・読込・コピーなどの結果は画面上の短い通知にも表示します。`Recent`ページは直近16件の変更を重複なしで表示し、借用している元の行を直接操作します。

## Pause とライフサイクル

**Pause While Visible** の既定値は ON です。表示を開いた時点の `Time.timeScale` を保存して `0` にし、閉じる、Controller を無効化する、または破棄すると元へ戻します。メニュー内部の更新は `Time.unscaledDeltaTime` を使います。

ゲームを止めずに値を観察したい場合は Inspector で無効にするか、ビュー生成前に `Menu.PauseWhileVisible` を変更します。

## テーマ

既定テーマは半透明の濃紺背景を画面全体へ描き、内容の原点を左 24 px、上 16 px、文字サイズを 20 px とします。選択行は横幅いっぱいの青、マウスホバー行は薄い青で区別します。`Describe` の内容は固定フッターを使わず、選択行に応じて画面右下の枠付きパネルへ浮かせて表示します。

この外観でもマウス入力は有効です。Bool と色見本の左 1 クリック、値欄のダブルクリック、`−` / `+`、数値スライダー、HSV・アルファ編集、ホイールスクロール、お気に入りの星、ページボタン、右クリックによる戻る・閉じる操作を直接行えます。

入力欄は広い画面でも空き幅いっぱいへ伸ばさず、`EditFieldWidthRatio` の幅を上限にして右側へ余白を残します。狭い Game View では値列を左へ寄せ、入力欄を `EditFieldMinimumWidthRatio` まで優先して残し、足りない分はスライダーから先に縮めます。展開したグラフとカラーピッカーも利用可能幅へ収めます。モデルの文字列や保存値は切り詰めず、表示だけを省略します。

`DebugMenuTheme` は Inspector で Controller の **Theme** に展開されます。**Font Size** は文字、**Gui Scale** はボタン・チェック・入力欄・スライダー・グラフ・Pickerを含むGUI寸法を一括変更します。文字が切れないよう、Font Size が拡縮後の行高を超える場合は行高も自動で広がります。操作部品ごとの比率も同じ場所で上書きできます。

トップレベルの `Appearance` ページでは、`Font Size`（8〜48）、`GUI Scale`、`Row Height`、`Panel Margin`、`Top Margin` を実行中に変更できます。`Compact` / `Standard` / `Large` と、Controller生成時の値へ戻す `Reset` があり、各寸法は通常の設定保存で復元されます。

Play 中の Inspector 変更は `DebugMenuController` が表示へ反映します。直接入力中とスライダー・色選択面のドラッグ中は現在の操作を残し、終了後に再適用します。コードから変更した場合は `controller.Theme.SetSizes(24, 1.25f); controller.RequestApplyTheme();` のように遅延反映を要求します。`ApplyTheme()` も互換用に利用できます。表示だけを再構築するため、現在ページ、カーソル、値は維持されます。

入力欄は `InputFieldText`、`InputFieldSelection`、`InputFieldCursor` で文字、選択背景、カーソルを個別に設定します。

## 製品版への境界

Debug Menu は Release ビルドを自動判定して無効化しません。製品版から除外する場合は、次のいずれかをプロジェクト側で行います。

- 製品シーンへ `DebugMenuController` を入れない。
- Controller を生成するコードと `[DebugMenuRegister]` メソッドを `#if UNITY_EDITOR || DEVELOPMENT_BUILD` で囲む。
- デバッグ項目を専用 asmdef に分け、製品向けビルドから除外する。

表示するログ、個人情報、認証情報、破壊的な Action、隠れた状態で効くショートカットも同じ境界で管理してください。

## 対応範囲と制限

| 項目 | 状態 |
|---|---|
| Unity | 6000.5.7f1 を公開対象・検証済み環境として指定 |
| Editor OS | Windows Editor で検証済み |
| Player | 対象プラットフォームごとのビルド・実機確認が必要 |
| Render Pipeline | UI Toolkit のオーバーレイのみを描画し、レンダーパイプライン固有機能は使用しない |
| Input System | 任意。利用可能なら実行時検出し、未導入時は旧 Input へフォールバック |
| 物理ゲームパッド | 論理割り当てを自動テスト済み。機種ごとの実機確認は未実施 |
| IL2CPP | 登録メソッドへ `[Preserve]` または `link.xml` が必要 |
| 保存 | デバッグ用途。暗号化・改ざん検出・機密情報保護は行わない |

タッチ専用操作、コンソール固有入力、VR 入力、ネットワーク経由のリモート操作は提供しません。旧 Input のゲームパッド判定は一般的な軸名と Xbox 互換ボタンを試すため、プロジェクト固有の入力設定では `DebugMenuController.InputProvider` を使用してください。

## 更新

1. 更新前に Debug Menu と Containers の版を記録する。
2. 保存プロファイルを維持する場合は `Application.persistentDataPath/DebugMenu/` をバックアップする。
3. UPM は配布ページの固定リリースタグまたはコミットIDへ両方の参照を更新する。フォルダ配置では Containers、Debug Menu の順にフォルダ全体を置き換える。
4. 表示名やページ構成を変えた行は `WithSaveKey` を維持する。
5. Play Mode、保存プロファイルの往復、対象 Player ビルドを確認する。

## 削除

1. シーンと生成コードから `DebugMenuController` および `[DebugMenuRegister]` の参照を外す。
2. 自動生成された `Assets/Settings/DebugMenuPanelSettings.asset` と `Assets/Settings/DebugMenuTheme.tss` が不要なら削除する。
3. UPM から Debug Menu を削除するか、フォルダ配置なら `Assets/Modules/DebugMenu` を削除する。
4. 他の機能が参照していない場合に限り Containers を削除する。
5. 保存済みプロファイルが不要なら `Application.persistentDataPath/DebugMenu/` を削除する。

## サポート

不具合報告には次の情報を含めてください。

- Debug Menu と Containers の版。
- Unity の完全なバージョン、OS、対象 Player プラットフォーム。
- Input System の導入有無と、使用した入力デバイス。
- 再現手順、Console のエラー全文、最小構成の登録コード。
- 表示問題の場合は Game View の解像度、UI Scale、Theme の変更値、スクリーンショット。

連絡先は `gaku.fujimoto.business@gmail.com` です。

## 配布と利用条件

Unity Asset Store から取得した Debug Menu は Unity Asset Store 標準 EULA に従います。Git URL / UPM / リポジトリは技術的な取得経路であり、それ自体が利用、再配布、再許諾その他の権利を付与するものではありません。詳細はパッケージルートの `LICENSE.md`、同梱物と外部依存は `Third-Party Notices.txt` を確認してください。
