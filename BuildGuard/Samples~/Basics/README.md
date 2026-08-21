# Build Guard Basics

## 1. 正常な手動scanとbuildを確認する

1. `BuildGuardBasics.unity`を開きます。
2. Build ProfilesのScene一覧へ追加します。
3. **Tools > Build Guard > Scan Build Scenes** を開き、`Scan Build Scenes`を押します。
4. `No missing references found.`を確認します。
5. Player buildを実行します。

このSceneはactive rootとinactive childを持ちますが、壊れた参照はありません。Build Guardはbuildを止めません。

## 2. Missing Scriptを安全に確認する

`BrokenSceneExample.unity.txt`はMissing Scriptを1件持つScene YAMLです。`.txt`なのでSampleをImportしただけではSceneとして読み込まれません。

1. scratch branchか使い捨てprojectでtemplateを複製します。
2. 複製先の拡張子を`.unity`へ変更します。
3. そのSceneだけをPlayer build対象へ指定します。
4. manual scanに`Broken Example[0]`と`Missing Scripts: 1`が出ることを確認します。
5. `Open Scene`で対象GameObjectが選択されることを確認します。
6. Player buildも同じ問題で止まることを確認します。
7. 確認後は複製したSceneを削除します。

## 3. Missing Object Referenceを確認する

この手順もscratch copyで行ってください。

1. `BuildGuardBasics.unity`を複製して開きます。
2. Cameraを1つ追加します。
3. Project windowでRender Texture assetを作成します。
4. Render TextureをCameraのTarget Textureへ設定し、Sceneを保存します。
5. Render Texture assetを削除します。
6. Sceneを閉じて開き直し、そのSceneをPlayer build対象へ指定します。
7. manual scanに`UnityEngine.Camera`と`m_TargetTexture`が出ることを確認します。
8. `Open Scene`でCameraが選択されることを確認します。
9. Player buildも同じ問題で止まることを確認します。
10. 確認後は複製したSceneを削除します。

Build GuardはSceneを修復、保存、削除しません。`Copy`で1件の修復情報を共有できます。
