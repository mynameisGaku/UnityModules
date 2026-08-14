# Build Guard 1.0.0

Build Guardは、Player build対象SceneにMissing MonoBehaviourが残ったまま成果物が作られることを防ぐEditor専用moduleです。Runtime assembly、設定asset、global singleton、公開APIを持ちません。

## 検査時点

同じMissing Script ruleを2つのPlayer build callbackから適用します。

1. `BuildPlayerProcessor.PrepareForBuild`で`BuildPlayerOptions.scenes`を取得し、build開始前に全Scene assetを検査します。
2. `IProcessSceneWithReport.OnProcessScene`でUnityが実際に処理する一時Sceneを検査します。

preflightはincremental buildがScene contentを再利用する場合にも毎回動作します。Scene callbackは他のbuild処理が一時Sceneへ加えた変更も検査します。

## Sceneの扱い

- 既に読み込まれているSceneはそのまま検査します。
- 閉じているSceneはadditiveで開き、検査後に保存せず閉じます。
- 検査前のactive Sceneが有効なら、終了時にactive状態を復元します。
- 同じScene pathが重複している場合は、大小文字を区別せず1回だけ検査します。
- 空pathやScene assetとして解決できないpathはUnity本体のbuild診断へ委ねます。
- 検査はScene、Prefab instance、GameObject、Componentを変更しません。

## 検出範囲

Sceneのroot順と各Transformのchild順でactive・inactiveに関係なく全GameObjectを走査し、`GameObjectUtility.GetMonoBehavioursWithMissingScriptCount`が返す件数を収集します。Prefab instanceも展開済みScene階層として同じ規則で扱います。

GameObject名には兄弟indexを付けます。`/`、`\\`、改行、復帰、tabは一行で判別できるようescapeし、検出結果をordinal path順へ並べてからmessageを作成します。

## 対象外

`OnProcessScene`へ渡される`BuildReport`がnullのPlayMode読込と、`BuildPipeline.isBuildingPlayer`がfalseのAssetBundle buildは拒否しません。v1は次を行いません。

- Missing Componentの自動削除やscriptの推測復旧
- project内の全Prefab・Scene・ScriptableObjectの常時scan
- PlayerSettings、Development Build、Profiler、署名、version、secretのpolicy判定
- test、Package Validation Suite、version管理commandの自動実行
- custom Build windowや設定asset

## Build Guard Basics

Package ManagerからSampleをImportすると、次を確認できます。

- `BuildGuardBasics.unity`: active rootとinactive childを持つbuild可能Scene
- `BrokenSceneExample.unity.txt`: Missing Scriptを1件含むtext template

失敗動作を確認する場合だけtemplateをscratch locationへ複製し、`.unity`へ変更してPlayer build対象にしてください。確認後は壊れたSceneを削除してください。

## 検証方針

Editor testはinactive階層、Prefab instance、path escape、決定論的message、閉じたSceneの一時読込とactive Scene復元を検証します。配布gateではclean projectへtarballを導入し、有効Sceneのbuild成功とMissing Script Sceneのbuild失敗を実際の`BuildPipeline.BuildPlayer`で確認します。
