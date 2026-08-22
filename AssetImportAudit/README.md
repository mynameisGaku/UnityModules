# アセット設定チェック（Asset Import Audit）

## 30秒で分かる説明

Texture Import Settingsをfolder単位で一覧化し、共通設定とStandalone・Android・iOS向け設定をまとめてそろえるEditor専用ツールです。Textureを1枚ずつ選び、Inspector下部のplatform tabを何度も開く作業を、Previewから適用までの5段階にまとめます。

## できること

- `Assets`配下のTexture2Dを決定論的なpath順で検査する。
- 共通のMax Texture Size、Compression、Mipmaps、sRGB、Read/Write、Filter Mode、Aniso Levelを比較・適用する。
- Standalone・Android・iOSのOverride、Max Texture Size、Compressionを比較・適用する。
- 共通設定だけ、platform設定だけ、または両方を一度にPreviewする。
- Max Texture SizeをUnity Importerのpreset（32～16384）から選び、再Previewで確実に収束させる。
- Preview後の外部変更を検出し、選択対象を一部だけ更新せず全件停止する。
- 選択したAssetだけ、または差分のあるAsset全件を適用する。

## 使わない方がよい場合

画像を見て自動的に最適な圧縮形式を選びたい場合や、Addressables・Resourcesの参照、unused asset、削除・Renameを調べたい場合には向きません。このツールは利用者が決めたImporter設定を安全にそろえる用途へ限定しています。

## 3分で試す

1. Package Managerの`Add package from git URL...`へ次を入力します。

   ```text
   https://github.com/mynameisGaku/UnityModules.git?path=/AssetImportAudit#asset-import-audit-v1.1.0
   ```

2. `Tools > Asset Import Audit > Open`を開きます。
3. ①`Root Folder`へ`Assets/Art/Textures`などを入力するか、Project windowでfolderを選んで`Use Selection`を押します。
4. ②`Settings Scope`を選び、共通設定またはplatform overrideの期待値を決めます。iOSはUnity内部の`iPhone`設定へ対応します。
5. ③`Preview`を押します。
6. ④Assetごとの現在値と期待値を確認し、適用するAssetを選びます。
7. ⑤`Apply Selected`または`Apply All`を押します。

![アセット設定チェックの操作順](Documentation~/asset-import-audit-guide.png)

<details>
<summary>注釈を加える前の実際のEditorWindow</summary>

![実際のAsset Import Audit画面](Documentation~/asset-import-audit-window.png)

</details>

## 実行するとどうなるか

差分があるAssetはpathごとに並び、共通設定は設定名、platform設定は`[Standalone]`・`[Android]`・`[iOS]`付きで現在値から期待値への変化を表示します。Applyすると対象TextureがUnityの通常処理で再importされ、完了後に同じ条件で再Previewされます。差分がなくなれば`No differences found.`と表示されます。

platform overrideを無効にする場合は、Overrideだけを無効化します。Texture format、resize algorithm、compression qualityなど、このmoduleが所有しないplatform設定は保持します。active build targetは切り替えません。

## よくある問題

### Applyが`StalePlan`で止まる

Preview後にInspector、script、別のEditor拡張などが対象Importerを変更しています。現在の状態を勝手に上書きしないための停止です。もう一度Previewして差分を確認してください。

### platformのCompression形式を自動選択してほしい

端末世代、画質、alpha、配布先によって正解が異なるため、自動推測しません。このmoduleが扱う`TextureImporterCompression`はUnityのLow・Normal・High相当の方針です。具体的なGPU formatはUnityのImporterと利用側のtarget方針で決めてください。

### 大きなfolderで時間がかかる

ApplyしたTextureは再importされます。最初は小さいsubfolderを選び、差分と所要時間を確認してから範囲を広げてください。

## 公開API

- `AssetImportAuditTextureSettings`: 共通Texture Import Settings。
- `AssetImportAuditTexturePlatformSettings`: platform overrideの期待値。
- `AssetImportAuditTextureAuditSettings`: 共通・platform・両方の検査範囲。
- `AssetImportAuditService.Preview`: 差分と再確認用planを作成する。
- `AssetImportAuditService.Apply`: planを全件または選択Assetへ適用する。
- `AssetImportAuditPlan` / `AssetImportAuditIssue` / `AssetImportAuditApplyResult`: Previewと結果を確認する値。

## 変更範囲と失敗条件

- 対象は入力した`Assets` folder以下のTexture2Dだけです。
- Package、Scene、Prefab、Material、参照、file名、画像dataは変更しません。
- selected applyでは、選択した全Importerを先に再確認します。1件でも消失・変更していれば、選択対象を1件も変更しません。
- Apply開始後の予期しないUnity Importer例外について、transaction rollbackは保証しません。結果が`ApplyFailed`の場合はImporterを確認して再Previewしてください。
- `ApplyFailed`の`AppliedAssetCount`には、例外が起きる前に再importまで完了したAsset数が入ります。
- platform format、resize algorithm、Crunch、compression quality、alpha split、Android ETC2 fallbackは変更しません。

## 非目標

Addressables、Resourcesの依存解析、unused asset判定、画像内容の品質判定、file削除・Rename、参照置換、build時の自動変更、active build target切替、platform formatの自動推測は扱いません。
