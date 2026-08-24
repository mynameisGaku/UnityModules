# Weighted Choice Table Basics

Sceneを開き、左から順に実Buttonを押すとID昇順の累積weight区間と2つの選択結果を確認できます。

- `Add Common 6`: ID 10、区間`[0, 6)`
- `Add Rare 3`: ID 20、区間`[6, 9)`
- `Add Epic 1`: ID 30、区間`[9, 10)`
- `Select 0.65 → Rare`: ticket 6.5からID 20を選択
- `Select 0.95 → Epic`: ticket 9.5からID 30を選択

5 Buttonは960×600で1列、640×360で3+2列に収まります。Runtime assemblyはUI Toolkitや乱数生成へ依存しません。
