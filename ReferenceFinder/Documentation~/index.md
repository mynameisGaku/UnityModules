# 参照元検索

ReferenceFinderはEditor専用の直接参照逆引きmoduleです。

## 処理の流れ

```text
target Asset
    ↓
Assets以下の候補をAssetDatabase.FindAssetsで列挙
    ↓
各候補のAssetDatabase.GetDependencies(path, false)を取得
    ↓
target pathを直接含む候補だけをordinal順で返す
```

`recursive: false`を使うため、結果は直接参照に限定されます。target自身とfolderは候補から除外します。

## 公開API

- `AssetReferenceFinder.FindDirectReferences(Object)`
- `AssetReferenceFinder.FindDirectReferences(string, IReadOnlyList<string>)`
- `AssetReferenceSearchResult`

Windowの進捗Cancel、選択、Ping、Open、path copyはEditor UIだけの補助です。

## 検証方針

- 実Assetを一時生成し、直接・間接・無関係の参照を区別する。
- path順をordinalで固定する。
- folderの重複・入れ子・無効pathを検証する。
- Cancel時に途中結果であることを明示する。
- import済みsampleのtargetがownerから1件だけ直接参照されることを確認する。
