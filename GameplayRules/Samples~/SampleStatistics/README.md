# Sample Statistics Basics

5種類のsample列を実Buttonで評価する設定済みSceneです。

1. `Balanced`はmean 2.5、variance 1.25、range 3です。
2. `Constant`はmean 7、variance 0、range 0です。
3. `Spread`はmean 0、variance 66.667、range 20です。
4. `Subrange`は外側のsentinelを除き2・4・6だけを評価します。
5. `Extreme`は有限結果を表現できず`ResultOutOfRange`を返します。

UI Toolkitの実Button callbackを使い、960×600の1列と640×360の3+2列をPlayMode testで検証します。
