# アセット設定チェック 1.0.0

## 目的

Texture Import Settings のばらつきを、対象フォルダー単位で確認して安全にそろえます。Build Guard が Scene と Prefab の不備、Reference Finder が参照と Rename を担当するのに対し、この module は importer の設定だけを担当します。

## 使い方

`Tools > Asset Import Audit > Open` で画面を開き、①対象 `Assets` フォルダーを選び、②期待値を指定し、③`Preview` を押します。④差分を確認したら、⑤`Apply Selected` または `Apply All` を押してください。Apply 前に importer の値が変わっていた場合は、Preview をやり直すまで停止します。

![アセット設定チェックの操作順](asset-import-audit-guide.png)

## 設定項目

Max Texture Size、Texture Compression、Mipmap Enabled、sRGB Texture、Read/Write、Filter Mode、Aniso Level を比較・適用します。値の意味は Unity の Texture Import Settings に従います。

## 変更されないもの

Package 配下、Scene、Prefab、Material、Asset の参照、ファイル名、画像データそのものは変更しません。削除や unused 判定も行いません。

## 適用前の確認

Apply 前に別の importer 操作を行った場合は、自動で全件適用を拒否します。対象を小さいフォルダーから確認し、差分の内容を見てから適用してください。
