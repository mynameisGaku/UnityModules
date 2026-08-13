# Scene Flow Basics

`SceneFlowService`で次の4操作を順番に確認するサンプルです。

1. Target AをSingleで読み込みます。
2. Target BをAdditiveで追加します。
3. Target Bを有効Sceneにします。
4. 有効SceneではなくなったTarget Aをアンロードします。

## 開き方

1. Package ManagerからこのSampleをImportします。
2. Unityメニューの `Tools > Scene Flow > Setup Basics Sample` を実行します。
3. 開いたBootstrap SceneをPlayします。
4. Game Viewのボタンを上から順に押します。

Setupは現在のBuild Profileまたはplatform profileのScene一覧を使います。既存Sceneと順序は維持し、不足する3 Sceneだけを末尾へ追加します。既にあるサンプルSceneが無効な場合は、その位置で有効にします。再実行しても重複しません。

SampleのImportだけではBuild Profileや開いているSceneを変更しません。
