# アセット整理・参照管理（Reference Finder）

## 30秒で分かる

Project windowでAssetを選ぶだけで、次の3つを実行できます。

- **Find Asset References**: そのAssetを使っているScene、Prefab、Material、ScriptableObjectなどを探す。
- **Preview Replacement**: 安全に特定できたserialized propertyだけを確認し、Undo可能な参照置換を行う。
- **Batch Rename Selected Assets**: 複数Assetへ文字置換、prefix、suffixをまとめて適用し、変更後の全pathを確認してから名前を変える。

Unity標準の依存関係APIは「このAssetが何を使うか」を取得できますが、「このAssetを誰が使うか」は候補を逆向きに調べる必要があります。また、複数Assetの命名を揃える作業は1個ずつRenameする必要があります。本moduleは参照の検索・置換とAsset名の一括整理を、事前確認を必須にしたEditor toolとしてまとめます。

## こんなときに使う

- Texture、Material、Prefabを削除・置換する前に利用箇所を知りたい。
- 古いTexture、Material、Prefab、ScriptableObjectへの参照を新しいAssetへまとめて切り替えたい。
- ScriptableObjectの参照元をSceneやPrefabから探したい。
- Project全体を文字列検索して誤検出を整理する手間を減らしたい。
- 参照結果をpath一覧としてコピーしたい。
- 大規模Projectで検索対象folderを絞りたい。
- MaterialやPrefabを経由して最終的に利用するSceneまで辿りたい。
- ImportしたTextureへ共通prefixを付けたい。
- 複数Asset名の一部をまとめて置換したい。
- Rename後もGUIDによる参照を維持したい。

## 使わない方がよいケース

- `Resources.Load`、Addressables key、独自IDなど、文字列から動的に解決する参照を探したい。
- C#コード上の型・メソッド参照を検索したい。
- 間接参照をすべて展開した巨大な依存グラフが必要。
- Scene内参照やImporter設定も含め、形式を問わず自動置換したい。
- folder、Package内Asset、sub-assetの名前もまとめて変えたい。

検索はAssetDatabaseが認識するAsset依存を対象にします。置換は`SerializedObject`から具体的なpropertyを特定できた直接参照だけが対象です。

## 3分で参照検索・置換を試す

1. Package Managerで **Reference Finder Basics** をImportします。
2. `ReferenceFinderExampleTarget.asset`を選択します。
3. Project windowで右クリックし、**Find Asset References**を選びます。
4. **Search Mode**を`Direct`にすると`ReferenceFinderExampleOwner.asset`だけが表示されます。
5. **Replacement Asset**へ`ReferenceFinderExampleReplacement.asset`を指定し、**Preview Replacement**を押します。
6. `Owner / _reference`という変更予定を確認します。
7. 実際に変更する場合だけ**Replace Previewed References**を押します。直後ならUnityのUndoで元へ戻せます。
8. **Search Mode**を`Recursive`にすると、検索結果には`ReferenceFinderExampleRoot.asset`も表示されます。

Windowは **Tools > Reference Finder** からも開けます。

## 3分で一括Renameを試す

1. Project windowで名前を変えたいAssetを複数選択します。
2. 右クリックし、**Batch Rename Selected Assets**を選びます。
3. `Find`と`Replace`、または`Prefix`と`Suffix`を入力します。
4. **Preview**を押し、全ての変更前pathと変更後pathを確認します。
5. **Apply Previewed Renames**を押します。

Windowは **Tools > Asset Management > Batch Rename** からも開けます。PreviewはAssetを変更しません。Apply直前にGUID、元path、変更先の空きをもう一度検証するため、Preview後にProjectが変わっていた場合はRenameを開始しません。

## APIから使う

```csharp
using ReferenceFinder;

var result = AssetReferenceFinder.FindDirectReferences("Assets/UI/MainMenu.prefab");
foreach (var referencePath in result.ReferenceAssetPaths)
{
    UnityEngine.Debug.Log(referencePath);
}
```

特定folderだけを検索する場合は第2引数へ`Assets`以下のfolder pathを渡します。

```csharp
var result = AssetReferenceFinder.FindDirectReferences(
    "Assets/UI/CommonButton.mat",
    new[] { "Assets/Scenes", "Assets/Prefabs" });
```

間接参照まで検索する場合は`FindReferences`へ`Recursive`を渡します。

```csharp
var result = AssetReferenceFinder.FindReferences(
    "Assets/UI/CommonButton.mat",
    AssetReferenceSearchMode.Recursive,
    new[] { "Assets/Scenes", "Assets/Prefabs" });
```

置換は必ずPreviewを作り、その同じPlanを適用します。Preview後にfieldが変更されていた場合は、何も変更せず失敗します。

```csharp
var plan = AssetReferenceReplacer.Preview(oldAsset, newAsset, new[] { "Assets/UI" });
foreach (var occurrence in plan.Occurrences)
{
    UnityEngine.Debug.Log($"{occurrence.AssetPath}: {occurrence.PropertyPath}");
}

if (plan.FailedAssetPaths.Count == 0)
{
    var replacementResult = AssetReferenceReplacer.Apply(plan);
    UnityEngine.Debug.Log($"Replaced {replacementResult.ReplacedReferenceCount} references.");
}
```

