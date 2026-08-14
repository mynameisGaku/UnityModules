# Build Guard Basics

## build成功を確認する

1. `BuildGuardBasics.unity` を開きます。
2. Build ProfilesのScene一覧へ追加します。
3. Player buildを実行します。

このSceneはactive rootとinactive childを持ちますが、Missing Scriptはありません。Build Guardは何も出力せずbuildを継続します。

## build失敗を安全に確認する

`BrokenSceneExample.unity.txt` はMissing Scriptを1件持つScene YAMLをtextとして保持しています。通常はUnity Sceneとしてimportされないため、SampleをImportしただけではprojectを壊しません。

1. scratch branchまたは使い捨てprojectでtemplateを複製します。
2. 複製先の拡張子を `.unity` に変更します。
3. そのSceneだけをPlayer build対象へ指定します。
4. buildが`BuildFailedException`で中止され、`Broken Example[0]`と件数1が表示されることを確認します。
5. 確認後は複製した壊れたSceneを削除します。

Build GuardはSceneを修復、保存、削除しません。
