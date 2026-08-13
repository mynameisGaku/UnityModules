# Screen Transition Basics

明るい背景と状態表示を同じUI Toolkit panelに置き、Screen Transitionのオーバーレイがdisplay viewport全体を覆うことを確認するサンプルです。

## 開き方

1. Package Managerから **Screen Transition Basics** をImportします。
2. `ScreenTransitionBasics.unity`を開きます。
3. Play Modeを開始します。
4. Game Viewの **Cover**、**Reveal**、**Auto Demo**を操作します。

Coverは青黒色で画面を覆い、Revealは背景を再表示します。Auto DemoはCoverとRevealを続けて実行します。画面上部には現在の段階、進捗、不透明度、最後の結果が表示されます。

## 構成

- `ScreenTransitionBasicsPanelSettings.asset`がscale、display、panel間のsort orderを所有します。
- Sceneの`UIDocument`がpanel内の描画順を所有します。
- `ScreenTransitionController`が全画面オーバーレイと遷移の寿命を所有します。
- `ScreenTransitionBasicsController`が背景、説明、ボタン、状態表示を組み立てます。

SampleのImportだけではProject Settings、Build Profile、開いているSceneを変更しません。Legacy Input APIは使用しません。
