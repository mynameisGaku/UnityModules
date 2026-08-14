# Build Guard

Player buildへ含めるSceneにMissing MonoBehaviourが残っている場合、buildを開始前またはScene処理中に失敗させるEditor専用moduleです。検出したScene path、兄弟index付きのGameObject階層path、GameObjectごとの件数、合計件数を一度に示します。

## インストール

Unity 6000.5.7f1以降を使用してください。

Package Managerの **Add package from git URL...** へ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/BuildGuard#build-guard-v1.0.0
```

または、この `BuildGuard` folderをprojectの `Assets/Modules/` 以下へ配置します。追加packageへの依存はありません。

## 基本動作

導入後の設定やsingletonは不要です。Player buildへ渡されたSceneをbuild開始前に検査し、さらにUnityが実際に処理する一時Sceneを検査します。

- rootから全childを走査し、inactive GameObjectも検査します。
- Prefab instance内も実Scene階層として検査します。
- Missing Scriptがなければ何も出力せずbuildを継続します。
- Missing Scriptがあれば自動修復やScene保存をせず、`BuildFailedException`でbuildを中止します。
- PlayModeでのScene読込とAssetBundle buildは対象外です。

失敗messageの例です。

```text
Build GuardがPlayer build対象Scene内のMissing Scriptを検出しました。
Scene: Assets/Scenes/Game.unity
- Root[0]/Inactive Child[1]: 2
合計: 2
Missing MonoBehaviourを修復または削除してからbuildを再実行してください。
```

## v1の境界

v1はMissing MonoBehaviourの検出だけを責務にします。PrefabやSceneの自動修復、project全assetの一括scan、PlayerSettingsやDevelopment Buildのpolicy、test実行、version管理、secret検出、独自Build画面は含みません。Runtime assemblyとPlayerへ含まれるcodeもありません。

## サンプル

Package ManagerのSamplesから **Build Guard Basics** をImportしてください。

- `BuildGuardBasics.unity` はMissing Scriptのないbuild可能Sceneです。
- `BrokenSceneExample.unity.txt` は意図的にMissing Scriptを含む安全なtext templateです。試す場合だけscratch copyを作り、拡張子を `.unity` へ変更してbuild対象に加えてください。

元のprojectや共有branchへ壊れたSceneを残さないでください。
