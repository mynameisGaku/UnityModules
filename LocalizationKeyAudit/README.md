# Localization Key Audit

## 30秒で分かる説明

Localization Key Auditは、Unity LocalizationのString Table Collectionについて、明示したrequired localeごとのdirect coverageとtable integrityを手動で確認するEditor専用ツールです。結果はadvisory（判断材料）であり、assetを変更せず、buildを停止しません。

## できること

- required localeのtableがあるかを確認します。
- 共有keyごとに、required localeのdirect entryとdirect valueを確認します。
- `MissingLocaleTable`、`MissingDirectEntry`、`EmptyDirectValue`を別々に報告します。
- 宣言されたcoverage scope内の静的参照だけを調べ、見つからない場合は`NoStaticReferenceFoundWithinDeclaredScope`と報告します。
- findingと一緒にcoverage scope、coverage外、incomplete要因を示します。

## 使わない方がよい場合

- locale fallbackを含むruntimeの最終表示可否を確定したい場合
- C# source code、dynamic lookup、Smart String内のnested参照を網羅したい場合
- Packages、Addressables、remote content、外部dataまで到達可能性を解析したい場合
- buildを自動で失敗させたい場合
- entryの自動修正や削除を行いたい場合

このpackageは、これらの目的を満たしません。特に、静的参照が見つからないkeyをunusedとは判定しません。

## 3分で試す

1. Package Managerの「Add package from git URL...」へ次を入力します。

   ```text
   https://github.com/mynameisGaku/UnityModules.git?path=/LocalizationKeyAudit#localization-key-audit-v1.0.0
   ```

2. Unity Editorの`Tools/Localization Key Audit/Open`からwindowを開きます。
3. `Required Locales`へカンマまたは改行区切りのLocale identifier、`Declared Assets Paths`へ改行区切りの`Assets` scopeを入力します。
4. `Audit`を実行します。監査はbuttonを押したときだけ行われます。
5. findingだけでなく、表示されたcoverageと完了状態も確認します。

sample assetは同梱していません。既存projectのLocalization assetに対して手動で実行します。

## 最小のEditor操作例

次のように対象を明示してから監査します。

```text
Required locales: en, ja
Declared Assets Paths:
Assets/Game
Assets/UI

1. Audit
2. Completion statusとStatic coverageを確認
3. Issuesを選択してDetailsを確認
```

scope外にも参照があり得るprojectでは、`NoStaticReferenceFoundWithinDeclaredScope`を削除判断へ使わないでください。

## 実行結果

| Finding / status | 意味 | 断定しないこと |
| --- | --- | --- |
| `MissingLocaleTable` | 対象collectionに、明示したrequired localeのtableがありません。 | locale fallbackなどを含むruntimeの最終結果は断定しません。 |
| `MissingDirectEntry` | required localeのtableはありますが、共有keyに対応するdirect entryがありません。 | fallbackにより値が得られる可能性を否定しません。 |
| `EmptyDirectValue` | direct entryはありますが、そのdirect valueが空です。 | 空値が意図的かどうか、runtimeで別の値が得られるかは断定しません。 |
| `NoStaticReferenceFoundWithinDeclaredScope` | 宣言されたcoverage scope内で、対象keyへの静的参照を検出できませんでした。 | keyが未使用であるとは判定せず、削除候補にも変換しません。 |
| `StaticReferenceCoverageIncomplete` | scope内に未対応形式、読取失敗、上限超過などがあり、参照走査を完了できませんでした。 | 認識済みの部分だけから参照なしとは判定しません。 |
| `OrphanedLocaleTable` | typed String Tableに対応するString Table Collectionが見つかりませんでした。 | assetを自動修復・削除しません。 |
| `OrphanedSharedTableData` | valid raw Shared Table Dataに対応するtyped String／Asset Table ownerが見つかりませんでした。 | String用のassetだとは断定せず、assetを自動修復・削除しません。 |
| `ReadOnlyGuaranteeUnavailable` | raw preflightで、typed loadをread-onlyのまま実行できると証明できませんでした。 | 不完全な結果を通常の監査完了として扱いません。 |

`MissingLocaleTable`、`MissingDirectEntry`、`EmptyDirectValue`は互いに別の状態です。ひとつの「未翻訳」判定へまとめません。

## よくある問題

### `ReadOnlyGuaranteeUnavailable`で停止する

Shared Table Dataのraw serialized dataを安全に読めないか、collection GUIDが欠落・空・malformedです。監査はassetを変更しないため、typed adapterを呼ばずに停止します。先にassetの状態を別の安全な手順で確認してください。

### 静的参照なしでもruntimeで使われている