一括RenameもPreviewとApplyを分離しています。

```csharp
var renamePlan = AssetBatchRenamer.Preview(
    selectedAssets,
    "Old",
    "New",
    "UI_",
    "_v2");

foreach (var entry in renamePlan.Entries)
{
    UnityEngine.Debug.Log($"{entry.OriginalPath} -> {entry.NewPath}");
}

var renameResult = AssetBatchRenamer.Apply(renamePlan);
UnityEngine.Debug.Log($"Renamed {renameResult.RenamedAssetCount} assets.");
```

## 結果の見方

- **Direct References**: 対象を直接依存に持つAsset。
- **Scanned**: 実際に確認した候補数と総候補数。
- **FailedAssetPaths**: Unityが依存関係を読み取れなかった候補。
- **WasCanceled**: Windowの進捗表示でCancelしたため結果が途中かどうか。
- **SearchMode**: 直接参照だけか、間接参照を含むか。
- **Occurrences**: 置換できるAsset path、owner、serialized property path。
- **UnsupportedAssetPaths**: 依存はあるが安全なpropertyとして確定できず、変更しないAsset。
- **FailedAssetPaths**: 読み取りに失敗したAsset。1件でもあるPlanは適用できません。
- **AssetRenamePlan.Entries**: 一括RenameするGUID、変更前path、変更後path。
- **AssetRenameResult.RenamedAssetPaths**: 完了した変更後path。

結果pathはordinal順で固定されます。同じProject状態なら表示順が変わりません。

## 注意点

- 検索候補は`Assets`以下です。Package Assetをtargetにして、Project側の利用箇所を探すことはできます。
- sub-assetを選んだ場合もAsset file単位で判定します。
- `Direct`では、AがBを参照しBがtargetを参照する場合、Aは結果に含みません。
- `Recursive`では、AがBを参照しBがtargetを参照する場合もAを結果に含みます。
- 大規模Projectでは検索に時間がかかります。Windowの進捗表示から中止できます。
- 置換元と置換先は同じ具体型である必要があります。
- Scene（`.unity`）、script参照、文字列key、Importer内部などは自動置換しません。
- `Recursive`検索結果は調査用です。置換Previewは直接参照だけを再検査します。
- Preview後に参照が変わった場合、置換は開始前に停止します。
- 置換はUndoへ記録し、変更したAssetを保存します。version control上の差分も確認してください。
- 一括Renameは`AssetDatabase.RenameAsset`を使うためGUID参照を維持しますが、Unity Undoへは登録されません。必ずPreviewとversion controlの差分を確認してください。
- 一括Renameはscript、folder、Package内Asset、sub-asset、caseだけが異なる変更、重複path、既存pathとの衝突を拒否します。scriptのfile nameと型名を別々に変えてcompileを壊す操作は扱いません。
- 一括Rename中に予期しない失敗が起きた場合、同じ処理内で完了済みのRenameを逆順に戻します。復旧にも失敗したpathは例外messageへ含めます。
- PlayerへRuntime codeは入りません。Editor専用moduleです。

## トラブルシューティング

### 参照しているはずのAssetが出ない

文字列key、reflection、Addressables label、`Resources.Load`などの動的参照はAssetDatabaseの直接依存ではありません。通常のObject field参照か確認してください。

### 結果が多すぎる、検索に時間がかかる

Windowの**Search Root**へ`Assets`以下のfolderを指定してください。Project windowでfolderまたはその中のAssetを選び、**Use Selection Folder**でも設定できます。APIでは複数folderを指定できます。

### 検索結果が途中までしかない

`WasCanceled`を確認してください。Cancelした結果は途中経過です。

### 検索結果には出るが置換対象にならない

Scene、Importer設定、Unityが通常の`SerializedProperty`として公開しない参照は`Unsupported Assets`へ表示します。誤更新を避けるため、自動置換せず対象AssetをOpenして手動で確認してください。

### Replaceが押せない

`Inspection Failures`が1件以上ある場合、Previewの完全性を保証できないため置換を停止します。Consoleと対象pathを確認し、問題を解消してからPreviewを取り直してください。

### Batch RenameのPreviewが失敗する

選択にscript、folder、Package内Asset、sub-assetが含まれていないか確認してください。変更後pathが既存Assetや同じPreview内の別Assetと重なる場合も停止します。WindowsとmacOSで結果が変わらないよう、caseだけを変えるRenameも対象外です。

### Batch Renameを元へ戻したい

Asset名の変更はUnity Undoの対象ではありません。version controlから変更前のpathを確認し、必要に応じて戻してください。参照はGUIDで維持されるため、`.meta`を削除・作り直さないでください。

## 非目標

- C# symbol検索。
- 文字列keyの推測。
- Addressables catalog解析。
- Scene、Importer、文字列keyの自動置換。
- 型の異なるAssetへの強制置換。
- Unsupported Assetの推測更新。
- Assetの削除。
- script、folder、Package内Asset、sub-assetのRename。
- Asset RenameのUnity Undo対応。
- Runtimeでの検索。
