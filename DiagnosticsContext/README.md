# 不具合レポート保存（DiagnosticsContext）

## 30秒で分かる

Diagnostics Contextは、障害調査に必要な小さなcontext、時系列breadcrumb、実行中のUnity Warning・Error・Assert・Exceptionを明示的なownerの寿命内だけ有界に保持し、利用者が要求したときだけJSON reportとして保存します。通常の例外処理やクラッシュ収集の代替ではなく、「問い合わせ時に現在までの手掛かりを書き出す」ための補助モジュールです。

「何をしていたときに起きたか」を利用者へ聞くだけでなく、ゲーム側が明示した状態と直近の出来事を一つの JSON に残せます。

## こんなときに使う

- 再現しにくい不具合の直前操作を breadcrumb として残したい。
- Scene、mode、選択中 ID など、調査に必要な値だけを context へ追加したい。
- 利用者の明示操作で保存し、内容を確認してから共有してもらいたい。

動作確認対象: **Unity 6000.5.7f1** / Windows / .NET Standard 2.1

Runtime APIはUI Toolkitへ依存しません。パッケージは同梱Basics sampleを表示するため、Unity組込みのUI Toolkit moduleだけを宣言します。第三者package、global singleton、自動生成GameObject、network uploadはありません。

## インストール

Package Managerの **Add package from git URL** に固定タグ付きURLを指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/DiagnosticsContext#diagnostics-context-v1.0.0
```

利用側にasmdefがある場合は `DiagnosticsContext.Runtime` を参照します。フォルダーを直接管理する場合だけ、`DiagnosticsContext/`を`Assets/Modules/DiagnosticsContext/`へ配置してください。

## 所有とプライバシー

診断情報を必要とするSceneまたはアプリケーション進行ownerが、`DiagnosticsContextService.TryCreate`でServiceを明示作成して保持します。Serviceは作成から`Dispose`までUnity log callbackを購読し、終了時に購読と保留中の状態を終えます。global singletonや自動生成GameObjectは作りません。Sceneをまたぐ寿命が必要なら、利用側がServiceを保持するownerの寿命を管理します。

contextとbreadcrumbは、利用側がAPIへ渡した値だけを保持します。端末名、利用者名、hardware識別子、IP address、位置情報、アカウント情報を独自fieldとして自動追加しません。ただし、利用側が渡す文字列やUnityのWarning・Error・Assert・Exception本文とstack情報には、project path、OS user名、token、個人情報、秘密情報が元から含まれる可能性があります。導入側がlogging内容、同意、共有前の確認、保管期間、削除を管理してください。

## 基本動作

- contextは現在状態を表すkeyとvalueとして、breadcrumbは時系列の短い出来事として利用側が明示追加します。
- UnityのWarning、Error、Assert、ExceptionはServiceが有効な間のlog callbackから取得します。通常のLogは自動取得しません。
- 各集合と各文字列には上限があり、長時間実行でも無制限に増えません。
- reportは明示的な書出し要求が成功した場合だけ作成します。対象log payloadの取得だけではfileを作りません。
- reportは`Application.persistentDataPath`配下の専用diagnostics directoryへ保存します。利用側が渡すreasonはJSON本文にだけ入り、directory名やfile名には使いません。
- JSONは同じdirectoryの一時fileへ書いてstreamをflushした後、未使用の最終file名へ移動します。成功時に一時fileを残しませんが、電源断に対する永続性までは保証しません。
- 書出し結果と保存pathは戻り値または状態から確認し、失敗を通常のアプリケーション制御として扱います。

公開メソッド、件数property、結果型の完全な一覧は [Documentation~/index.md](Documentation~/index.md) を参照してください。

## reportの性質

Diagnostics Contextは、実行中の明示操作で取得できるbest-effortのsnapshotです。process crash、強制終了、OS終了、保存先障害の後にも必ずreportが残ることは保証しません。reportを自動送信せず、remote endpoint、認証、再送queue、暗号化、圧縮も提供しません。利用者へreportを共有してもらう導線、同意、保管期間、削除、送信時の保護は導入側の責務です。

## v1の境界

- unhandled exceptionやnative crashの捕捉、crash dump生成を行いません。
- 通常のLogは自動取得しません。
- screenshot、save data、Scene object、stack全体を自動収集しません。
- device、user、hardware、network、locationの識別情報を自動収集しません。
- reportのupload、共有UI、暗号化、圧縮、retention削除を行いません。
- global singleton、常駐Manager、自動生成GameObjectを作りません。
- `Debug.unityLogger`の設定や既存log handlerを変更しません。

## サンプル

Package Managerから **Diagnostics Context Basics** をImportし、同梱Sceneを開いてPlayします。**Add Context**、**Add Breadcrumb**、**Emit Warning**で現在の有界状態を作り、**Write Report**で手動reportを保存します。**Dispose / Recreate**は明示ownerの寿命を確認します。画面には件数、直近の結果、保存path、プライバシー境界を表示します。

利用条件は [LICENSE.md](LICENSE.md)、同梱物と外部依存は [Third-Party Notices.txt](Third-Party%20Notices.txt) を参照してください。
