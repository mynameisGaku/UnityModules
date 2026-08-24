# Stable Score Selector Basics

current候補と候補score列を実Buttonで評価する設定済みSceneです。

1. `Select`はcurrent未選択から最高scoreのID 2を選びます。
2. `Keep`はscore差0.06がminimum advantage 0.10未満なのでID 1を維持します。
3. `Switch`はscore差0.16がminimum advantage 0.10以上なのでID 2へ切り替えます。
4. `Tie`はminimum advantage 0でも同scoreのcurrent ID 20を維持します。
5. `Missing`はcurrent ID 99が入力に無いためbest ID 8へ復帰します。

UI Toolkitの実Button callbackを使い、960×600の1列と640×360の3+2列をPlayMode testで検証します。
