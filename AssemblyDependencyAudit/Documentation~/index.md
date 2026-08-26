# Assembly依存チェック 1.3.0

## 目的

`Assets`と導入済み`Packages`のAssembly Definition（`.asmdef`）を読み取り、参照関係、選択`.asmdef`の宣言単位の参照解決、構成上の問題を1つのEditorWindowで確認します。Assembly Definition Reference（`.asmref`）は別一覧でtargetの整合性を検査し、同じfolderに複数あるassembly owner候補も報告します。read-onlyの検査に限定し、Project fileは変更しません。

## 操作順

1. `Tools > Assembly Dependency Audit > Open`を開きます。
2. `Refresh`を押します。
3. 中央の`Assemblies`列から対象を選びます。
4. 左の`Referenced By`で直接の参照元を確認します。
5. 右の`Depends On`で直接の参照先を確認します。
6. `Details`の`Declared References`で、選択`.asmdef`のName／GUID参照と解決先を確認します。
7. `Assembly References`で`.asmref`の元path、reference、解決先を確認します。
8. 検索と問題filterで対象を絞り、asset pathと問題内容を確認します。

## 3列view

| 列 | 表示内容 |
| --- | --- |
| Referenced By | 選択Assemblyを直接参照しているAssembly |
| Assemblies | `Assets`と`Packages`で見つかったAssembly |
| Depends On | 選択Assemblyが直接参照しているAssembly |

名前参照と`GUID:`参照は、Unityが解決できるAssembly Definitionのpathへ対応付けます。Assembly名はUnity compilerと同じく大小文字を区別せず、解決先がない参照や、同名候補が複数あって決定できない参照はgraphへ推測で追加せず、問題として表示します。

## 選択`.asmdef`の宣言参照

`Declared References`は、選択した`.asmdef`の`references`配列を宣言順のまま表示します。同じ値や同じ解決先が複数回宣言されていても重複を除かず、各rowでName／GUIDのkind、raw declaration、一意に解決したAssembly名とasset pathを確認できます。3列graphの`Depends On`は同じ解決先へのedgeを1本へまとめるため、`Declared References`の件数と一致しない場合があります。

1 Assemblyあたり500件ずつ`Prev`／`Next`でpageを切り替え、解析上限4,096件まで全ての宣言へ到達できます。raw valueとtargetはそれぞれ160文字までをsurrogate pairを分断せず表示し、全文は既存の`Open`から元の`.asmdef`を確認します。既存のAssembly、asmref、Issueのclipboard出力は変更しません。

`Not uniquely resolved`は、一意なtarget indexを持たないことだけを示します。未解決か曖昧かは対応する`Issue Details`の`UnresolvedReference`または`AmbiguousReference`で区別してください。null reference、未知のkind、範囲外index、null targetなど内部resultの不整合は別のAssemblyへ推測せず、invalid rowとして見える状態にします。`.asmref`を選択すると直前の`.asmdef`の宣言参照を隠し、Assemblyの選択変更または監査resultのclearではpageとDetails位置を先頭へ戻します。

## `.asmref` target一覧

`Assembly References`は全`.asmref`をasset path順で表示し、元の`reference`、名前／GUIDの指定方法、一意に解決できたasmdef pathを確認できます。不正なJSON、空または欠落した`reference`、未解決target、曖昧なtargetも一覧と詳細から到達できます。500件ごとの`Prev`／`Next`とfilterで全pageへ移動できます。

GUIDが一意でも、対応する`.asmdef`のJSONまたはAssembly名が有効でなければ解決済みにはしません。判明したasmdef pathを問題詳細へ残して未解決として報告します。

長いpath、reference、messageはEditorの描画負荷を抑えるため画面上だけ省略します。選択後の`Copy Reference`または`Copy Issue`は省略前の全文をclipboardへ入れます。宣言参照rowは表示専用であり、既存Copyの項目へ追加しません。

`.asmref`は同じfolder以下のscriptを既存Assemblyへ所属させるassetであり、asmdef同士の依存を追加するfileではありません。このツールも`.asmref`を`Dependencies`、`Dependents`、循環検出へ追加しません。別folderにある複数の`.asmref`が同じ一意なtargetを指すことは問題として扱いません。

## 同じfolderのowner候補

同じ正規化済みparent folderに2件以上の`.asmdef`／`.asmref`がある場合、各assetへ`MultipleAssemblyOwnersInFolder`を1件ずつ報告します。targetが同じ`.asmref`同士でも重複を消しません。JSON、Assembly名、reference、targetの有効性に関係なく配置を検査するため、不正JSONや未importの物理fileも候補へ含め、既存の構文・target問題と併記します。これはowner候補のpath配置を確認する検査であり、現在どのAssemblyへcompileされたかを断定するものではありません。子folderまたは別folderは競合しません。

## 報告する問題

- 循環参照
- 自己参照
- Playerで使えるAssemblyからEditor専用Assemblyへの参照
- 不正なJSON
- 空のAssembly名
- 重複したAssembly名
- 重複したGUID
- 解決できない参照
- 同名候補による曖昧な参照
- 名前参照と`GUID:`参照の混在
- `includePlatforms`と`excludePlatforms`の同時指定
- `.asmref`の不正なJSON
- `.asmref`の空または欠落した`reference`
- `.asmref`の未解決target
- `.asmref`の曖昧なtarget
- 同じfolderに複数ある`.asmdef`／`.asmref` owner候補

循環検出はgraph全体を調べます。3列viewは選択Assemblyの直接参照だけを表示するため、循環の全経路は問題一覧に含まれるAssemblyを順に選んで確認してください。

## 変更されないもの

`.asmdef`、`.asmref`、C# source、Scene、Prefab、Project Settings、Package manifestは変更されません。Assetのimport、script compile、Player buildも自動実行しません。

## 検査範囲と上限

このツールが判断するのは`.asmdef`に明示された参照とplatform設定、`.asmref`のJSONとtarget整合性、およびassembly owner候補のpath配置です。C# sourceが実際に参照先の型を使用しているか、scriptがactual compileでどのAssemblyへ入るか、参照を削除してもcompileできるか、compile時間へどの程度影響するかは判断しません。precompiled plugin、型単位とAsset単位の依存も検査対象外です。

Asset Databaseの型検索と`Assets`・登録済み`Packages`の物理rootを和集合にし、未importまたは不正な`.asmref`も見落とさないようにします。dot始まり、末尾`~`、`cvs`、Hidden属性、reparse pointのdirectoryは物理探索で降りず、Unityが無視するfile名も候補へ含めません。Asset Databaseがreparse point配下のassembly assetを認識した場合は、そのtyped assetを黙って除外せず、安全に読めないことを明示してRefresh全体を停止します。directory数、file entry数、`.asmdef`／`.asmref`件数、1 fileのbyte数、asmdef phaseとasmref phaseそれぞれの読取総量、問題数には安全上限があります。1件でも読めない場合や上限を超えた場合は監査全体を停止し、部分結果を表示しません。

宣言参照の表示は既存の解析結果だけを読み、Analyzer、model、依存graph、issue taxonomyを変更しません。公開API、Runtime assembly、build callbackも追加しません。

## 公開APIと依存

公開C# APIとRuntime assemblyはありません。外部Packageへの依存もありません。
