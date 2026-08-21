# ビルド前の不備チェック（Build Guard）

## 30秒で分かる

Player buildに含めるSceneを自動で調べ、次の「Inspectorでは見落としやすい壊れた参照」が残っていれば、長いbuildを始める前に止めます。

- 削除したC# scriptがComponentとして残った **Missing Script**
- 削除したTexture、Material、PrefabなどをComponent fieldが参照したままの **Missing Object Reference**

設定画面を開く必要はありません。Packageを導入して通常どおりPlayer buildするだけです。失敗messageにはScene、GameObject階層、Component型、field pathをまとめて表示します。

## こんなときに便利

- PrefabやSceneを整理したあと、どこに壊れた参照が残ったか分からない。
- inactive GameObjectまで手作業でInspectorを開いて確認するのが面倒。
- CI buildを早く止め、修復場所が分かるmessageを残したい。

## 3分で使う

### 1. インストール

Unity 6000.5.7f1以降で、Package Managerの **Add package from git URL...** へ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/BuildGuard#build-guard-v1.1.0
```

または`BuildGuard` folderをprojectの`Assets/Modules/`へ配置します。追加packageへの依存はありません。

### 2. いつもどおりbuildする

Build ProfilesのScene一覧を設定し、Player buildを実行します。問題がなければBuild Guardは何も出力せず、そのままbuildを続けます。

### 3. messageから修復場所を開く

壊れた参照がある場合はbuildが止まり、次のようなmessageがConsoleへ出ます。

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

## 何を検査するか

- Player buildへ実際に渡されたScene
- active・inactiveを問わない全GameObject
- Scene内のPrefab instance
- UnityがSceneへ保存するObject Reference field
- build開始前の元Sceneと、Unityがbuild中に処理する一時Scene

検出時もSceneを自動修復、保存、削除しません。

## 対象外

このmoduleはbuild対象Sceneの壊れた参照に責務を絞ります。project全体の未使用asset検索、Runtimeで代入するnull fieldの判定、AddressablesやResources内assetの一括検査、PlayerSettings policy、test実行、自動修復は行いません。Runtime assemblyとPlayerへ含まれるcodeもありません。

## サンプル

Package ManagerのSamplesから **Build Guard Basics** をImportしてください。

- `BuildGuardBasics.unity`: そのままbuildできるScene
- `BrokenSceneExample.unity.txt`: Missing Scriptの失敗を安全に試すtext template
- `Samples~/Basics/README.md`: Missing Object Referenceを安全に作る3分手順

共有branchへ壊れたSceneを残さないよう、失敗確認はscratch copyか使い捨てprojectで行ってください。
