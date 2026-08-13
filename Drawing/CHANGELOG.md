# Changelog

このパッケージの変更履歴。書式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に、バージョン番号は [Semantic Versioning](https://semver.org/lang/ja/) に従う。

## [1.0.0] - 2026-08-13

### Added

- `Draw` 静的クラス。`Line` `Ray` `Arrow` `Path` `Box` `Bounds` `Sphere` `Circle` `Capsule` `Point` `Axis` `Text` `Clear`。
  シーンに置くものも初期化の呼び出しも要らず、最初に呼んだ時点で必要なものが用意される。
- 共通の省略可能引数 `color` / `duration` / `thickness` / `depthTest` と、
  既定値をまとめて差し替える `Draw.Scope`（構造体を返すので確保なし）。
- 画面上のピクセル単位で一定になる線の太さ。頂点シェーダ側でスクリーン空間へ展開するため、
  カメラが複数あっても向きが狂わない。太さの下限は 1.5 ピクセル。
  ちょうど 1 ピクセル幅にすると、四角形が画素の中心をまたいだ場所で塗られず線が虫食いになるため。
- Linear 色空間での色の一致。表示直前の linear → sRGB 変換を見越して頂点色を落としてから渡すので、
  指定した `Color` がそのままの見た目で出る。
- `duration` による持続表示。1 フレーム線は深度別の描画処理へ一度渡すまで保持し、
  1 フレーム文字はカメラが無い再描画で破棄するため、古い文字が後からまとめて出ない。
- ワールド座標に出す文字（`Draw.Text`）。追加のフォント資産もパッケージも要らない。
- `[Conditional("UNITY_EDITOR")]` と `[Conditional("DEVELOPMENT_BUILD")]` による、
  リリースビルドでの呼び出しごとの除去。引数の計算も残らない。
- 溜まった線分 16384 本・文字 1024 件の上限と、超えたときの種類別の 1 回だけの警告。
  持続時間を付けたまま毎フレーム呼んだときに、描画が原因で作業が止まらないようにする。
- `Graphics.RenderMesh` にメッシュを 1 枚渡す描画。Unity 6000.5.7f1、URP 17.5、Windows D3D12 で動作確認済み。
- Domain Reload を無効にしても、Play Mode ごとに描画設定と終了状態を既定値へ戻す初期化。
- 持続時間つきの線と文字は、未描画でも期限に達した時点で破棄する寿命管理。
- ゲームビューの再描画が来ないbatch/headless環境でも、1フレーム文字を無期限に保持しない寿命管理。
- 線と文字を分けた容量超過の検出と警告。負の容量は 0 として扱う。
- NaN・Infinity を含む座標の拒否と、持続時間・太さ・色の安全な既定値への補正。
- 箱の線分計算と文字表示で毎回発生していた一時確保の除去。
- 新 Input System のみのプロジェクトでも追加設定なしで動き、Sceneを開いてPlayするだけで確認できるサンプル。
- Package Manager の Git URL と安定版タグ `drawing-v1.0.0` を使う導入手順。
- 公開APIを `Draw` と `DrawScope` だけに限定し、内部描画型を利用側から隠す構成。

### Notes

- 非再生時（編集中）は描かれない。Play Mode とビルドでのみ動作する。
- 文字が出るのはゲームビューのみで、位置は `Draw.Camera`（既定は `Camera.main`）を基準にする。
- 形は輪郭線だけで表し、面は塗らない。
- Built-in Render Pipeline、HDRP、Windows D3D12 以外の環境は未検証。
- APIは Unity のメインスレッドから呼び出す。
- 保持上限は線分 16384 本、文字 1024 件。