C# source code、dynamic lookup、Smart String内のnested参照、Addressablesや外部dataはcoverage外です。`NoStaticReferenceFoundWithinDeclaredScope`は宣言scope内の観測だけを示します。

### direct findingがあるのにruntimeでは値が表示される

locale fallback、個別参照のfallback設定、Locale override、culture fallbackなどで値が解決された可能性があります。本監査はdirect valueとruntime解決を分離します。

### 結果がincompleteになる

上限到達、読取失敗、scope外path、未対応serialized表現がある場合、結果はincompleteです。問題なしの完全な結果として扱わないでください。

## 詳しい契約

### Package boundary

- 実行は手動です。自動実行、build前処理、CIの合否判定には組み込みません。
- Editor専用です。Runtime assemblyとRuntime APIはありません。
- public APIはありません。
- 監査はread-onlyです。autofix、entry追加、値の書換え、削除、asset保存を行いません。
- WindowはassetをloadするPing／Openを提供せず、選択findingのpathと詳細をclipboardへcopyするだけです。
- 結果は宣言されたrequired localeとcoverage scopeに対する直接的な観測です。fallback後のruntime表示結果や、翻訳が実行時に利用できないことまでは断定しません。

### Read-only preflight

Unity Localization 1.5.12では、`SharedTableData.OnAfterDeserialize()`が保存されたcollection GUID文字列を処理します。GUIDが欠落または空の場合、公式実装は`delayCall`でasset GUIDを代入し、`EditorUtility.SetDirty`を呼ぶため、読み込みだけのつもりでもassetをdirtyにし得ます。一方、非空のGUIDがmalformedな場合は、先に`Guid.Parse`が例外を送出し、この自動修復経路には入りません。typed deserializeを安全に完了できない状態として扱う必要があります。

このため、監査はtyped loadより先にraw serialized dataを検査します。String TableとAsset Tableは同じ`SharedTableData`型を使うため、raw preflightは両方を対象にします。通過後だけtyped String／Asset Table ownerを読み、Asset Tableだけが所有するidentityをString keyのduplicate、orphan、static-reference判定から除外します。Asset Tableのentryやlocalized asset自体はdirect coverage対象にしません。

String TableとAsset Tableが同じcollection GUIDを使う場合、raw YAMLのGUID＋entry IDだけではreference typeを一意に判定できません。この状態はcleanな完全結果へ推測で畳まず、terminal `AuditFailed`として部分結果を破棄します。

Shared Table Dataをrawに読めない、期待するserialized表現を確認できない、またはcollection GUIDが欠落・空・malformedである場合は、監査全体を`ReadOnlyGuaranteeUnavailable`で停止します。その状態ではtyped adapterを1回も呼ばず、部分的に取得できたfindingも通常の完了結果として公開しません。

### Coverage scope

静的参照の確認範囲は実行時に明示し、結果にも表示します。v1.0.0は宣言された`Assets` scope内にあるtext serialized Unity YAMLの`.unity`、`.prefab`、`.asset`から、隣接するtable GUID＋key ID pairとして直接確認できる参照だけを対象にします。folder指定では他の拡張子を対象外とし、未対応fileを直接指定した場合やbinary／非UTF-8／未知のserialized表現はincompleteにします。次の領域や参照形態はcoverage外です。

- `Packages/`と`Library/PackageCache/`内のpackage asset
- C# source code、実行時に組み立てる文字列、reflectionなどのdynamic lookup
- Smart String内部のplaceholderやselector、およびSmart String内にnestedされた`LocalizedString`
- Addressables catalog、remote content、外部data、実行時load経路と到達可能性
- 宣言されたasset scope外のscene、prefab、ScriptableObjectその他のasset
- locale fallback chain、project設定のfallback、各参照のfallback設定やLocale override、culture fallbackによるruntime解決

coverage外があるため、`NoStaticReferenceFoundWithinDeclaredScope`を「unused」と言い換えません。上限到達、読取失敗、scope外path、未対応serialized表現がある場合はincompleteとして扱い、問題なしの完全な結果にはしません。

結果の`References`と`Edges`はraw YAMLで認識したGUID＋entry ID pairを数える観測metricです。Asset Tableだけに解決できるpairもmetricには含みますが、String keyのdangling／参照あり判定には使用しません。

### Dependency and documents

hard dependencyとして`com.unity.localization` 1.5.12を使用します。Addressablesを直接依存として宣言せず、Unity Localization側の依存関係に従います。

完全なfinding semanticsとcoverageは[Documentation](Documentation~/index.md)を参照してください。変更履歴は[CHANGELOG.md](CHANGELOG.md)、ライセンスは[LICENSE.md](LICENSE.md)、third-party情報は[Third-Party Notices.txt](Third-Party%20Notices.txt)にあります。
