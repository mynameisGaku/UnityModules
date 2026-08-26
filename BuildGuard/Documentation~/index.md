# Build Guard 1.6.0

Build Guardは、Player build対象Scene、Project windowで直接選択した保存済みScene、選択Prefabの壊れたComponent参照をscanし、build対象SceneのPrefab構造変更を別flowでreviewするEditor専用moduleです。Runtime assembly、設定asset、global singleton、公開APIを持ちません。

## 利用者の操作

### build Scene scan

**Tools > Build Guard > Scan Build Scenes** は、active Build Profileで有効なSceneを上から順に検査します。結果windowには次を表示します。

- 問題の種類
- Scene asset path
- 兄弟index付きGameObject階層path
- Missing Script件数、またはComponent型・順番・serialized property path

`Open Scene`は未保存Sceneの保存確認後に対象Sceneを開き、階層pathがまだ一致すればGameObjectを選択します。対象が既に修復・移動されている場合はScene assetを選択します。`Copy`は1件を一行でclipboardへコピーします。

Missing Scriptの`Open and Remove`は、確認後に同じ方法でSceneとGameObjectを特定します。対象GameObjectのmissing MonoBehaviour slotだけを`Undo.RegisterFullObjectHierarchyUndo`へ記録して除去し、Sceneをdirty状態のまま残します。自動保存しないため、利用者がInspectorを確認して保存するかUndoで戻せます。Missing Object Referenceは参照先を推測できないため自動修復しません。

### 選択Scene scan

**Assets > Build Guard > Scan Selected Scenes** は同じ結果windowを開き、Project windowで直接選択した`Assets/`配下の保存済みScene Assetをcaptureします。windowを開いた後は`Use Current Selection`でもcaptureを更新し、`Scan Selected Scenes`で検査します。Build Profileへの登録や有効状態は問いません。

対象は直接選択したSceneだけです。選択folder以下を再帰せず、`Packages/`、Scene以外のAsset、Hierarchy上のGameObjectを除外します。最大4,096件の選択asset候補から最大256件のSceneをpath順へ並べ、重複を除いたsnapshotとして保持します。

capture後にSceneが移動・削除されてpathを解決できなくなった場合は、scan結果を全て破棄して`Use Current Selection`による再選択を案内します。scan中にCancelした場合は完了済みSceneの結果を保持します。検出rule、結果表示、navigation、Missing Scriptの明示修復はbuild Scene scanと同じです。

### 選択Prefab scan

**Assets > Build Guard > Scan Selected Prefabs** は、Project windowで選択したPrefab、または選択folder以下のPrefabをpath順に検査します。各Prefabは`PrefabUtility.LoadPrefabContents`で一時展開し、Sceneと同じMissing Script／Missing Object Reference ruleを適用した後、保存せずUnloadします。

`Open Prefab`はPrefab Modeを開き、兄弟index付き階層pathがまだ一致すればGameObjectを選択します。Missing Scriptの`Open and Remove`は対象hierarchyをUndoへ記録してmissing slotだけを除去し、Prefab Stageをdirty状態のまま残します。利用者が保存またはUndoするまでPrefab assetへ確定しません。

### Prefab structural override review

**Tools > Build Guard > Review Prefab Overrides** は、active Build Profileで有効なSceneのoutermost connected Prefab instanceを検査します。Added／Removed GameObjectとAdded／Removed ComponentだけをScene、instance、path、Component、sourceの安定順で表示します。Transformを含むProperty Modificationは対象外です。

`Refresh / Scan`は最大1,000件のimmutable snapshotを作ります。Cancelまたは1 Sceneでもscan failureが発生した場合はpartial findingsを全て破棄します。これはMissing Script／Missing Object Referenceのbuild blockerとは接続されず、findingがあってもPlayer buildを停止しません。

`Open & Select`はScene GUIDとfinding identityを同じscannerで再確認します。loaded Sceneでは一致するGameObjectだけを選択します。closed Sceneはadditiveで一時確認して必ず閉じ直し、currentならScene assetを選択します。変更済みfindingはstaleと案内します。Apply、Revert、Undo、保存、dirty化は行いません。

### 自動build検査

同じruleを2つのPlayer build callbackから適用します。

1. `BuildPlayerProcessor.PrepareForBuild`で`BuildPlayerOptions.scenes`を取得し、build開始前に全Scene assetを検査します。
2. `IProcessSceneWithReport.OnProcessScene`でUnityが実際に処理する一時Sceneを検査します。

preflightはincremental buildがScene contentを再利用する場合にも毎回動作します。Scene callbackは他のbuild処理が一時Sceneへ加えた変更も検査します。

## 検出する不備

### Missing Script

