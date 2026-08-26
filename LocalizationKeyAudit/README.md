# Localization Key Audit

## 30秒で分かる説明

Localization Key Auditは、Unity LocalizationのString Table Collectionについて、明示したrequired localeごとのdirect coverageとtable integrityを手動で確認するEditor専用ツールです。静的参照は、1回の監査につき`Assets`または1つのregistered packageのどちらか一方をlogical rootとして、同じroot内に宣言したpathだけを対象にできます。監査結果全体のfindingは4カテゴリ別の件数でも確認でき、現在のfilterで実際に表示するfindingだけをまとめてcopyできます。結果はadvisory（判断材料）であり、assetを変更せず、buildを停止しません。

## できること

- required localeのtableがあるかを確認します。
- 共有keyごとに、required localeのdirect entryとdirect valueを確認します。
- `MissingLocaleTable`、`MissingDirectEntry`、`EmptyDirectValue`を別々に報告します。
- 1回につきlogical rootを`Assets`または1つの`Packages/<registered-name>`に限定し、そのroot内に宣言された静的参照だけを調べます。見つからない場合は`NoStaticReferenceFoundWithinDeclaredScope`と報告します。
- findingと一緒にcoverage scope、coverage外、incomplete要因を示します。
- filter前のfindingを`Terminal`、`Required Locale Coverage`、`Static References`、`Integrity`の4カテゴリ別に集計します。
- SearchとCategory filter後に一覧へ実際に描画する先頭500件だけを、result順と重複を保って`Copy Displayed` buttonからcopyします。

## 使わない方がよい場合

- locale fallbackを含むruntimeの最終表示可否を確定したい場合
- C# source code、dynamic lookup、Smart String内のnested参照を網羅したい場合
- `Packages`全体、未登録package、Addressables、remote content、外部dataまで到達可能性を自動解析したい場合
- buildを自動で失敗させたい場合
- entryの自動修正や削除を行いたい場合

このpackageは、これらの目的を満たしません。特に、静的参照が見つからないkeyをunusedとは判定しません。

## 3分で試す

1. Package Managerの「Add package from git URL...」へ次を入力します。

   ```text
   https://github.com/mynameisGaku/UnityModules.git?path=/LocalizationKeyAudit#localization-key-audit-v1.3.0
   ```

2. Unity Editorの`Tools/Localization Key Audit/Open`からwindowを開きます。
3. `Required Locales`へカンマまたは改行区切りのLocale identifierを入力します。`Declared Asset Paths`の既定値は`Assets`です。必要なら、同じ`Assets` root内のpath、または1つのregistered package root内のpathだけへ置き換えます。
4. `Audit`を実行します。監査はbuttonを押したときだけ行われます。
5. findingだけでなく、表示されたcoverageと完了状態も確認します。
6. 必要なら`Copy Displayed` buttonで、現在のfilterにより一覧へ実際に表示されているfindingをcopyします。

sample assetは同梱していません。既存projectのLocalization assetに対して手動で実行します。

## 最小のEditor操作例

通常は、既定のAssets-only scopeを同じ`Assets` root内で絞り込んで監査します。

```text
Required locales: en, ja
Declared Asset Paths:
Assets/Game
Assets/UI

1. Audit
2. Completion statusとStatic coverageを確認
3. Issuesを選択してDetailsを確認
```

registered packageだけを監査する場合は別のauditとして実行します。次の`com.yourcompany.localization-content`はplaceholderです。実際に登録されているpackageのmanifest `name`へ置き換え、`Assets`や別packageのpathを同じ入力へ混在させないでください。

```text
Required locales: en, ja
Declared Asset Paths:
Packages/com.yourcompany.localization-content/Runtime
Packages/com.yourcompany.localization-content/Content

1. Audit
2. Completion statusとStatic coverageを確認
3. Issuesを選択してDetailsを確認
```

scope外にも参照があり得るprojectでは、`NoStaticReferenceFoundWithinDeclaredScope`を削除判断へ使わないでください。

## 実行結果

Windowの`Issue Categories (unfiltered result)`は、現在の監査resultに含まれる全findingを`Terminal`、`Required Locale Coverage`、`Static References`、`Integrity`へ1回だけ分類した件数です。Search、Category filter、一覧の500件表示上限を変えても、この内訳は変わりません。件数はfinding数であり、uniqueなasset数、collection数、key数ではありません。`Clear`は結果と内訳を消し、次の`Audit`は新しいresultから集計します。

resultまたはStatic coverageが`Incomplete`の場合、あるカテゴリが0件でも、そのカテゴリに問題がない、安全である、またはfindingが存在しないことの証明にはなりません。内訳より`Complete`／`Incomplete`とcoverageの完了状態を優先してください。

### 表示中findingの一括copy

`Copy Displayed Issues`は、現在のSearchとCategory filterを適用した`visibleIssueIndices`のうち、一覧で実際に描画される先頭`min(filtered, 500)`件だけをcopyします。500件を超えてfilterに一致する表示外findingは含めません。result内の順序とduplicate findingをそのまま保ち、resultまたはStatic coverageが`Incomplete`でも、現在表示できているfindingのcopyは抑止しません。ただし、copyできた内容は監査の完了性を保証しません。

clipboard本文の先頭には`Result`、`Static Coverage`、`Displayed Issues`、`Filtered Issues`、`Total Issues`を記録します。これにより、Window外で本文だけを確認する場合も、Incomplete resultや500件の表示上限を全件結果と誤読しないようにします。

