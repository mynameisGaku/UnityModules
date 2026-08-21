# ビルド前の不備確認・修復（Build Guard）

## 30秒で分かる

Player buildに使うSceneをまとめて調べ、壊れた参照を一覧から開けるEditor専用moduleです。

- 削除したC# scriptがComponentとして残った **Missing Script**
- 削除したTexture、Material、Prefabなどを指したままの **Missing Object Reference**

buildを始める前は **Tools > Build Guard > Scan Build Scenes** を押します。問題があればScene、GameObject階層、Component、fieldを一覧表示します。`Open Scene`で修復場所へ移動でき、Missing Scriptは`Open and Remove`で対象GameObjectからUndo記録付きで除去できます。確認を忘れても、Player build開始時に同じ検査が自動実行され、問題があるbuildだけを止めます。

## こんな面倒を減らす

- SceneやPrefabを整理した後、壊れた参照を探してHierarchyとInspectorを手作業で巡回する。
- inactive GameObjectに残ったMissing Scriptを見落とし、長いbuildの後で気付く。
- Consoleのmessageだけを頼りに、どのComponentのどのfieldかを探し直す。
- Missing Scriptを見つけた後、InspectorのComponent menuを一つずつ開いて除去する。
- CIで壊れたSceneを早く止めつつ、修復場所が分かる記録を残す。

## 3分で使う

### 1. インストール

Unity 6000.5.7f1以降で、Package Managerの **Add package from git URL...** へ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/BuildGuard#build-guard-v1.3.0
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

### 3. いつもどおりbuildする

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

閉じたSceneは一時的にadditiveで開き、scan後に保存せず閉じます。scanだけでは元のactive Sceneと開閉状態を維持し、修復、保存、削除を行いません。`Open and Remove`を利用者が明示実行した場合だけ対象Sceneへ移動し、そのGameObjectのMissing ScriptをUndoへ記録して除去します。Sceneは自動保存しません。

## 対象外

このmoduleはbuild対象Scene内の壊れたComponent参照だけを扱います。project全体の未使用asset検索、Runtimeで後から代入するnull fieldの必須判定、AddressablesやResources内assetの一括検査、PlayerSettings policy、test実行は行いません。Missing Object Referenceの置換、Sceneの自動保存、複数Sceneの一括修復もしません。Runtime assemblyとPlayerへ含まれるcodeもありません。

Assetを削除する前に利用箇所を探し、既存参照をまとめて切り替える用途は、別moduleの **アセット参照管理（Reference Finder）** が担当します。Build Guardは、削除後などに壊れたSceneをbuild前に見つけ、Missing Scriptだけを確認付きで除去する用途へ絞っています。

## サンプル

Package ManagerのSamplesから **Build Guard Basics** をImportしてください。

- `BuildGuardBasics.unity`: そのままscan・buildできるScene
- `BrokenSceneExample.unity.txt`: Missing Scriptの失敗を安全に試すtext template
- `Samples~/Basics/README.md`: 2種類の問題を作り、手動scanと自動build停止を確認する手順

共有branchへ壊れたSceneを残さないよう、失敗確認はscratch copyか使い捨てprojectで行ってください。
