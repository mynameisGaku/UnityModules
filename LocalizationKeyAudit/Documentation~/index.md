# Localization Key Audit

## Purpose

Localization Key Auditは、String Table Collectionの共有keyについて、明示されたrequired localeにdirect tableとdirect valueが揃っているかを手動で確認するEditor専用監査です。監査結果は修正の判断材料であり、runtime翻訳の可否、keyの使用有無、buildの合否を決めるものではありません。

## Audit flow

1. `Tools/Localization Key Audit/Open`からwindowを開き、required localeと静的参照coverage scopeを明示します。既定scopeは`Assets`です。1回の監査ではlogical rootを`Assets`または1つの`Packages/<registered-name>`のどちらか一方に限定し、全ての宣言pathをそのroot配下に置きます。
2. typed objectをloadする前に、Shared Table Dataのraw serialized dataをpreflightします。
3. raw preflightがread-onlyを保証できない場合は、`ReadOnlyGuaranteeUnavailable`として監査全体を停止します。
4. preflightを通過した場合だけtyped loadを行い、String／Asset Table ownerを分類したうえでrequired localeごとのString direct coverageとintegrityを確認します。
5. `Audit`を押し、finding、coverage scope、coverage外、incomplete要因を合わせて確認します。自動scanは行いません。

監査はassetの修正、保存、削除を行いません。autofixも提供しません。

## Issue category counts

Windowの`Issue Categories (unfiltered result)`は、現在の監査resultに含まれる全findingを次の4カテゴリへexact 1つずつ分類した件数です。

| Category | Issue kinds |
| --- | --- |
| `Terminal` | `ReadOnlyGuaranteeUnavailable`、`InvalidConfiguration`、`LimitExceeded`、`AuditFailed` |
| `Required Locale Coverage` | `RequiredLocaleNotConfigured`、`MissingLocaleTable`、`MissingDirectEntry`、`EmptyDirectValue` |
| `Static References` | `DanglingStaticReference`、`NoStaticReferenceFoundWithinDeclaredScope`、`StaticReferenceCoverageIncomplete` |
| `Integrity` | `DuplicateCollectionName`、`DuplicateCollectionGuid`、`DuplicateSharedEntryId`、`DuplicateSharedEntryKey`、`DuplicateLocaleTable`、`DuplicateLocalizedEntryId`、`OrphanedLocalizedEntry`、`OrphanedLocaleTable`、`OrphanedSharedTableData`、`DuplicateLocaleIdentifier` |

内訳はresult取得後に全findingから1回だけ集計し、Search、Category filter、500件の一覧表示上限から独立して保持します。これはfinding数であり、uniqueなasset数、collection数、key数ではありません。`Clear`はresultと内訳を消し、次の`Audit`は新しいresultから集計します。

resultまたはStatic coverageが`Incomplete`の場合、あるカテゴリが0件でも、安全、問題なし、またはfindingなしを証明しません。`Complete`／`Incomplete`とcoverageの完了状態が引き続き監査の完了性を示します。

## Finding semantics

### `MissingLocaleTable`

対象collectionにrequired locale用のtableが存在しない状態です。これはrequired localeへのdirect coverage不足を示します。fallbackを含むruntimeでの最終値は示しません。

### `MissingDirectEntry`

required locale用tableは存在しますが、共有keyに対応するdirect entryがない状態です。locale fallbackなどでruntime値が得られる可能性は残るため、「runtimeで未翻訳」とは断定しません。

### `EmptyDirectValue`

共有keyに対応するdirect entryは存在しますが、direct valueが空の状態です。空値が意図的か、fallbackや実行時処理で別の値になるかは監査対象外です。

### `NoStaticReferenceFoundWithinDeclaredScope`

宣言されたcoverage scope内で静的参照を検出できなかった状態です。coverage外の参照やdynamic lookupがあり得るため、keyのunused判定ではありません。自動削除や削除推奨には使用しません。

### `ReadOnlyGuaranteeUnavailable`

raw preflightでtyped loadのread-only性を証明できなかったterminal statusです。このstatusが発生した監査ではtyped loadを行わず、direct coverageの部分結果を通常の完了結果として扱いません。

### Integrity findings

duplicate String collection／entry／Locale identity、collectionに属さない`OrphanedLocaleTable`、typed String／Asset Table ownerに対応しないvalid raw assetの`OrphanedSharedTableData`を区別して報告します。Asset Tableだけが所有するShared Table DataはString keyのduplicate、orphan、static-reference判定から除外します。StringとAssetで同じcollection GUIDが使われる場合はreference typeをraw YAMLだけで断定できないため、terminal `AuditFailed`として部分結果を破棄します。findingはassetの自動修復や削除を行いません。

## Why raw preflight is required

Unity Localization 1.5.12の`SharedTableData.OnAfterDeserialize()`は、保存されたcollection GUID文字列を処理します。GUIDが欠落または空の場合、公式実装は`delayCall`でasset GUIDを代入し、`EditorUtility.SetDirty`でassetをdirtyにします。一方、非空のGUIDがmalformedな場合は、先に`Guid.Parse`が例外を送出し、この自動修復経路には入りません。typed deserializeを安全に完了できない状態です。

