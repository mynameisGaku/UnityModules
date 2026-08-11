# Debug Menu 1.0.0

## 構成

Debug Menu は次の 3 層で構成されます。

- `DebugMenuRoot` / `DebugPage` / `DebugElement`: 描画に依存しないメニュー状態。
- `DebugMenuController` / `DebugMenuView`: MonoBehaviour の入口と UI Toolkit 表示。
- `DebugMenuSettings` / `DebugMenuFavorites` / `DebugMenuHistory` / `DebugMenuSearch`: 値保存や補助機能。

ランタイム asmdef は `DebugMenu.Runtime` です。内部で `Containers.Runtime` の `FastList<T>`、`RingBuffer<T>`、`UndoRedoStack<T>` などを使うため、Containers 1.0.0 が必要です。

## 導入

フォルダ配置では `Containers`、`DebugMenu` の順に `Assets/Modules` へコピーし、利用側 asmdef から `DebugMenu.Runtime` を参照します。

Git UPM では次の順で追加します。

1. `https://github.com/mynameisGaku/UnityModules.git?path=/Containers#main`
2. `https://github.com/mynameisGaku/UnityModules.git?path=/DebugMenu#main`

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
| `Enum<TEnum>` | enum 宣言順の候補を左右で選ぶ。 |
| `Choice` | 文字列配列の候補を左右で選ぶ。 |
| `Text` | 文字列を直接入力する。 |
| `Color` | 16 進入力、HSV 面、色相帯、任意のアルファ帯。`ShowAlpha = false` は常に不透明にする。 |
| `Vector` / `DebugVector` | `Vector` 拡張は Vector3、`DebugVector.Of` は Vector2 / Vector3 を追加する。公開コンストラクタでは 2〜4 成分を扱える。 |
| `Watch` | 関数の戻り値を毎フレーム表示する。編集・保存はしない。 |
| `Graph` | 表示中に数値標本を採取し、直近 N 件を折れ線表示する。編集・保存はしない。 |

共通設定は `Describe`、`WithUnit`、`WarnOutside`、`WithShortcut`、`WithSaveKey` です。`Graph` では `SampleInterval`、`AutoScale`、`Min`、`Max`、`Digits`、`HeightRatio` も設定できます。

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

### Input System

Input System パッケージは任意です。`DebugMenuKeyboard` は `Unity.InputSystem.Keyboard` をリフレクションで検出し、利用できなければ旧 `UnityEngine.Input` を安全に試します。コンパイル時の Input System 参照はありません。

ゲームパッドやゲーム固有の入力を使う場合は、ビュー生成前に `DebugMenuController.InputProvider` へ `Func<DebugMenuInputState>` を渡します。`PreviousPage` と `NextPage` も状態へ含められます。`InputProvider` は表示中のメニュー操作用で、`F1` のトグル読み取りは置き換えません。独自デバイスから開閉する場合は `DebugMenuRoot.Toggle()` を呼びます。

## ショートカット

`WithShortcut(KeyCode)` を付けた行は、表示中のページに関係なく登録済みの全ページと接続された子ページから探され、最初に一致した行の決定処理が呼ばれます。Page モードの子ページリンクへ付けた場合は、そのリンク先を開きます。循環参照のあるページ構成も一度ずつ走査します。同じキーを複数行へ割り当てないでください。ショートカットはメニューが閉じていても動作するため、製品ビルドへ残す項目を明示的に選別してください。

## 保存

Controller の **Persist Values** が有効なら、起動時に読み込み、破棄時に保存します。既定の `DebugMenuFileStorage` は次へ JSON を置きます。

```text
Application.persistentDataPath/DebugMenu/debug-menu-settings.json
```

保存対象は Bool、Int、Float、Enum / Choice、Text、Color、Vector など値を持つ行です。Action、Group、Watch、Graph は対象外です。キーを省略するとページと親子の表示名から生成されます。改名・移動後も同じ値を引き継ぐ行には `WithSaveKey` で安定キーを指定します。

`DebugMenuSettings(IDebugMenuStorage, string)` を直接作ると、ファイル、`DebugMenuPlayerPrefsStorage`、独自ストレージを選べます。保存形式は改ざん防止や暗号化を目的としていません。

## Pause とライフサイクル

**Pause While Visible** の既定値は ON です。表示を開いた時点の `Time.timeScale` を保存して `0` にし、閉じる、Controller を無効化する、または破棄すると元へ戻します。メニュー内部の更新は `Time.unscaledDeltaTime` を使います。

ゲームを止めずに値を観察したい場合は Inspector で無効にするか、ビュー生成前に `Menu.PauseWhileVisible` を変更します。

## テーマ

既定テーマは半透明の濃紺背景を画面全体へ描き、内容の原点を左 24 px、上 16 px、文字サイズを 20 px とします。選択行は横幅いっぱいの青、マウスホバー行は薄い青で区別します。`Describe` の内容は固定フッターを使わず、選択行に応じて画面右下の枠付きパネルへ浮かせて表示します。

この外観でもマウス入力は有効です。Bool と色見本の左 1 クリック、値欄のダブルクリック、`−` / `+`、数値スライダー、HSV・アルファ編集、ホイールスクロール、お気に入りの星、ページボタン、右クリックによる戻る・閉じる操作を直接行えます。

入力欄は広い画面でも空き幅いっぱいへ伸ばさず、`EditFieldWidthRatio` の幅を上限にして右側へ余白を残します。狭い Game View では値列を左へ寄せ、入力欄を `EditFieldMinimumWidthRatio` まで優先して残し、足りない分はスライダーから先に縮めます。展開したグラフとカラーピッカーも利用可能幅へ収めます。モデルの文字列や保存値は切り詰めず、表示だけを省略します。

`DebugMenuTheme` は Inspector で Controller の **Theme** に展開されます。**Font Size** は文字、**Gui Scale** はボタン・チェック・入力欄・スライダー・グラフ・Pickerを含むGUI寸法を一括変更します。文字が切れないよう、Font Size が拡縮後の行高を超える場合は行高も自動で広がります。操作部品ごとの比率も同じ場所で上書きできます。

Play 中の Inspector 変更は `DebugMenuController` が表示へ反映します。直接入力中は未確定文字を残し、入力の確定または取消後に再適用します。コードから変更した場合は `controller.Theme.SetSizes(24, 1.25f); controller.ApplyTheme();` のように `ApplyTheme()` を呼びます。表示だけを再構築するため、現在ページ、カーソル、値は維持されます。

入力欄は `InputFieldText`、`InputFieldSelection`、`InputFieldCursor` で文字、選択背景、カーソルを個別に設定します。

## 未実装の機能

コンボ展開、検索UI、標準Undo/Redo入力、プロファイル、最近触った項目、Path／配列行、トースト、クリップボード用スナップショット、標準ゲームパッド割り当て、実行中Appearanceページ、3形式保存は未実装またはサービスだけで画面へ未接続です。Color Picker は行内展開、保存形式はJSONです。

## 製品版への境界

Debug Menu は Release ビルドを自動判定して無効化しません。製品版から除外する場合は、次のいずれかをプロジェクト側で行います。

- 製品シーンへ `DebugMenuController` を入れない。
- Controller を生成するコードと `[DebugMenuRegister]` メソッドを `#if UNITY_EDITOR || DEVELOPMENT_BUILD` で囲む。
- デバッグ項目を専用 asmdef に分け、製品向けビルドから除外する。

表示するログ、個人情報、認証情報、破壊的な Action、隠れた状態で効くショートカットも同じ境界で管理してください。
