# Changelog

このパッケージの変更履歴。書式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に、バージョン番号は [Semantic Versioning](https://semver.org/lang/ja/) に従う。

## [1.0.0] — 未リリース

### Added

- UI Toolkit ベースのランタイムデバッグメニューと `F1` 開閉。
- 全画面半透明背景、20 px 文字、左 24 px・上 16 px の原点、行のホバー・選択表示、右下へ浮かせる説明パネル。
- キーボード、マウス、押しっぱなし加速、最上位ページ切り替え。Bool と色見本の左 1 クリック、色の値欄をダブルクリックして 16 進入力、右クリックで戻る・閉じる、ホイールスクロール、お気に入りの星クリックに対応。
- 狭い Game View では表示名・値・スライダーを縮めて省略表示し、展開したグラフとカラーピッカーも行幅内へ収めるレスポンシブ配置。
- `FontSize` と `GuiScale` による文字・GUI寸法の一括変更、文字切れを防ぐ行高下限、入力終了を待つ `ApplyTheme()` 再適用。
- 入力欄は標準130pxを上限として右余白を残し、狭幅時だけ縮小。入力文字・選択背景・カーソルも別色へ分離。
- チェック、色見本、変更ボタン、スライダー、Pickerの寸法比率をテーマへ公開。
- `GraphBackground` と `GraphGrid` を実際のグラフ描画へ接続。
- Bool、Int、Float、Enum、Text、Color、Vector、Action、Watch、Graph、Group、子ページ。
- 数値スライダー、直接文字入力、HSV・アルファ編集、折れ線グラフ。
- お気に入り、検索、Undo/Redo、値の保存・復元を支えるサービス API。
- Input System の任意検出と `DebugMenuInputState` による独自入力差し替え。
- `[DebugMenuRegister]` による分散登録と IL2CPP 向け `[Preserve]` 運用。
- `Tools > Debug Menu > Add To Scene` による PanelSettings と Controller のセットアップ。
- 2 つの最上位ページを含む Basics サンプル。
