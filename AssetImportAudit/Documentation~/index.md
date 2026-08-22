# アセット設定チェック 1.1.0

## 目的

Texture Import Settingsのばらつきを、対象folder単位で確認して安全にそろえます。共通設定に加え、Standalone・Android・iOS向けOverrideを同じ画面でPreviewできます。Build GuardはSceneとPrefabの不備、Reference Finderは参照とRename、このmoduleはImporter設定を担当します。

## 操作順

1. ①`Assets`配下の対象folderを選びます。
2. ②`Settings Scope`をShared Settings、Platform Override、Shared And Platformから選び、期待値を設定します。
3. ③`Preview`を押します。
4. ④pathごとの現在値と期待値を確認します。
5. ⑤`Apply Selected`または`Apply All`を押します。

![アセット設定チェックの操作順](asset-import-audit-guide.png)

## 共通設定とplatform設定

| Scope | 比較・適用する値 |
| --- | --- |
| Shared Settings | Max Texture Size、Texture Compression、Mipmap Enabled、sRGB Texture、Read/Write、Filter Mode、Aniso Level |
| Platform Override | Standalone・Android・iOSのOverride、Max Texture Size、Texture Compression |
| Shared And Platform | 上記の両方 |

iOSはUnityのImporter APIで`iPhone`というplatform名へ対応します。active build targetは変更しません。Overrideを無効にする場合はOverrideだけを変更し、Max Texture Size、Compression、format、resize algorithmなど他の値は保持します。

Max Texture SizeはUnity Importerが表示する32～16384のpresetから選択します。任意の整数を入力してImporter側の補正と再Previewを繰り返す状態にはしません。

## 適用前の確認

Apply前に、選択対象すべての管理対象値をPreview時点のsnapshotと比較します。1件でもImporterが消失・変更していれば、選択対象を一部だけ変更せず`StalePlan`で停止します。未選択Assetの変更は、選択したAssetの適用を妨げません。

![狭いEditorWindowでもApplyを確認できるPreview画面](asset-import-audit-preview.png)

## 変更されないもの

Package配下、Scene、Prefab、Material、Asset参照、file名、画像data、active build targetは変更しません。platform固有のformat、resize algorithm、Crunch、compression quality、alpha split、Android ETC2 fallbackも変更しません。

## 再import

ApplyしたTextureはUnityの通常処理で再importされます。大きなfolderでは時間とmemoryを使うため、小さいsubfolderから確認してください。

## Unity公式資料

- [Platform-specific texture overrides panel reference](https://docs.unity3d.com/ja/current/Manual/class-TextureImporter-type-specific.html)
- [TextureImporterPlatformSettings](https://docs.unity3d.com/ja/current/ScriptReference/TextureImporterPlatformSettings.html)
