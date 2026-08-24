# Input Stabilizer Basics

Sceneを開くと、同じcommandを3 sample連続確認してから確定するstate machineを実Buttonで確認できます。

- `Sample +4 · 1/3`: +4候補を開始し、current 0を保持
- `Confirm +4 · 2/3`: 同じ候補の連続countを2へ進める
- `Commit +4 · 3/3`: 3回目でcurrentを+4へ更新
- `Noise -4`: -4候補を1回だけ作り、current +4を保持
- `Return +4`: 現在値へ戻ったsampleでnoise候補を取り消す

Runtime moduleはUI Toolkitへ依存しません。UI Toolkitはこのサンプル表示だけに使います。
