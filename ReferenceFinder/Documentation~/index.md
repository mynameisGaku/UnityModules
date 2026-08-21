# アセット整理・参照管理

ReferenceFinderはEditor専用のAsset整理・参照管理moduleです。直接・間接参照の逆引き、具体的なserialized propertyだけを対象にしたUndo可能な置換、選択AssetのGUIDを維持する一括Renameを提供します。

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

## 置換の流れ

```text
target / replacementをGUID + local file IDで固定
    ↓
直接参照元だけを検索
    ↓
LoadAllAssetsAtPath + SerializedObjectでObjectReference propertyを特定
    ↓
Scene・未特定形式をUnsupportedへ分離
    ↓
利用者がPreviewを確認
    ↓
全propertyが未変更か再検査
    ↓
Undo記録 → 一括置換 → Asset保存
```

置換元と置換先は同じ具体型だけを受け付けます。Preview後にowner、property、参照先のいずれかが変わった場合は、変更前にPlan全体を停止します。読み取り失敗pathが1件でもあるPlanも適用しません。Sceneとscript参照は自動変更しません。

## 一括Renameの流れ

```text
Project windowでmain Assetを複数選択
    ↓
Find / Replace / Prefix / Suffixから変更後pathを計算
    ↓
script・folder・Package・sub-asset・case-only・衝突を拒否
    ↓
利用者が全pathをPreview
    ↓
全GUID・元path・変更先の空きを再検査
    ↓
AssetDatabase.RenameAssetで順番に変更
```

Renameは同じfolder内のfile nameだけを変更します。Unity Undoには登録されないため、version controlを復旧手段にします。途中で予期しない失敗が発生した場合は、完了済みの変更を逆順に戻します。

## 公開API

- `AssetReferenceFinder.FindDirectReferences(Object)`
- `AssetReferenceFinder.FindDirectReferences(string, IReadOnlyList<string>)`
- `AssetReferenceFinder.FindReferences(Object, AssetReferenceSearchMode, IReadOnlyList<string>)`
- `AssetReferenceFinder.FindReferences(string, AssetReferenceSearchMode, IReadOnlyList<string>)`
- `AssetReferenceSearchResult`
- `AssetReferenceSearchMode`
- `AssetReferenceReplacer.Preview(Object, Object, IReadOnlyList<string>)`
- `AssetReferenceReplacer.Apply(AssetReferenceReplacementPlan)`
- `AssetReferenceReplacementPlan`
- `AssetReferenceOccurrence`
- `AssetReferenceReplacementResult`
- `AssetBatchRenamer.Preview(IReadOnlyList<Object>, string, string, string, string)`
- `AssetBatchRenamer.Apply(AssetRenamePlan)`
- `AssetRenamePlan`
- `AssetRenameEntry`
- `AssetRenameResult`

Windowの進捗Cancel、選択、Ping、Open、path copyはEditor UIだけの補助です。

## 検証方針

- 実Assetを一時生成し、直接・間接・無関係の参照を区別する。
- path順をordinalで固定する。
- folderの重複・入れ子・無効pathを検証する。
- Cancel時に途中結果であることを明示する。
- import済みsampleのtargetがownerから1件だけ直接参照されることを確認する。
- 3段のsampleで、直接検索はownerだけ、間接検索はownerとrootを返すことを確認する。
- Search Root外の参照元を結果から除外する。
- PreviewがAsset path、owner local file ID、property pathを決定論的に返すことを確認する。
- 型不一致とscript参照を変更前に拒否する。
- Preview後に変更されたpropertyを検出し、部分更新しないことを確認する。
- 適用後に参照先が置き換わり、Undoで元へ戻ることを確認する。
- Scene参照をUnsupportedへ分離することを確認する。
- Rename Previewがfind、replace、prefix、suffixを決められた順で合成し、元Assetを変更しないことを確認する。
- 重複・既存destination、script、folder、sub-asset、case-only Renameを変更前に拒否する。
- Apply前に古くなったPlanを検出し、他のAssetも変更しないことを確認する。
- Rename後もGUIDとserialized referenceが維持されることを確認する。
