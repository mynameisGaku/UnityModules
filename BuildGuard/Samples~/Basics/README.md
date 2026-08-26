# Build Guard Basics

## 1. 正常な手動scanとbuildを確認する

1. `BuildGuardBasics.unity`を開きます。
2. Build ProfilesのScene一覧へ追加します。
3. **Tools > Build Guard > Scan Build Scenes** を開き、`Scan Build Scenes`を押します。
4. `No missing references found.`を確認します。
5. Player buildを実行します。

このSceneはactive rootとinactive childを持ちますが、壊れた参照はありません。Build Guardはbuildを止めません。

## 2. build対象外の選択Sceneを確認する

1. `BuildGuardBasics.unity`を`BuildGuardSelectedScene.unity`として複製します。
2. 複製したSceneをBuild Profilesへ追加せず、Project windowで直接選択します。
3. 右クリックして **Build Guard > Scan Selected Scenes** を選びます。
4. `Selected Scene Assets`が`1`であることを確認し、`Scan Selected Scenes`を押します。
5. `No missing references found.`を確認します。
6. 確認後は複製したSceneを削除します。

選択Scene scanはBuild Profileへの登録や有効状態に関係なく使えます。folderを再帰せず、直接選択した`Assets/`配下の保存済みSceneだけを検査します。

## 3. Missing Scriptを安全に確認する

`BrokenSceneExample.unity.txt`はMissing Scriptを1件持つScene YAMLです。`.txt`なのでSampleをImportしただけではSceneとして読み込まれません。

1. scratch branchか使い捨てprojectでtemplateを複製します。
2. 複製先の拡張子を`.unity`へ変更します。
3. そのSceneだけをPlayer build対象へ指定します。
4. manual scanに`Broken Example[0]`と`Missing Scripts: 1`が出ることを確認します。
5. `Open and Remove`を押し、対象GameObjectからMissing Scriptが除去されることを確認します。
6. Sceneが未保存のまま残ることを確認し、UndoでMissing Scriptが戻ることも確認します。
7. 再度除去してSceneを保存すると、manual scanとPlayer buildがMissing Scriptでは止まらなくなることを確認します。
8. 確認後は複製したSceneを削除します。

## 4. Missing Object Referenceを確認する

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

Build GuardはMissing Scriptだけを明示操作で除去できます。Missing Object Referenceの推測修復、Sceneの自動保存、Asset削除は行いません。`Copy`で1件の修復情報を共有できます。

## 5. PrefabのMissing Scriptを確認する

`BrokenPrefabExample.prefab.txt`はMissing Scriptを1件持つPrefab YAMLです。`.txt`なのでImportしただけではPrefabとして読み込まれません。

1. scratch branchか使い捨てprojectでtemplateを複製します。
2. 複製先の拡張子を`.prefab`へ変更します。
3. Project windowでそのPrefabを選択します。
4. 右クリックして **Build Guard > Scan Selected Prefabs** を選びます。
5. `BrokenPrefabExample[0]`と`Missing Scripts: 1`を確認します。
6. `Open and Remove`を押し、Prefab ModeでMissing Scriptが除去されることを確認します。
7. Prefabが未保存のまま残ることと、UndoでMissing Scriptが戻ることを確認します。
8. 確認後は複製したPrefabを削除します。

Prefab scanもMissing Object Referenceを表示しますが、参照先は推測しません。`Open Prefab`で対象fieldへ移動し、利用者が正しいAssetを指定してください。
