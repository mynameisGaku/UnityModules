# Assembly依存チェック（Assembly Dependency Audit）

## 30秒で分かる説明

`Assets`と`Packages`にあるAssembly Definition（`.asmdef`）の参照関係を3列でたどり、選択した`.asmdef`では宣言順と重複を保ったName／GUID参照の解決詳細も確認できます。循環参照やPlayer向けAssemblyからEditor専用Assemblyへの参照を見つけ、Assembly Definition Reference（`.asmref`）は別一覧でtargetの整合性を検査し、同じfolderに複数ある`.asmdef`／`.asmref` owner候補も報告します。Projectのfileは変更せず、現在の構成を読み取って表示します。

## できること

- `Assets`と`Packages`から見つかった`.asmdef`を決定論的なpath順で検査する。
- `Referenced By`、`Assemblies`、`Depends On`の3列で、選択Assemblyの参照元と参照先を同時に確認する。
- 選択した`.asmdef`の宣言参照を元の順序と重複のまま表示し、Name／GUID、raw declaration、一意に解決したAssembly名とpathを確認する。
- 検索と問題filterで、大きなProjectの対象を絞り込む。
- 循環参照と自己参照を報告する。
- Playerで使えるAssemblyからEditor専用Assemblyへの参照を報告する。
- 不正なJSON、空のAssembly名、重複したAssembly名またはGUIDを報告する。
- 解決できない参照と、同名候補が複数ある曖昧な参照を報告する。
- 1つの`.asmdef`内で名前参照と`GUID:`参照が混在している状態を報告する。
- `includePlatforms`と`excludePlatforms`が同時に指定されている状態を報告する。
- `.asmref`を別一覧へ表示し、不正なJSON、空の`reference`、未解決target、曖昧なtargetを報告する。
- 同じfolderに複数ある`.asmdef`／`.asmref`を、JSONやtargetの有効性に関係なくowner候補の配置競合として各assetへ報告する。
- 別folderにある複数の`.asmref`が同じtargetを指す正当な構成を保持し、`.asmref`から依存graphへ推測したedgeを追加しない。

## 使わない方がよい場合

問題を自動修正したい場合、使われていないAssembly参照を自動判定したい場合、compile時間を推定したい場合には向きません。このツールは`.asmdef`に明示された構造を読み取り、修正前の判断材料を提示する用途へ限定しています。

## 3分で試す

1. Package Managerの`Add package from git URL...`へ次を入力します。

   ```text
   https://github.com/mynameisGaku/UnityModules.git?path=/AssemblyDependencyAudit#assembly-dependency-audit-v1.3.0
   ```

2. `Tools > Assembly Dependency Audit > Open`を開きます。
3. `Refresh`を押して、`Assets`と`Packages`のAssembly Definitionを読み取ります。
4. 中央の`Assemblies`列からAssemblyを選びます。
5. 左の`Referenced By`で参照元、右の`Depends On`で直接の参照先を確認します。
6. `Details`の`Declared References`で、選択した`.asmdef`が記録している個々のName／GUID参照と解決先を確認します。
7. `Assembly References`で`.asmref`の元path、reference、解決先を確認します。
8. 問題がある場合は問題filterで絞り、表示されたpathと内容を確認してから元の`.asmdef`または`.asmref`を編集します。

## 実行するとどうなるか

Refresh結果はasset pathのOrdinal順、同一pathではGUIDのOrdinal順で安定して表示されます。中央で選択したAssemblyに対して、直接参照しているAssemblyを左列、直接参照するAssemblyを右列へ表示します。このgraphは同じ解決先へのedgeを1本へまとめますが、`Declared References`は`.asmdef`に記録された順序と重複をそのまま保ち、Name／GUIDのraw declarationと一意な解決先を個別に表示します。循環参照は循環へ含まれるAssemblyを問題としてまとめ、その他の構成不備は該当する`.asmdef`と参照値を示します。`.asmref`は独立した一覧へ元path、referenceの指定方法、解決先を表示し、不正・未解決・曖昧な項目も選択して詳細を確認できます。同じfolderのowner候補は各`.asmdef`／`.asmref`から問題詳細へ到達できます。

`Declared References`は500件ごとの`Prev`／`Next`で、1 Assemblyあたりの既存上限4,096件まで全pageへ移動できます。解決結果が`Not uniquely resolved`の場合、その行だけでは未解決と曖昧を決めず、対応する`Issue Details`で区別します。null reference、未知のkind、範囲外indexなど内部resultの不整合も別の参照へ推測せず、invalid rowとして表示します。`.asmref`を選択した場合、直前の`.asmdef`の宣言参照は表示しません。

長いpath、reference、messageはEditorの描画負荷を抑えるため画面上だけ省略します。宣言参照rowのraw valueとtargetはそれぞれ160文字までをsurrogate-safeに表示し、全文は`Open`で元の`.asmdef`を確認します。既存の`Copy Reference`と`Copy Issue`は省略前の全文をclipboardへ入れ、その内容は変更しません。

