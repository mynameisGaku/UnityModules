# Diagnostics Context

Diagnostics Contextは、利用側が明示した小さなcontext、時系列breadcrumb、owner寿命中のUnity Warning・Error・Assert・Exceptionを有界に保持し、手動要求時だけJSON reportへ保存する診断補助モジュールです。導入の最短手順はパッケージ直下の [README](../README.md) を参照してください。

動作確認対象: **Unity 6000.5.7f1** / Windows / .NET Standard 2.1

## 導入

Package Managerの **Add package from git URL** に次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/DiagnosticsContext#diagnostics-context-v1.0.0
```

利用側のasmdefから `DiagnosticsContext.Runtime` を参照します。パッケージは同梱Basics sampleのため、Unity組込みの`com.unity.modules.uielements` 1.0.0を宣言します。第三者packageへの依存はなく、Runtime APIはUI Toolkit型を公開しません。

## 所有と寿命

診断contextを必要とするSceneまたはアプリケーション進行ownerが、`DiagnosticsContextService.TryCreate`でServiceを明示作成して保持します。Serviceは作成から`Dispose`までUnity log callbackを購読します。終了時は購読を解除し、内部状態を終えます。

このモジュールはglobal singletonや自動生成GameObjectを作りません。Sceneをまたぐ場合は、利用側がServiceを持つGameObjectの寿命を管理します。破棄済みServiceを再利用せず、新しい所有期間には新しいService instanceを使います。

## データとプライバシー

contextとbreadcrumbは、利用側が明示的にAPIへ渡した文字列だけを保持します。Diagnostics Context自身は次を自動収集しません。

- device名、hardware識別子、OS利用者名、account識別子。
- IP address、network識別子、位置情報。
- screenshot、save data、Scene hierarchy、任意file。
- 通常のLog。

UnityのWarning、Error、Assert、Exceptionは例外です。Serviceが有効な間にUnity log callbackへ届いた本文とstack情報を有界に取得します。log payloadにはproject path、OS user名、token、個人情報、秘密情報など、導入側または第三者codeが元から書いた情報が含まれ得ます。moduleが識別fieldを追加しないことは、reportに識別可能情報が絶対に含まれないという意味ではありません。製品のlogging内容、同意、共有前の確認、保管期間、削除を導入側で管理してください。取得だけではreport fileを作成しません。通常のLogは取得しません。

## 公開API

公開型は次の3つです。

| 型 | 役割 |
|---|---|
| `DiagnosticsContextService` | context、breadcrumb、Unity log payloadの有界保持、report snapshot、owner寿命 |
| `DiagnosticsWriteResult` | 書出し成功、error、保存path、UTF-8 byte数を返す値 |
| `DiagnosticsError` | 入力、thread、寿命、storage、size、writeの失敗理由 |

Serviceの基本的な利用順序は次のとおりです。

1. `DiagnosticsContextService.TryCreate`でServiceを作成し、Scene ownerが保持する。
2. 利用側が現在状態のcontextと、調査に必要なbreadcrumbを明示追加する。
3. 必要な時だけreasonを添えてreport書出しを要求する。
4. 結果を確認し、成功した場合だけ返されたpathを利用者へ案内する。
5. ownerの終了時にServiceの`Dispose`を呼ぶ。

`TryCreate(out service, out error)`と`WriteReport(reason)`はUnityのメインスレッドから呼びます。`SetContext(key, value)`、`RemoveContext(key)`、`AddBreadcrumb(message)`は`DiagnosticsError`を返し、別threadからも呼べます。`Dispose`は繰り返し呼べます。

件数は`ContextEntryCount`、`BreadcrumbCount`、`CapturedLogCount`から読めます。上限によって追い出した件数は`DroppedBreadcrumbCount`と`DroppedLogCount`へ累積します。直近の`DiagnosticsWriteResult`を保持する責任は呼出し側にあり、Serviceは自動保存や再送queueを持ちません。

`DiagnosticsError`の意味は次のとおりです。

| 値 | 意味 |
|---|---|
| `None` | 失敗なし |
| `InvalidInput` | keyがnull・空白・不正Unicode・上限超過、valueがnull、またはbreadcrumb・reasonがnull・空白だけ |
| `ContextCapacityExceeded` | 新しいcontext keyを追加できる固定容量へ達した |
| `Disposed` | 終了済みServiceを操作した |
| `MainThreadRequired` | 作成または書出しをUnityのメインスレッド以外から呼んだ |
| `StorageUnavailable` | `persistentDataPath`または専用directoryを利用できない |
| `ReportTooLarge` | UTF-8 JSONが524,288 byte上限を超える |
| `WriteFailed` | 一時fileの作成、flush、移動などの保存処理に失敗した |

`DiagnosticsWriteResult`の`Succeeded`は書出し成功、`Error`は失敗理由、`ReportPath`は成功した最終`.json`のpath、`ReportByteCount`は保存したUTF-8 byte数です。失敗時のpathは利用しません。

## 有界保持

contextは32件、breadcrumbは64件、captured logは32件まで保持します。keyは64、valueは256、breadcrumbは512、log messageは1024、stack traceは2048、reasonは256 Unicode scalarが上限です。keyは空白だけ、null、不正Unicode、上限超過を`InvalidInput`で拒否します。context valueはnullだけを拒否し、空文字列と空白だけの文字列を明示値として許可します。長いvalueと不正surrogateは、Unicode scalarを壊さない有効文字列へ整えて上限内へ収めます。breadcrumbとreasonは空白だけなら拒否し、それ以外は同じ規則で整えて切り詰めます。log callbackのpayloadも同じ境界で切り詰めます。context容量超過は`ContextCapacityExceeded`です。breadcrumbとcaptured logが件数上限へ達した後は古い項目を追い出し、drop件数を記録します。JSON全体はUTF-8で524,288 byte以下です。

reportの`schemaVersion`は整数`1`です。`createdUtc`はUTC時刻をInvariant Cultureのround-trip `O` formatで表す文字列です。top-level fieldは`schemaVersion`、`createdUtc`、`reason`、`droppedBreadcrumbCount`、`droppedLogCount`、`context`、`breadcrumbs`、`logs`の順です。contextはkeyのordinal順、breadcrumbとlogはsequence昇順です。各context itemは`key`、`value`、breadcrumb itemは`sequence`、`message`、log itemは`sequence`、`type`、`message`、`stackTrace`の順です。同じsnapshotからは、生成時刻や一意file名など明示的に変化するfieldを除き、同じ論理内容を得られます。

## 保存境界

reportは`Application.persistentDataPath`配下の専用diagnostics directoryへだけ保存します。利用側が渡したreasonやcontext値はJSON本文だけへ入り、directory名またはfile名へ連結しません。Runtimeは保存先を正規化し、専用directoryから逸脱するpathを成功として返しません。

書出しは同じdirectory内の一時fileを使い、streamをflushして閉じた後、未使用の一意な最終`.json`名へ上書きなしで移動します。成功時に一時fileを残しません。これはprocess内で途中JSONを最終reportとして見せないための境界であり、電源断後の永続性を保証するものではありません。失敗時は結果を返し、可能な範囲で今回の一時fileだけを除去します。ほかのreportや利用側fileを削除しません。

保存pathは実行環境のlocal pathであり、report JSON本文には自己参照として含めません。画面表示や利用者への案内にpathを使う場合も、reportへcontextとして再追加しないでください。

## 手動reportの限界

Diagnostics Contextのreportは、処理が動作している最中に明示操作で保存するbest-effort snapshotです。次を保証しません。

- unhandled exception、native crash、強制終了、電源断の後にもreportが存在すること。
- OSや保存媒体の障害時にも書出しが完了すること。
- crash直前の全状態、全thread、全stackを再現できること。
- reportの自動upload、再送、remote保管、暗号化、圧縮、削除。

crash survivalが必要な製品では、対象platform向けcrash reporterを別途選定してください。Diagnostics Contextをその補足情報として連携する場合も、明示的な同意、privacy policy、秘密情報の除去、保管期間を導入側で設計します。

## threadとlog callback

Unityのlog callbackはworker threadから届く場合があります。log取得はcallback thread上でUnity objectやUIを操作せず、thread-safeな有界領域へ直接記録します。後続のメインスレッド`WriteReport`は、その呼出し開始までに記録済みのlogを同じlock内でsnapshotへ含めるため、別の`Update`を待つ必要はありません。callbackと同時進行中で、まだ記録が完了していないlogは次回snapshotの対象です。

context追加、削除、breadcrumb追加はthread-safeです。Serviceの作成とreport書出しはUnityのメインスレッドから呼びます。

## 非目標

- crash dump、unhandled exception handler、クラッシュ後の生存保証。
- report upload、remote endpoint、認証、retry queue。
- device、user、hardware、network、locationの自動識別。
- screenshot、save data、Scene objectの自動取得。
- file暗号化、圧縮、retention policy、共有UI。
- global singleton、常駐Manager、自動生成GameObject。
- 既存logger設定またはlog handlerの置換。

## 検証

EditModeテストは入力上限、Unicode、決定論的JSON、path検証、保存失敗を確認します。PlayModeテストはowner寿命、対象log payloadの購読と解除、worker Warning、report書出しを実環境で確認します。

**Diagnostics Context Basics** のimport済みsampleテストは実Button callbackを通してcontext、breadcrumb、warningを追加し、report JSONのparse、保存先の封じ込め、reasonがfile名へ入らないこと、一時file不在、Dispose / Recreateを確認します。表示検証はPanelSettingsの実RenderTextureを960x600と640x360へ切り替え、card内のtextと全操作Buttonが互いに重ならず見えることをresolved `worldBound`で確認します。
