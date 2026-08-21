# アセット参照管理（Reference Finder）

## 30秒で分かる

Project windowでAssetを選び、**Find Asset References**を実行すると、そのAssetを参照しているScene、Prefab、Material、ScriptableObjectなどを一覧表示します。さらに置換先Assetを指定すると、変更可能なserialized propertyだけを事前確認し、Undo可能な一括置換を実行できます。

Unity標準の依存関係APIは「このAssetが何を使うか」を取得できますが、「このAssetを誰が使うか」は候補を逆向きに調べる必要があります。本moduleが検索、利用箇所の確認、対応可能な参照のPreview、安全な置換までを1つのWindowにまとめます。

## こんなときに使う

- Texture、Material、Prefabを削除・置換する前に利用箇所を知りたい。
- 古いTexture、Material、Prefab、ScriptableObjectへの参照を新しいAssetへまとめて切り替えたい。
- ScriptableObjectの参照元をSceneやPrefabから探したい。
- Project全体を文字列検索して誤検出を整理する手間を減らしたい。
- 参照結果をpath一覧としてコピーしたい。
- 大規模Projectで検索対象folderを絞りたい。
- MaterialやPrefabを経由して最終的に利用するSceneまで辿りたい。

## 使わない方がよいケース

- `Resources.Load`、Addressables key、独自IDなど、文字列から動的に解決する参照を探したい。
- C#コード上の型・メソッド参照を検索したい。
- 間接参照をすべて展開した巨大な依存グラフが必要。
- Scene内参照やImporter設定も含め、形式を問わず自動置換したい。

検索はAssetDatabaseが認識するAsset依存を対象にします。置換は`SerializedObject`から具体的なpropertyを特定できた直接参照だけが対象です。

## 3分で試す

1. Package Managerで **Reference Finder Basics** をImportします。
2. `ReferenceFinderExampleTarget.asset`を選択します。
3. Project windowで右クリックし、**Find Asset References**を選びます。
4. **Search Mode**を`Direct`にすると`ReferenceFinderExampleOwner.asset`だけが表示されます。
5. **Replacement Asset**へ`ReferenceFinderExampleReplacement.asset`を指定し、**Preview Replacement**を押します。
6. `Owner / _reference`という変更予定を確認します。
7. 実際に変更する場合だけ**Replace Previewed References**を押します。直後ならUnityのUndoで元へ戻せます。
8. **Search Mode**を`Recursive`にすると、検索結果には`ReferenceFinderExampleRoot.asset`も表示されます。

Windowは **Tools > Reference Finder** からも開けます。

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

## 結果の見方

- **Direct References**: 対象を直接依存に持つAsset。
- **Scanned**: 実際に確認した候補数と総候補数。
- **FailedAssetPaths**: Unityが依存関係を読み取れなかった候補。
- **WasCanceled**: Windowの進捗表示でCancelしたため結果が途中かどうか。
- **SearchMode**: 直接参照だけか、間接参照を含むか。
- **Occurrences**: 置換できるAsset path、owner、serialized property path。
- **UnsupportedAssetPaths**: 依存はあるが安全なpropertyとして確定できず、変更しないAsset。
- **FailedAssetPaths**: 読み取りに失敗したAsset。1件でもあるPlanは適用できません。

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

## 非目標

- C# symbol検索。
- 文字列keyの推測。
- Addressables catalog解析。
- Scene、Importer、文字列keyの自動置換。
- 型の異なるAssetへの強制置換。
- Unsupported Assetの推測更新。
- Assetの削除。
- Runtimeでの検索。
