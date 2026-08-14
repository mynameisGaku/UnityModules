# Diagnostics Context Basics

明示的なcontextとbreadcrumb、Serviceが有効な間のUnity Warningを有界に保持し、手動操作でJSON reportへ保存するUI Toolkitサンプルです。Runtime本体はError、Assert、Exceptionも取得しますが、このsampleは安全に再生できるWarningでlive subscriptionを示します。

## 開き方

1. Package Managerから **Diagnostics Context Basics** をImportします。
2. `DiagnosticsContextBasics.unity`を開きます。
3. Play Modeを開始します。
4. Game Viewの **Add Context**、**Add Breadcrumb**、**Emit Warning**、**Write Report**、**Dispose / Recreate**を操作します。

## 操作

- **Add Context** はsampleが用意した明示的なkeyとvalueを追加します。
- **Add Breadcrumb** は連番付きの短い出来事を追加します。
- **Emit Warning** はUnity warningを1件発生させ、live subscriptionによる取得を確認します。
- **Write Report** は、その時点のsnapshotを手動でJSONへ保存します。対象log payloadの取得だけでは保存しません。
- **Dispose / Recreate** はService ownerを明示的に終了し、新しい所有期間を作ります。

## 画面の読み方

- StatusはServiceの利用可否とcontext、breadcrumb、captured logの件数を表示します。sample自身はWarningだけを発生させますが、件数には外部から届いたError、Assert、Exceptionも含まれます。
- Last Resultは直近操作または書出し結果を表示します。
- Report Pathは直近に成功した保存pathを表示します。このpathはreport JSON本文へ自動追加されません。
- Privacy境界は、自動識別情報収集を行わず、reportが手動生成のみであることを表示します。

このsampleはクラッシュ後にもreportが残ることを保証せず、reportをuploadしません。contextへ個人情報や秘密情報を渡さない判断に加え、UnityのWarning・Error・Assert・Exception本文とstack情報に含まれ得るproject path、OS user名、token、個人情報を確認してください。logging方針、共有導線、同意、保管期間、削除は導入側で管理します。

## 構成

- `DiagnosticsContextBasicsPanelSettings.asset`がUI Toolkit panelの描画設定を所有します。
- Sceneの同じGameObjectに`UIDocument`と`DiagnosticsContextBasicsController`を配置します。
- Controllerが`DiagnosticsContextService`を明示生成し、画面と同じ寿命で終了します。
- Runtime APIはUI Toolkit型を公開しません。

SampleのImportだけではProject Settings、Build Profile、開いているSceneを変更しません。Legacy Input API、UXML、外部画像、外部fontは使用しません。