Refreshはread-onlyです。`.asmdef`、`.asmref`、script、Scene、Prefab、Project Settings、Package manifestを変更せず、Assetのimportやcompileも要求しません。

## よくある問題

### 参照先が見つからない

名前参照の綴り、または`GUID:`に続く値を確認してください。Packageを削除した後の参照が残っている場合もあります。本ツールは推測で別のAssemblyへ結び替えません。

### 同じAssembly名が複数表示される

Assembly名は大小文字を区別せずProject内で一意である必要があります。同名候補がある間、名前参照の解決先を決めず、重複と曖昧な参照を別々に報告します。pathを確認して名前を整理した後、Refreshし直してください。

### PlayerからEditor専用Assemblyへの参照になる

参照元のplatform設定、参照先の`includePlatforms` / `excludePlatforms`、またはAssemblyの責務境界を確認してください。このツールはPlayer buildへ入るべき参照先を自動選択しません。

### 問題がない参照も表示される

3列viewは問題の有無に関係なく一意な直接依存を表示します。`Declared References`はgraphで1本にまとめられる同じ解決先への重複宣言も別々に表示します。問題だけを確認する場合は問題filterを使ってください。

### `.asmref`のtargetが見つからない、または曖昧になる

`reference`がAssembly名なら同名の`.asmdef`、`GUID:`形式なら32桁GUIDと対応する`.asmdef`を確認してください。対応する`.asmdef`のJSONとAssembly名が有効でない場合も、判明したpathを詳細へ残して未解決として報告します。同名または同じGUIDの候補が複数ある場合や、監査対象の`.asmref`自身とasmdefのGUIDが衝突する場合は、どれか1件へ決め打ちせず曖昧として報告します。別folderにある複数の`.asmref`が同じ一意なasmdefを指すこと自体は問題ではありません。

### 同じfolderに複数の`.asmdef`または`.asmref`がある

同じfolder以下のscript所属を指定するassembly assetは1件にしてください。この検査は、同じ正規化済みparent folderにある`.asmdef`／`.asmref`をowner候補として数え、targetが同じ場合も各assetへ問題を報告します。不正JSONや未importの物理fileも候補へ含めますが、現在どのAssemblyへcompileされたかは断定しません。子folderまたは別folderにあるowner候補は競合しません。

## 公開API

公開C# APIはありません。`AssemblyDependencyAudit.Editor`はEditorWindowと内部の検査処理だけを提供し、Runtime assemblyは追加しません。

## 変更範囲と失敗条件

- 読み取り対象は、UnityのAsset Databaseが認識した`.asmdef`／`.asmref`と、`Assets`および導入済み`Packages`の物理rootから見つかった同fileの和集合です。dot始まり、末尾`~`、`cvs`、Hidden属性、reparse pointのdirectoryは物理列挙で降りず、Unityが無視するfile名も候補へ含めません。Asset Databaseがreparse point配下のassembly assetを認識した場合は、そのtyped assetを黙って除外せず、安全に読めないことを明示してRefresh全体を停止します。
- `.asmref`はtarget整合性だけを検査します。precompiled plugin、C# sourceの型利用、AddressablesやAsset参照と同様、asmdef依存graphへは含めません。
- 選択`.asmdef`の宣言参照は最大500件ずつpage表示し、1 Assemblyあたりの解析上限4,096件まで到達できます。宣言順と重複は表示でも維持し、graphだけが一意な解決先ごとにedgeをまとめます。
- 同じfolderのowner候補はJSON、Assembly名、reference、targetの有効性に関係なくpath単位で報告し、不正JSONなどの問題と併記します。この配置検査だけでactual compile所属は判断しません。
- RefreshはProject fileとUnity設定を変更しません。問題を直すには、表示内容を確認した利用者が対象`.asmdef`または`.asmref`を編集する必要があります。
- 1件でもfileを読み取れない場合や、探索・件数・file size・asmdef／asmref各phaseの総読取量という安全上限を超えた場合はRefresh全体を失敗として部分結果を破棄します。読み取れた不正JSONは問題として残し、推測した内容でgraphを補いません。
- 重複名で名前参照が曖昧な場合は、候補のどれかへ勝手に接続しません。
- Package Cacheなど書き込みが想定されない場所の`.asmdef`と`.asmref`も表示しますが、編集可否はPackageの導入方法に依存します。

## 非目標

自動修正、`.asmdef`／`.asmref`の生成・削除・書き換え、unused参照判定、compile時間・incremental build効果の推定、scriptのactual compile所属確認、型単位やAsset単位の依存解析、Player buildの自動実行、graph画像のexportは扱いません。Sampleは同梱しません。
