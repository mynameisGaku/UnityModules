# Linear Trend Estimator Basics

5種類のsample列を実Buttonで評価する設定済みSceneです。

1. `Rising`はslope 10、intercept 10、next 50です。
2. `Flat`はslope 0、intercept 20、next 20です。
3. `Falling`はslope -10、intercept 40、next 0です。
4. `Noisy`はslope 8、intercept 13、next 45です。
5. `Extreme`は有限結果を表現できず`ResultOutOfRange`を返します。

UI Toolkitの実Button callbackを使い、960×600の1列と640×360の3+2列をPlayMode testで検証します。
