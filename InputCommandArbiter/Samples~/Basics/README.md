# Input Command Arbiter Basics

Sceneを開くと、同一stepのcommand候補をpriorityと入力順で仲裁する規則を実Buttonで確認できます。

- `No eligible`: 正常な未選択結果
- `Attack only`: 単独eligibleのAttackを選択
- `Interact wins`: Attackより高priorityのInteractを選択
- `Tie keeps first`: 同priorityでは先頭のAttackを選択
- `Reject duplicate`: ineligibleを含む重複command idを選択前に拒否

5 Buttonは960×600で1列、640×360で3+2列に収まります。Runtime assemblyはUI Toolkitへ依存しません。
