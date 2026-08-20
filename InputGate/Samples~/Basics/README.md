# Input Gate Basics

`InputGateBasics.unity`を開いてPlayしてください。

- `Space`またはGamepad South: Gameplay mapのPulse回数を増やします。
- `Enter`またはGamepad North: 停止対象外UI mapのPulse回数を増やします。
- `Acquire Gate`: Gameplay mapを止めるleaseを1件取得します。
- `Acquire Nested x2`: leaseを2件追加し、入れ子停止を作ります。
- `Release One`: sampleが最後に取得した1件だけを解放します。
- `Release All`: sample所有leaseだけを全解放します。
- `Reset Counters`: 2種類の受付回数を0へ戻します。

Gameplayが停止中でもUI mapと画面Buttonは動き続けます。1件だけ解放しても他のleaseが残っていればGameplayは停止したままです。最後の解放でGameplay mapが復元されます。

Sceneは`PlayerInput`、`InputGateController`、`UIDocument`、sample controllerを設定済みです。ImportだけではProject Settings、Build Profile、開いているSceneを変更しません。外部画像、外部font、UXMLは使用しません。
