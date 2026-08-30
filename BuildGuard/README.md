# プロジェクト不備確認・修復（Build Guard）

## 30秒で分かる

Player buildに使うScene、Project windowで直接選択した保存済みScene、選択Prefabを調べ、壊れた参照を一覧から開けるEditor専用moduleです。build対象SceneのPrefab structural overrideは、build停止とは切り離した別windowで確認できます。

- 削除したC# scriptがComponentとして残った **Missing Script**
- 削除したTexture、Material、Prefabなどを指したままの **Missing Object Reference**
- Prefab instanceへ追加・削除したGameObjectまたはComponentの **Structural Override**

build対象Sceneは **Tools > Build Guard > Scan Build Scenes**、選択Sceneは **Assets > Build Guard > Scan Selected Scenes**、選択Prefabは **Assets > Build Guard > Scan Selected Prefabs** から検査します。structural overrideは **Tools > Build Guard > Review Prefab Overrides** で確認します。Missing Script／Missing Object ReferenceだけがPlayer buildを停止し、structural overrideはreview結果に留まります。どのflowも自動保存しません。

## こんな面倒を減らす

- SceneやPrefabを整理した後、壊れた参照を探してHierarchyとInspectorを手作業で巡回する。
- inactive GameObjectに残ったMissing Scriptを見落とし、長いbuildの後で気付く。
- Consoleのmessageだけを頼りに、どのComponentのどのfieldかを探し直す。
- Missing Scriptを見つけた後、InspectorのComponent menuを一つずつ開いて除去する。
- CIで壊れたSceneを早く止めつつ、修復場所が分かる記録を残す。
- Build Profileへ未登録、または無効にした作業中Sceneだけをbuild設定を変えずに確認する。
- PrefabをSceneへ配置する前に、inactive階層も含めて壊れたComponentを確認する。
- Prefab instanceへ加わった構造変更だけを、property値の差分と混ぜずにScene横断で確認する。

## 3分で使う

### 1. インストール

