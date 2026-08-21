# プロジェクト不備確認・修復（Build Guard）

## 30秒で分かる

Player buildに使うSceneと、Project windowで選択したPrefabを調べ、壊れた参照を一覧から開けるEditor専用moduleです。

- 削除したC# scriptがComponentとして残った **Missing Script**
- 削除したTexture、Material、Prefabなどを指したままの **Missing Object Reference**

build対象Sceneは **Tools > Build Guard > Scan Build Scenes**、PrefabはProject windowで選択して **Assets > Build Guard > Scan Selected Prefabs** を押します。問題があればAsset path、GameObject階層、Component、fieldを一覧表示します。Missing Scriptは`Open and Remove`でSceneまたはPrefab Modeを開き、Undo記録付きで除去できます。どちらも自動保存しません。SceneはPlayer build開始時にも自動検査されます。

## こんな面倒を減らす

- SceneやPrefabを整理した後、壊れた参照を探してHierarchyとInspectorを手作業で巡回する。
- inactive GameObjectに残ったMissing Scriptを見落とし、長いbuildの後で気付く。
- Consoleのmessageだけを頼りに、どのComponentのどのfieldかを探し直す。
- Missing Scriptを見つけた後、InspectorのComponent menuを一つずつ開いて除去する。
- CIで壊れたSceneを早く止めつつ、修復場所が分かる記録を残す。
- PrefabをSceneへ配置する前に、inactive階層も含めて壊れたComponentを確認する。

## 3分で使う

### 1. インストール

Unity 6000.5.7f1以降で、Package Managerの **Add package from git URL...** へ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/BuildGuard#build-guard-v1.4.0
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

### 3. 選択Prefabを確認する

1. Project windowで1個以上のPrefab Asset、またはPrefabを含むfolderを選択します。
2. 右クリックして **Build Guard > Scan Selected Prefabs** を選びます。
3. `Scan Selected Prefabs`を押します。
4. Missing Scriptなら`Open and Remove`を押します。
5. Prefab Modeで対象GameObjectと変更内容を確認し、保存するかUndoで戻します。
6. Missing Object Referenceは`Open Prefab`で対象へ移動し、正しい参照を手動設定します。

folderを選択した場合は、そのfolder以下のPrefabをまとめて対象にします。scanはPrefabを一時的に読み込んで閉じるため、scanだけではPrefabを変更しません。複数選択時はpath順に検査し、途中でCancelしても完了済みPrefabの結果を残します。

### 4. いつもどおりbuildする

Player build時は操作不要です。buildへ実際に渡されたSceneを同じruleで再検査します。問題がなければ何も出力せず続行し、問題があれば次のようなmessageで開始前に止めます。

```text
Build Guard found build-blocking issues in a Player build Scene.
Scene: Assets/Scenes/Game.unity
Missing Scripts: 1
- Root[0]/Inactive Child[1]: 1
Missing Object References: 1
- Camera Root[1] :: UnityEngine.Camera[1].m_TargetTexture
Repair or remove the listed missing references before building again.
```

`Root[0]`などの数字は同じ親の中での並び順です。`UnityEngine.Camera[1].m_TargetTexture`は、対象GameObjectの2番目のComponentにある`m_TargetTexture` fieldが壊れていることを表します。

## 検査する範囲

- active Build Profileで有効なScene（手動scan）
- Player buildへ実際に渡されたScene（自動検査）
- active・inactiveを問わない全GameObject
- Scene内のPrefab instance
- UnityがSceneへ保存したObject Reference field
- build開始前の元Sceneと、Unityがbuild中に処理する一時Scene
- Project windowで利用者が明示選択したPrefab Asset（手動scan）

閉じたSceneは一時的にadditiveで開き、Prefabは`LoadPrefabContents`で一時展開し、scan後に保存せず閉じます。scanだけでは元のactive Sceneと開閉状態を維持し、修復、保存、削除を行いません。`Open and Remove`を利用者が明示実行した場合だけSceneまたはPrefab Modeへ移動し、そのGameObjectのMissing ScriptをUndoへ記録して除去します。SceneとPrefabは自動保存しません。

## 対象外

このmoduleはbuild対象Sceneと明示選択したPrefab内の壊れたComponent参照だけを扱います。project全体の常時scan、未使用asset検索、Runtimeで後から代入するnull fieldの必須判定、AddressablesやResources内assetの一括検査、PlayerSettings policy、test実行は行いません。Missing Object Referenceの置換、Scene・Prefabの自動保存、複数Assetの一括修復もしません。Runtime assemblyとPlayerへ含まれるcodeもありません。

Assetを削除する前に利用箇所を探し、既存参照をまとめて切り替える用途は、別moduleの **アセット整理・参照管理（Reference Finder）** が担当します。Build Guardは、削除後などに壊れたScene・Prefabを見つけ、Missing Scriptだけを確認付きで除去する用途へ絞っています。

## サンプル

Package ManagerのSamplesから **Build Guard Basics** をImportしてください。

- `BuildGuardBasics.unity`: そのままscan・buildできるScene
- `BrokenSceneExample.unity.txt`: Missing Scriptの失敗を安全に試すtext template
- `BrokenPrefabExample.prefab.txt`: Prefab scanと修復を安全に試すtext template
- `Samples~/Basics/README.md`: Scene・Prefabの問題を作り、手動scanと自動build停止を確認する手順

共有branchへ壊れたSceneを残さないよう、失敗確認はscratch copyか使い捨てprojectで行ってください。