監査は最初にraw serialized representationを読み、collection GUIDが存在し、空でなく、期待する形式として検証可能であることを確認します。String TableとAsset Tableは同じ`SharedTableData`型を使うため、raw preflightは両方を対象にし、成功後だけtyped ownerを分類します。Asset Tableのentryやlocalized assetはdirect coverage対象外です。raw dataを読めない場合、形式を安全に認識できない場合、値が欠落・空・malformedの場合は`ReadOnlyGuaranteeUnavailable`で停止します。binaryや将来の未知のserialization表現も、安全だと推測してtyped loadしません。preflight失敗時のtyped adapter呼出回数は0です。

## Direct coverage and runtime behavior

この監査が確認するのは、指定されたtableに直接保存されたlocale別entryと値です。Unity Localizationのruntime解決は、locale fallback chain、project fallback設定、個別参照のfallback設定、Locale override、culture fallback、Addressablesのload結果などに左右されます。そのためdirect findingからruntimeの表示結果を断定しません。

## Static-reference coverage

結果には、検索した論理asset scopeを明示します。v1.1.0が1回の監査で受け付けるlogical rootは、`Assets`または1つの`Packages/<registered-name>`のexact 1つです。同じroot配下なら複数pathを宣言できます。bare `Packages`を起点とする全package走査は行いません。

`registered-name`は登録済みpackageのmanifest `name`とexactに照合し、`PackageInfo.resolvedPath`をそのpackageのphysical rootとして使います。bare `Packages`、直接指定した`Library/PackageCache`、未登録package名は拒否します。`Assets`とpackage、または異なる複数packageのrootを混在させた場合はfilesystem access前にincompleteとし、認識済みreferences／edgesをpartial coverageとして返しません。

明示pathに`~`、`:`、またはdot／spaceで終わるsegmentがある場合はshort-nameなどの曖昧性を避けるため拒否します。解決後のnormalized targetが重複する場合、physical root自身またはその全ancestorや選択したchild pathにreparse pointがある場合、root外へescapeする場合もfail closedとし、先に認識できた参照をpartial coverageとして返しません。同じlogical root内で複数pathを宣言しても、asset候補、directory、file、byte、reference、issueを含む全ての安全上限は監査全体で適用します。

Window、監査結果、error、clipboardには`Assets[/...]`または`Packages/<registered-name>[/...]`というlogical pathだけを残し、package cache、local package、resolved root、exception messageを含むphysical pathを露出しません。読取errorはlogical pathとexception typeだけを示します。

安全に解決したscope内のtext serialized `.unity`、`.prefab`、`.asset`だけをfolder走査し、隣接するtable GUID＋key ID pairを認識します。未対応fileの直接指定、binary、非UTF-8、未知のserialized表現は部分参照を採用せずincompleteにします。次はv1.1.0のcoverage外です。

| Coverage外 | 理由 |
| --- | --- |
| bare `Packages`、直接指定した`Library/PackageCache`、未登録または未宣言のpackage | package所有assetは、登録名を使って明示した論理scopeだけを対象にします。 |
| C# source code | literal、constructor、生成文字列、reflectionなどを網羅するcode解析は行いません。 |
| Dynamic lookup | 実行時生成keyや外部入力からのlookupは静的に確定できません。 |
| Smart Stringの内部 | placeholder、selector、nested `LocalizedString`をkey参照として完全には展開しません。 |
| Addressablesと外部data | catalog、remote content、外部source、runtime到達可能性は解析しません。 |
| 宣言scope外のasset | scene、prefab、ScriptableObjectを含め、未宣言範囲は検索しません。 |

coverage外があるため、参照を検出できない結果は`NoStaticReferenceFoundWithinDeclaredScope`とだけ報告します。読取失敗、scope外path、上限到達、未対応serialized表現がある場合はincompleteを明示し、cleanな完全結果にしません。

`References`と`Edges`はraw YAMLで認識したGUID＋entry ID pairを数える観測metricです。Asset Tableだけに解決できるpairもmetricには残しますが、String keyのdangling／参照あり判定からは除外します。

## Package boundaries

- Editor-only、manual execution、read-only、advisoryです。
- public APIは0件です。
- Runtime assemblyとRuntime APIは0件です。
- build blocker、build callback、autofix、asset削除はありません。
- WindowはassetをloadするPing／Openを持たず、findingのlogical pathと詳細をclipboardへcopyするだけです。physical pathはcopyしません。
- registered package対応で広がるのはstatic-reference coverageだけです。raw preflight、typed snapshot、direct coverage、integrity、graph、finding taxonomyは変更しません。
- `com.unity.localization` 1.5.12をhard dependencyとします。
- Addressablesへのdirect dependencyは宣言しません。

導入方法と概要は[README](../README.md)、変更履歴は[CHANGELOG](../CHANGELOG.md)、ライセンスは[LICENSE](../LICENSE.md)を参照してください。
