# 参照元検索（ReferenceFinder）

## 30秒で分かる

Project windowでAssetを選び、**Find Direct References**を実行すると、そのAssetを直接参照しているScene、Prefab、Material、ScriptableObjectなどを一覧表示します。

Unity標準の依存関係APIは「このAssetが何を使うか」を取得できますが、「このAssetを誰が使うか」は候補を逆向きに調べる必要があります。本moduleが検索、並び替え、選択、Ping、Open、path copyまでまとめます。

## こんなときに使う

- Texture、Material、Prefabを削除・置換する前に利用箇所を知りたい。
- ScriptableObjectの参照元をSceneやPrefabから探したい。
- Project全体を文字列検索して誤検出を整理する手間を減らしたい。
- 参照結果をpath一覧としてコピーしたい。

## 使わない方がよいケース

- `Resources.Load`、Addressables key、独自IDなど、文字列から動的に解決する参照を探したい。
- C#コード上の型・メソッド参照を検索したい。
- 間接参照をすべて展開した巨大な依存グラフが必要。

v1はAssetDatabaseが認識する**直接のAsset依存**だけを対象にします。

## 3分で試す

1. Package Managerで **Reference Finder Basics** をImportします。
2. `ReferenceFinderExampleTarget.asset`を選択します。
3. Project windowで右クリックし、**Find Direct References**を選びます。
4. `ReferenceFinderExampleOwner.asset`が1件表示されることを確認します。
5. pathをクリックすると選択とPing、**Open**でAssetを開けます。

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

## 結果の見方

- **Direct References**: 対象を直接依存に持つAsset。
- **Scanned**: 実際に確認した候補数と総候補数。
- **FailedAssetPaths**: Unityが依存関係を読み取れなかった候補。
- **WasCanceled**: Windowの進捗表示でCancelしたため結果が途中かどうか。

結果pathはordinal順で固定されます。同じProject状態なら表示順が変わりません。

## 注意点

- 検索候補は`Assets`以下です。Package Assetをtargetにして、Project側の利用箇所を探すことはできます。
- sub-assetを選んだ場合もAsset file単位で判定します。
- 直接依存だけを使うため、AがBを参照しBがtargetを参照する場合、Aは結果に含みません。
- 大規模Projectでは検索に時間がかかります。Windowの進捗表示から中止できます。
- PlayerへRuntime codeは入りません。Editor専用moduleです。

## トラブルシューティング

### 参照しているはずのAssetが出ない

文字列key、reflection、Addressables label、`Resources.Load`などの動的参照はAssetDatabaseの直接依存ではありません。通常のObject field参照か確認してください。

### 結果が多すぎる

APIでは`searchFolders`を指定できます。Windowのfolder選択UIはv1の対象外です。

### 検索結果が途中までしかない

`WasCanceled`を確認してください。Cancelした結果は途中経過です。

## 非目標

- C# symbol検索。
- 文字列keyの推測。
- Addressables catalog解析。
- 参照の自動置換・削除。
- Assetの自動修正。
- Runtimeでの検索。