区切りを含むclipboard文字列全体が1,048,576 UTF-16 code unitの場合はexactに受理します。1 code unitでも超える場合は、切り詰めやpartial copyを行わず操作全体を拒否し、既存clipboardを変更しません。表示findingが0件の場合、resultや表示indexがinvalid／staleな場合、`Clear`後、または監査の例外catch後もcopyできず、clipboardを変更しません。既存の選択1件用`Copy Details`の内容と動作は変更しません。

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

上限到達、読取失敗、scope外path、未対応serialized表現がある場合、結果はincompleteです。normalized duplicate target、rootまたはその全ancestorや選択したchild pathのreparse point、rootからのescapeもfail closedとし、認識済み参照のpartial resultを返しません。問題なしの完全な結果として扱わないでください。

### Package scopeが拒否される

package scopeには、登録済みpackageのmanifest `name`を使った`Packages/<registered-name>`またはその配下を指定します。bare `Packages`、直接指定した`Library/PackageCache`、未登録package名は受け付けません。`Assets`とpackage、または異なる複数packageを混在させた入力は、filesystem access前にincompleteとしてpartial coverage 0件で拒否します。short-nameなどの曖昧性を避けるため、`~`、`:`、またはdot／spaceで終わるsegmentを含む明示pathも拒否します。packageが登録済みかをPackage Managerで確認し、表示名やphysical folder名ではなくpackage名を指定してください。

## 詳しい契約

### Package boundary

- 実行は手動です。自動実行、build前処理、CIの合否判定には組み込みません。
- Editor専用です。Runtime assemblyとRuntime APIはありません。
- public APIはありません。
- 監査はread-onlyです。autofix、entry追加、値の書換え、削除、asset保存を行いません。
- WindowはassetをloadするPing／Openを提供せず、選択findingのlogical pathと詳細、または現在表示中のfindingだけをclipboardへcopyします。Window、監査結果、error、clipboardへphysical pathを露出せず、読取errorはlogical pathとexception typeだけを示します。
- 結果は宣言されたrequired localeとcoverage scopeに対する直接的な観測です。fallback後のruntime表示結果や、翻訳が実行時に利用できないことまでは断定しません。
- registered package対応で広がるのはstatic-reference coverageだけです。raw preflight、typed snapshot、direct coverage、integrity、graph、finding taxonomyは変更しません。

### Read-only preflight

Unity Localization 1.5.12では、`SharedTableData.OnAfterDeserialize()`が保存されたcollection GUID文字列を処理します。GUIDが欠落または空の場合、公式実装は`delayCall`でasset GUIDを代入し、`EditorUtility.SetDirty`を呼ぶため、読み込みだけのつもりでもassetをdirtyにし得ます。一方、非空のGUIDがmalformedな場合は、先に`Guid.Parse`が例外を送出し、この自動修復経路には入りません。typed deserializeを安全に完了できない状態として扱う必要があります。

このため、監査はtyped loadより先にraw serialized dataを検査します。String TableとAsset Tableは同じ`SharedTableData`型を使うため、raw preflightは両方を対象にします。通過後だけtyped String／Asset Table ownerを読み、Asset Tableだけが所有するidentityをString keyのduplicate、orphan、static-reference判定から除外します。Asset Tableのentryやlocalized asset自体はdirect coverage対象にしません。

String TableとAsset Tableが同じcollection GUIDを使う場合、raw YAMLのGUID＋entry IDだけではreference typeを一意に判定できません。この状態はcleanな完全結果へ推測で畳まず、terminal `AuditFailed`として部分結果を破棄します。

Shared Table Dataをrawに読めない、期待するserialized表現を確認できない、またはcollection GUIDが欠落・空・malformedである場合は、監査全体を`ReadOnlyGuaranteeUnavailable`で停止します。その状態ではtyped adapterを1回も呼ばず、部分的に取得できたfindingも通常の完了結果として公開しません。

### Coverage scope

静的参照の確認範囲は実行時に明示し、結果にも表示します。既定scopeはAssets-onlyです。1回の監査が受け付けるlogical rootは、`Assets`または1つの`Packages/<registered-name>`のexact 1つです。同じroot配下なら複数pathを宣言できます。package名は登録済みpackageのmanifest `name`とexactに照合し、対応する`PackageInfo.resolvedPath`を内部のphysical rootとして使います。

bare `Packages`、直接指定した`Library/PackageCache`、未登録package名は拒否します。`Assets`とpackage、または異なる複数packageのrootを混在させた場合はfilesystem access前にincompleteとし、認識済みreferences／edgesをpartial coverageとして返しません。登録済みpackageを検索する場合も、必ず`Packages/<registered-name>[/...]`として明示します。

明示pathに`~`、`:`、またはdot／spaceで終わるsegmentがある場合はshort-nameなどの曖昧性を避けるため拒否します。解決後のnormalized targetが重複する場合、physical root自身またはその全ancestorや選択したchild pathにreparse pointがある場合、root外へescapeする場合もfail closedとし、partial resultを返しません。Window、監査結果、error、clipboardには宣言したlogical pathだけを残し、physical rootやexception messageを露出しません。読取errorはlogical pathとexception typeだけを示します。

対象scope内にあるtext serialized Unity YAMLの`.unity`、`.prefab`、`.asset`から、隣接するtable GUID＋key ID pairとして直接確認できる参照だけを対象にします。folder指定では他の拡張子を対象外とし、未対応fileを直接指定した場合やbinary／非UTF-8／未知のserialized表現はincompleteにします。同じlogical root内で複数pathを宣言しても、asset候補、directory、file、byte、reference、issueを含む全ての安全上限は監査全体で適用します。次の領域や参照形態はcoverage外です。

- bare `Packages`、直接指定した`Library/PackageCache`、未登録package、および未宣言のregistered package asset
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
