# Stat Modifier Stack Basics

Sceneを開き、左から順に実Buttonを押すとbase 100へ3 stageのmodifierを合成できます。

- `Add Flat +15`: ID 10を追加し、value 115
- `Add Percent +20%`: ID 20を追加し、value 138
- `Add Factor ×1.5`: ID 30を追加し、value 207
- `Update Factor ×0.5`: ID 30を更新し、value 69
- `Reject Duplicate 10`: 既存ID 10を拒否し、value 69を保持

5 Buttonは960×600で1列、640×360で3+2列に収まります。Runtime assemblyはUI Toolkitへ依存しません。
