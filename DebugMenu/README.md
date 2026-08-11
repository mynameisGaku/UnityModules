# Debug Menu

Unity の実行中に、値の変更、アクションの実行、状態監視、折れ線グラフ、HSV 色編集を行うランタイムデバッグメニューです。UI Toolkit で描画し、キーボードとマウスの両方から操作できます。

対応: **Unity 6000.0 以降**  
依存: **Containers 1.0.0**（`com.studiogaku.containers`）

## インストール

### フォルダをコピーする

依存順を守り、次の 2 フォルダをプロジェクトへ配置します。

1. `Containers` を `Assets/Modules/Containers` へコピーする。
2. `DebugMenu` を `Assets/Modules/DebugMenu` へコピーする。
3. デバッグ項目を登録する利用側 asmdef から `DebugMenu.Runtime` を参照する。

`DebugMenu.Runtime` は `Containers.Runtime` を参照します。`DebugMenu` だけをコピーするとコンパイルできません。

### Git URL から UPM で追加する

Package Manager の **Add package from git URL**、または `Packages/manifest.json` を使い、Containers を先に追加します。

```json
{
  "dependencies": {
    "com.studiogaku.containers": "https://github.com/mynameisGaku/UnityModules.git?path=/Containers#main",
    "com.studiogaku.debug-menu": "https://github.com/mynameisGaku/UnityModules.git?path=/DebugMenu#main"
  }
}
```

同じ名前の依存パッケージを別のレジストリから導入済みなら、`com.studiogaku.containers` 1.0.0 を満たす構成でも使えます。

## シーンへ配置する

1. Unity の **Tools > Debug Menu > Add To Scene** を実行する。
2. 作成された `Debug Menu` GameObject と `PanelSettings` を確認する。
3. Play Mode に入り、`F1` で開閉する。

メニューを出している間は既定で `Time.timeScale` が `0` になります。止めたくない場合は `DebugMenuController` の **Pause While Visible** を無効にします。

## 標準の見た目

標準テーマは画面全体を半透明の濃紺背景で覆い、内容を画面左から 24 px、上から 16 px の位置に 20 px の文字で表示します。選択行は横幅いっぱいの青、ポインターを重ねた行は薄い青で示します。

`Describe` で設定した選択行の説明は、一覧を狭める固定フッターではなく画面右下の枠付きパネルへ浮かせて表示します。行のクリックとダブルクリック、値の `−` / `+`、スライダーと HSV・アルファ帯のドラッグ、ページ移動と戻るボタンは、この全画面表示でもマウスから操作できます。

入力欄は通常時に「行高の 6.5 倍」（標準 130 px）を上限として保ち、右端には余白を残します。Game View が狭い場合だけ値列を左へ寄せ、入力欄とスライダーを必要量まで縮めます。展開したグラフとカラーピッカーも行内へ収めます。値そのものは短縮せず、表示だけを省略します。

## 項目を登録する

利用側 asmdef に `DebugMenu.Runtime` の参照を追加し、`static void Method(DebugMenuRoot)` に `[DebugMenuRegister]` を付けます。登録メソッドは起動時に 1 回、自動で呼ばれます。

```csharp
using DebugMenu;
using UnityEngine;
using UnityEngine.Scripting;

public static class PlayerDebugMenu
{
    private static bool _invincible;
    private static float _moveSpeed = 6f;

    [Preserve]
    [DebugMenuRegister(Order = 10)]
    private static void Register(DebugMenuRoot menu)
    {
        var page = menu.AddPage("Player");

        page.Bool("Invincible", () => _invincible, value => _invincible = value)
            .WithShortcut(KeyCode.F2)
            .WithSaveKey("player.invincible");

        page.Float("Move Speed", () => _moveSpeed, value => _moveSpeed = value)
            .WithRange(0f, 20f)
            .WithStep(0.5f)
            .WithUnit("m/s")
            .WithSaveKey("player.move-speed");
    }
}
```

IL2CPP ではリフレクションだけから参照される登録メソッドが除去されないよう、各登録メソッドへ `UnityEngine.Scripting.PreserveAttribute` を付けてください。

## 操作

| 操作 | キーボード | マウス |
|---|---|---|
| 開閉 | `F1` | — |
| 行を選ぶ | `↑` / `↓` | 行をクリック |
| Bool を切り替える | `Enter` / `←` / `→` | チェック欄を左クリック |
| 10 行移動 | `PageUp` / `PageDown` | ホイールで一覧をスクロール |
| 値を変更 | `←` / `→` | `−` / `+`、またはスライダーをドラッグ |
| 決定・展開・子ページへ移動 | `Enter` | 行をダブルクリック |
| 色を編集 | `Enter` | 色見本を左クリックして HSV 展開、値欄をダブルクリックして 16 進入力 |
| 戻る・最上位で閉じる | `Esc` | 右クリック、または子ページの戻るボタン |
| 前後の最上位ページ | `[` / `]` | ヘッダーの左右ボタン |
| お気に入り切り替え | `F` | 行の星をクリック |
| 既定値へ戻す | `R` | — |
| 直接文字入力 | `Enter` | 値欄をダブルクリック |
| 全体検索 | `Ctrl+F` | Searchページを開く |
| Undo / Redo | `Ctrl+Z` / `Ctrl+Y` | — |

範囲を設定した `Int` / `Float` はスライダーをクリックまたはドラッグできます。`Color` は色見本の左クリックで HSV 編集を即座に展開し、HSV 面、色相帯、必要ならアルファ帯をドラッグします。マウスホイールは UI Toolkit の一覧（`ListView`）をスクロールします。メニュー上の右クリックは、子ページなら親へ戻り、最上位ページならメニューを閉じます。