Sceneのroot順と各Transformのchild順でactive・inactiveに関係なく全GameObjectを走査し、`GameObjectUtility.GetMonoBehavioursWithMissingScriptCount`が返す件数を収集します。

### Missing Object Reference

各Componentを`SerializedObject`として走査し、`ObjectReference`型で値を解決できない一方、保存された`EntityId`が有効なfieldを検出します。これにより、Scene保存後に削除されたTexture、Material、Prefabなどへの参照を検出できます。最初からnullの任意fieldは検出しません。

Prefab instanceも展開済みScene階層として同じ規則で扱います。選択Prefab scanも一時的なPrefab contents Sceneへ同じruleを適用します。結果はAsset path、階層path、Component順、property pathの順へ並べます。

### Structural Prefab Override

Sceneにあるoutermost connected Prefab instanceごとにUnityのadded／removed GameObject・Component APIを読み、追加・削除したsubtreeを1件へ正規化します。nested PrefabとVariantのsource identityを保持し、property override APIは読みません。結果はreview専用で、build callbackやMissing Script／Missing Object Referenceのinspectionへ渡しません。

## Sceneの扱い

- 既に読み込まれているSceneはcurrent in-memory状態をそのまま検査します。未保存の変更も対象にし、dirty状態を変えません。
- 閉じているSceneはadditiveで開き、検査後に保存せず閉じます。
- 検査前のactive Sceneが有効なら、終了時にactive状態を復元します。
- 同じScene pathが重複している場合は、大小文字を区別せず1回だけ検査します。
- 空pathやScene assetとして解決できないpathはUnity本体のbuild診断へ委ねます。
- scanはScene、Prefab instance、GameObject、Componentを変更しません。
- structural override navigationで一時openしたSceneも必ず閉じ、元のactive／open／dirty状態を維持します。

選択Scene scanはcapture済みpathを開始前に全件再検証します。staleなsnapshotでは走査を始めず、走査中の外部変更で全件完了できなかった場合もpartial resultを返しません。この追加は手動scanだけに閉じており、build callback、build Scene scan、選択Prefab scan、Missing Script修復、Prefab structural override reviewの対象と動作を変更しません。

GameObject名には兄弟indexを付けます。`/`、`\\`、改行、復帰、tabは一行で判別できるようescapeします。このpath生成と解決は手動windowと自動build検査で共通です。

## 対象外

`OnProcessScene`へ渡される`BuildReport`がnullのPlayMode読込と、`BuildPipeline.isBuildingPlayer`がfalseのAssetBundle buildは拒否しません。次も対象外です。

- Missing Object Referenceの自動修復
- Prefab structural overrideのProperty Modification表示、Apply、Revert、自動修復、build停止
- 複数Sceneの一括修復と自動保存
- project内の全Prefab・Scene・ScriptableObjectの常時scan（SceneとPrefabは利用者が明示選択した範囲だけ）
- Runtimeで後から設定するnull fieldの必須判定
- Addressables、Resources、AssetBundle contentの一括検査
- PlayerSettings、Development Build、Profiler、署名、version、secretのpolicy判定
- test、Package Validation Suite、version管理commandの自動実行
- custom rule登録や設定asset

## Build Guard Basics

Package ManagerからSampleをImportすると、次を確認できます。

- `BuildGuardBasics.unity`: active rootとinactive childを持つscan・build可能Scene
- `BrokenSceneExample.unity.txt`: Missing Scriptを1件含むtext template
- `BrokenPrefabExample.prefab.txt`: Missing Scriptを1件含むPrefab text template
- Sample README: Scene・Prefabの問題を手動scanとPlayer buildで確認する手順

失敗動作はscratch locationか使い捨てprojectで確認し、確認後は壊れたSceneを削除してください。

## 検証方針

Editor testはinactive階層、Prefab instance、削除済みRenderTexture、path escape、階層pathの逆引き、決定論的message、複数Sceneと選択Prefabの手動scan、cancel、結果window、Scene・Prefab移動、Missing Script除去とUndo、閉じたSceneとPrefab contentsの一時読込を検証します。選択SceneはProject選択filter、path順と重複除去、4,096候補／256 Scene上限、build対象外Scene、stale snapshotのpartial破棄、loaded Sceneの未保存状態、closed Sceneの一時読込、active／open／dirty状態保全を検証します。structural override reviewはnested／Variant／removed override、安定順、1,000件上限、cancel／failureのpartial破棄、stale identity、Scene open／active／dirty状態保全を検証します。配布gateではclean projectへtarballを導入し、有効Scene、build対象外の選択Scene、選択Prefabのscan、Missing Script除去後の未保存状態、正常Sceneのbuild成功、2種類の不備Sceneのbuild失敗を実際に確認します。