Unity 6000.5.7f1以降で、Package Managerの **Add package from git URL...** へ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/BuildGuard#build-guard-v1.6.0
```

または`BuildGuard` folderをprojectの`Assets/Modules/`へ配置します。追加packageへの依存はありません。設定assetの作成も不要です。

### 2. build前に一覧で確認する

1. Build ProfilesのScene一覧を設定します。
2. **Tools > Build Guard > Scan Build Scenes** を開きます。
3. `Scan Build Scenes`を押します。
4. Missing Scriptなら`Open and Remove`を押し、確認dialogの後で対象Sceneを開いて除去します。
5. Sceneは未保存のままなので、InspectorとHierarchyを確認してから保存するかUndoで戻します。
6. Missing Object Referenceは`Open Scene`を押し、選択されたComponentのfieldへ正しいAssetを手動設定します。
7. もう一度scanし、`No missing references found.`になることを確認します。

`Copy`はScene path、GameObject階層、Component/fieldを一行でclipboardへコピーします。scan中にCancelしても、完了済みSceneの結果は残ります。

### 3. 選択Sceneを確認する

1. Project windowで`Assets/`配下の保存済みScene Assetを1個以上、直接選択します。
2. 右クリックして **Build Guard > Scan Selected Scenes** を選びます。
3. `Selected Scene Assets`の件数を確認し、`Scan Selected Scenes`を押します。
4. 一覧から`Open Scene`、`Open and Remove`、`Copy`を既存のbuild Scene scanと同じように使います。

Assets menuはwindowを開いて現在の選択をcaptureします。既にwindowを開いている場合は`Use Current Selection`でも更新できます。対象は直接選択した保存済みSceneだけで、folderを再帰しません。`Packages/`、Scene以外のAsset、Hierarchy上のGameObjectは除外します。Build Profileへ未登録または無効なSceneも検査できます。

一度に扱える上限は選択asset候補4,096件、Scene 256件です。Sceneはpath順へ並べて重複を除き、capture時点のsnapshotとして保持します。capture後にSceneを移動・削除した場合はpartial resultを返さず、`Use Current Selection`での再選択を案内します。途中でCancelした場合は完了済みSceneの結果を残します。

### 4. 選択Prefabを確認する

1. Project windowで1個以上のPrefab Asset、またはPrefabを含むfolderを選択します。
2. 右クリックして **Build Guard > Scan Selected Prefabs** を選びます。
3. `Scan Selected Prefabs`を押します。
4. Missing Scriptなら`Open and Remove`を押します。
5. Prefab Modeで対象GameObjectと変更内容を確認し、保存するかUndoで戻します。
6. Missing Object Referenceは`Open Prefab`で対象へ移動し、正しい参照を手動設定します。

folderを選択した場合は、そのfolder以下のPrefabをまとめて対象にします。scanはPrefabを一時的に読み込んで閉じるため、scanだけではPrefabを変更しません。複数選択時はpath順に検査し、途中でCancelしても完了済みPrefabの結果を残します。

### 5. Prefabの構造変更をreviewする

1. **Tools > Build Guard > Review Prefab Overrides** を開きます。
2. `Refresh / Scan`を押します。
3. kind、Scene、Prefab instance、対象path、Component、sourceを確認します。
4. `Open & Select`を押すと、findingを再scanして現在も同じ場合だけ安全なnavigationを行います。

対象はactive Build Profileで有効なSceneにある、outermostかつconnectedなPrefab instanceです。Added／Removed GameObjectとAdded／Removed Componentだけを扱い、Transformを含むProperty Modificationは表示しません。結果はpath順に安定化したsnapshotで、最大1,000件を表示します。CancelまたはScene scan failureでは途中結果を破棄するため、partial resultを完全なreview結果として見せません。

対象Sceneが既に開いていれば現在のGameObjectを選択します。閉じていれば一時的にadditiveで再scanした後、Sceneを必ず閉じ直してScene assetを選択します。findingが変化していればstaleと案内します。Apply、Revert、Undo登録、Scene保存、dirty化は行わず、review結果はPlayer buildを止めません。

### 6. いつもどおりビルドする

プレイヤービルド時は操作不要です。ビルドへ実際に渡されたシーンを同じ規則で再検査します。問題がなければ何も出力せず続行し、問題があれば次のような診断文を示して開始前に止めます。

```text
プレイヤービルド対象のシーンで、ビルドを停止する問題が見つかりました。
シーン: Assets/Scenes/Game.unity
欠落スクリプト: 1
- Root[0]/Inactive Child[1]: 1
欠落オブジェクト参照: 1
- Camera Root[1] :: UnityEngine.Camera[1].m_TargetTexture
再度ビルドする前に、一覧の欠落スクリプトまたはオブジェクト参照を修復するか、該当箇所を削除してください。
```

`Root[0]`などの数字は同じ親の中での並び順です。`UnityEngine.Camera[1].m_TargetTexture`は、対象ゲームオブジェクトの2番目の部品にある`m_TargetTexture`プロパティが壊れていることを表します。

## 検査する範囲

- active Build Profileで有効なScene（手動scan）
- Project windowで直接選択した`Assets/`配下の保存済みScene Asset（手動scan、Build Profileへの登録・有効状態は不問）
- Player buildへ実際に渡されたScene（自動検査）
- active・inactiveを問わない全GameObject
- Scene内のPrefab instance
- UnityがSceneへ保存したObject Reference field
- build開始前の元Sceneと、Unityがbuild中に処理する一時Scene
- Project windowで利用者が明示選択したPrefab Asset（手動scan）
- active Build Profileで有効なSceneのoutermost connected Prefab instance（structural override review）
- Added／Removed GameObject・Component（property値のoverrideは除外）

既に読み込まれているSceneはcurrent in-memory状態を検査するため、未保存の変更も対象です。閉じたSceneは一時的にadditiveで開き、Prefabは`LoadPrefabContents`で一時展開し、scan後に保存せず閉じます。scanだけでは元のactive Scene、開閉状態、dirty状態を維持し、修復、保存、削除を行いません。structural override findingのnavigationも再scan後に元のScene開閉状態を復元します。`Open and Remove`を利用者が明示実行した場合だけSceneまたはPrefab Modeへ移動し、そのGameObjectのMissing ScriptをUndoへ記録して除去します。SceneとPrefabは自動保存しません。

## 対象外

このmoduleはbuild対象Scene、直接選択した保存済みScene、明示選択したPrefab内の壊れたComponent参照、およびbuild対象SceneのPrefab構造変更reviewだけを扱います。project全体やfolder単位のScene常時scan、未使用asset検索、Runtimeで後から代入するnull fieldの必須判定、AddressablesやResources内assetの一括検査、PlayerSettings policy、test実行は行いません。structural overrideのProperty Modification、Apply／Revert、自動修復、Player build停止も対象外です。Missing Object Referenceの置換、Scene・Prefabの自動保存、複数Assetの一括修復もしません。Runtime assemblyとPlayerへ含まれるcodeもありません。

Assetを削除する前に利用箇所を探し、既存参照をまとめて切り替える用途は、別moduleの **アセット整理・参照管理（Reference Finder）** が担当します。Build Guardは、削除後などに壊れたScene・Prefabを見つけ、Missing Scriptだけを確認付きで除去する用途へ絞っています。

## サンプル

Package ManagerのSamplesから **Build Guard Basics** をImportしてください。

- `BuildGuardBasics.unity`: そのままscan・buildできるScene
- `BrokenSceneExample.unity.txt`: Missing Scriptの失敗を安全に試すtext template
- `BrokenPrefabExample.prefab.txt`: Prefab scanと修復を安全に試すtext template
- `Samples~/Basics/README.md`: build対象・選択SceneとPrefabの手動scan、自動build停止を確認する手順

共有branchへ壊れたSceneを残さないよう、失敗確認はscratch copyか使い捨てprojectで行ってください。