直接入力は `Int`、`Float`、`Text` に対する `Enter`、または `Int`、`Float`、`Text`、`Color`、`Vector` の値欄ダブルクリックで始めます。入力文字、選択背景、カーソルは別々のテーマ色で描くため、全選択中も文字を確認できます。入力中は `Enter` で確定、`Esc` で破棄します。色は `#RRGGBB` / `#RRGGBBAA`、ベクトルは成分数と同じ個数のカンマ区切りです。

## 対応する行

| API | 用途 |
|---|---|
| `Group` / `Separator` | 折り畳める見出し / 区切り |
| `Action` | 決定時に処理を実行 |
| `Bool` | 真偽値の切り替え |
| `Int` / `Float` | 数値変更、範囲・刻み幅・直接入力 |
| `Enum` / `Choice` | 左右送り、または決定で展開する候補一覧から選択 |
| `Text` | 文字列の直接入力 |
| `Color` | 16 進入力、HSV・アルファ編集 |
| `Vector` / `DebugVector` | Vector3 の簡易追加、または 2〜4 成分の編集と一括入力 |
| `Watch` | 文字列または数値の読み取り専用表示 |
| `Graph` | 直近の数値標本を折れ線表示 |
| `AddChildPage` | 別画面またはインラインの子ページ |

`Describe` で選択行の説明、`WithUnit` で単位、`WarnOutside` で注意範囲、`WithShortcut` で行ショートカット、`WithSaveKey` で安定した保存キーを設定できます。

## 未実装の機能

現在未実装、またはサービスだけで通常画面へ未接続なのは次です。

- プロファイル切り替え、最近触った項目
- Path 行、配列行、トースト、クリップボード用スナップショット
- 標準ゲームパッド割り当て、実行中の Appearance ページ
- Text / JSON / Binary の 3 形式保存と Save As / Load From

Color Picker は行内展開です。保存は現在 JSON 形式です。

## 最上位ページと子ページ

`menu.AddPage` を複数回呼ぶと最上位ページが増えます。`[` / `]` またはヘッダーの左右ボタンで循環します。子ページではヘッダーの戻るボタンも表示されます。子ページを開いている状態で最上位ページを切り替えると、子ページの履歴は閉じられます。

```csharp
var page = menu.AddPage("Player");
var details = new DebugPage("Player Details");
details.Watch("State", () => "Ready");
page.AddChildPage(details, DebugAttachMode.Page);
```

`DebugAttachMode.Page` は決定時に別画面へ移動し、`DebugAttachMode.Inline` は同じ一覧内へ展開します。

## Pause と値の永続化

`DebugMenuController` の既定値は次のとおりです。

- **Pause While Visible: ON** — 表示中だけ `Time.timeScale = 0` にし、閉じたとき・無効化時・破棄時に元の値へ戻す。
- **Persist Values: ON** — 起動時に読み込み、Controller の破棄時に保存する。
- 保存先 — `Application.persistentDataPath/DebugMenu/debug-menu-settings.json`。

表示名やページ位置を変える可能性がある行には `WithSaveKey` を指定してください。未指定時はページと親子の経路からキーを作るため、移動・改名後は以前の値を復元できません。保存内容はデバッグ用の平文 JSON であり、秘密情報の保管には使わないでください。

`DebugMenuSettings` を直接使えば `DebugMenuPlayerPrefsStorage` や独自の `IDebugMenuStorage` に差し替えられます。

## 見た目を変える

Controller の **Theme > 基本サイズ** には、文字を変える **Font Size** と、ボタン・チェック・入力欄・スライダー・グラフ・Pickerをまとめて拡縮する **Gui Scale** があります。文字が切れないよう、Font Size が拡縮後の行高を超える場合は行高も自動で広がります。個別に詰めたい場合は、その下の行高、値列、入力欄、スライダー、各操作部品の比率を変更します。Play 中に Inspector で変更した値は、文字入力の終了後に表示へ反映されます。

コードからは次のように変更できます。`ApplyTheme()` は表示だけを再構築するため、現在ページ、カーソル、値は維持されます。

```csharp
controller.Theme.SetSizes(fontSize: 24, guiScale: 1.25f);
controller.ApplyTheme();
```

入力欄の文字、選択背景、カーソルは `InputFieldText`、`InputFieldSelection`、`InputFieldCursor` から個別に変更できます。既定値は白文字、青い選択背景、白いカーソルです。

見た目は `DebugMenuTheme` の値からコードで組み立てるため、専用 USS の配置は不要です。`PanelSettings` のランタイムテーマは必要で、エディタメニューが作成・割り当てを行います。

## Input System と独自入力

Input System は任意です。`Unity.InputSystem` が利用可能なら実行時に検出してキーボードを読み、利用できなければ旧 `UnityEngine.Input` へフォールバックします。このパッケージから Input System へのコンパイル時依存はありません。

ゲームパッドや独自の入力レイヤーを使う場合は、`DebugMenuController.InputProvider` に `DebugMenuInputState` を返す関数を設定します。最上位ページ切り替えには `PreviousPage` / `NextPage` を使います。`InputProvider` はメニュー内操作だけを置き換え、既定の `F1` 開閉は別に読まれます。別デバイスで開閉する場合は、その入力側から `controller.Menu.Toggle()` を呼びます。

## リリースビルドでの扱い

このモジュールは Release ビルドで自動的に無効にはなりません。製品版に含めない場合は、製品シーンから `DebugMenuController` を外すか、Controller の生成と登録コードを `UNITY_EDITOR || DEVELOPMENT_BUILD` で囲んでください。機密情報を表示する行や破壊的な `Action`、隠れた状態でも効くショートカットを製品版へ残さないでください。

詳しい API と運用上の注意は [Documentation](Documentation~/index.md)、動作例は Package Manager の **Samples > Debug Menu Basics** を参照してください。
