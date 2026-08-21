# 参照元検索

ReferenceFinderはEditor専用のAsset参照逆引きmoduleです。直接参照だけでなく、必要な場合は間接参照まで検索できます。

## 処理の流れ

```text
target Asset
    ↓
Assets以下の候補をAssetDatabase.FindAssetsで列挙
    ↓
各候補のAssetDatabase.GetDependencies(path, recursive)を取得
    ↓
target pathを直接含む候補だけをordinal順で返す
```

`Direct`は`recursive: false`、`Recursive`は`recursive: true`を使います。target自身とfolderは候補から除外します。Windowでは`Assets`以下のSearch Rootを1個、APIでは複数folderを指定できます。

## 公開API

- `AssetReferenceFinder.FindDirectReferences(Object)`
- `AssetReferenceFinder.FindDirectReferences(string, IReadOnlyList<string>)`
- `AssetReferenceFinder.FindReferences(Object, AssetReferenceSearchMode, IReadOnlyList<string>)`
- `AssetReferenceFinder.FindReferences(string, AssetReferenceSearchMode, IReadOnlyList<string>)`
- `AssetReferenceSearchResult`
- `AssetReferenceSearchMode`

Windowの進捗Cancel、選択、Ping、Open、path copyはEditor UIだけの補助です。

## 検証方針

- 実Assetを一時生成し、直接・間接・無関係の参照を区別する。
- path順をordinalで固定する。
- folderの重複・入れ子・無効pathを検証する。
- Cancel時に途中結果であることを明示する。
- import済みsampleのtargetがownerから1件だけ直接参照されることを確認する。
- 3段のsampleで、直接検索はownerだけ、間接検索はownerとrootを返すことを確認する。
- Search Root外の参照元を結果から除外する。
