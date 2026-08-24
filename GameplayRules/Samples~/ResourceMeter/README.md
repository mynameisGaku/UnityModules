# Resource Meter Basics

Sceneを開くと、capacity 100・初期値40のresource変更を実Buttonで確認できます。各scenarioは比較しやすいよう同じ初期stateから開始します。

- `Restore +30`: 40から70へ全量回復
- `Partial spend 50`: 不足時も40を適用して0へ到達し、未適用10を返す
- `Require spend 50`: 不足時は40を維持し、要求50を全て未適用として返す
- `Exact spend 40`: 全量必須policyでexactに0へ到達
- `Reject -1`: 負amountを拒否して直前stateを保持

5 Buttonは960×600で1列、640×360で3+2列に収まります。Runtime assemblyはUI Toolkitへ依存しません。
