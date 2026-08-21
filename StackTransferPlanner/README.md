# Stack Transfer Planner

同じitem種のsource stack列、destination stack列、要求unit数から、実際に移せる量と両側の明細を入力順で構築する純粋C# moduleです。inventory stateは変更しません。

## 導入

Unity 6000.5以降で、Package ManagerのAdd package from git URLへ次を入力します。

    https://github.com/mynameisGaku/UnityModules.git?path=/StackTransferPlanner#stack-transfer-planner-v1.0.0

## 最小例

    using GameplayInventory;

    var sources = new[]
    {
        new StackTransferSource(identifier: 1, availableUnits: 5),
        new StackTransferSource(identifier: 2, availableUnits: 5)
    };
    var destinations = new[]
    {
        new StackTransferDestination(identifier: 11, currentUnits: 0, capacity: 5),
        new StackTransferDestination(identifier: 12, currentUnits: 1, capacity: 6)
    };

    StackTransferPlanner.TryPlan(sources, destinations, requestedUnits: 9, out var plan, out _);
    // plan.TransferredUnits == 9

## 境界

- Input: 1〜32 source、1〜32 destination、0〜1,000,000,000 requested units
- Policy: sourceを入力順で減らし、destinationを入力順で満たす
- Output: requested・transferred・unfulfilled、移送前合計、全source／destination明細
- State: 配列、stack、inventory、storageを変更しない
- Dependency: RuntimeはUnityEngineへ依存しない

IDは各配列内で正かつ一意です。同じIDをsource側とdestination側の両方に使うことはできます。source unitsは0以上、destination capacityは正、current unitsは0〜capacityです。入力が有効なら移送量0でも成功します。

item種の照合、stack生成・削除、空slot探索、装備・重量・容量規則、inventory更新、rollback、network同期、永続化は対象外です。callerは1 item種ごとに呼び出し、返された計画を自身のtransaction境界で適用してください。

## Sample

Package ManagerからStack Transfer Planner Basicsをimportしてください。Full、Partial、Source limit、Destination limit、Zero requestを実Buttonで確認できます。960×600では5 Button 1列、640×360では3+2列です。
