# アセット設定チェック（Asset Import Audit）

## 30秒で分かる説明

Texture Import Settings をフォルダー単位で一覧化し、同じ設定へそろえるための Editor 専用ツールです。毎回 Inspector を開いて texture を1枚ずつ確認する作業を、5段階の手順にまとめます。

## 最短の使い方

1. Package Manager へ公開 tag の Git URL を追加します。
2. `Tools > Asset Import Audit > Open` を開きます。
3. `Root Folder` に `Assets/Art/Textures` などを入力するか、Project window でフォルダーを選んで `Use Selection` を押します。
4. Max Texture Size などの期待値を決めます。
5. `Preview` で差分を確認し、対象を選んで `Apply Selected`、または全件を `Apply All` します。

![アセット設定チェックの操作順](Documentation~/asset-import-audit-guide.png)

画像の番号どおり、①フォルダーを選ぶ → ②設定を決める → ③Previewを実行 → ④差分を確認 → ⑤選択/全件を適用、の順に操作します。

初期値は一般的なカラーテクスチャ向けです。最大サイズ 2048、Compression、Mipmaps 無効、sRGB 有効、Read/Write 無効、Bilinear、Aniso 1 になっています。プロジェクトの用途に合わせて Preview 前に変更してください。

## できること

- `Assets` 配下の Texture2D を決定論的なパス順で検査する。
- Max Texture Size、Compression、Mipmaps、sRGB、Read/Write、Filter Mode、Aniso Level を比較する。
- Preview 時点の importer 状態を保持し、Apply 前に外部変更を検出する。
- 選択した asset だけ、または差分のある asset 全件を適用する。

## 変更範囲と安全策

- 対象は入力した `Assets` フォルダー以下の Texture2D だけです。
- Package、Scene、Prefab、Material、参照、ファイル名は変更しません。
- Preview 後に importer が変わっていれば、全件を適用せず `StalePlan` で停止します。
- Apply 後の再importは Unity の通常の importer 処理で行われます。大きなフォルダーでは時間とメモリを使うため、最初は小さいサブフォルダーで確認してください。
- これは unused asset 検出や圧縮方式の自動最適化ではありません。正しい値はプロジェクト側で選びます。

## 公開 API

- `AssetImportAuditTextureSettings`: 期待する Texture Import Settings。
- `AssetImportAuditService.Preview`: 差分と再確認用 plan を作成。
- `AssetImportAuditService.Apply`: plan を全件または選択 asset へ適用。
- `AssetImportAuditPlan` / `AssetImportAuditIssue` / `AssetImportAuditApplyResult`: Preview と結果を確認する値。

## 非目標

Addressables、Resources の依存解析、unused asset 判定、画像内容の品質判定、ファイル削除・Rename、参照置換、build 時の自動変更は扱いません。
