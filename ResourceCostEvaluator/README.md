# Resource Cost Evaluator

複数resourceの明示残量とcostから、stateを変更せず「全て支払えるか」とresource別の支払後残量・不足量を返す純粋C# moduleです。

## Install

Unity Package ManagerのGit URLへ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/ResourceCostEvaluator#resource-cost-evaluator-v1.0.0
```

Unity 6000.5以降に対応します。Runtime APIはUnityEngineへ依存しません。付属sampleだけがbuilt-in UI Toolkitを使います。

## Quick start

```csharp
using GameplayResources;

var balances = new[]
{
    new ResourceAmount(1, 100d), // gold
    new ResourceAmount(2, 30d)   // mana
};
var costs = new[]
{
    new ResourceAmount(1, 25d),
    new ResourceAmount(2, 40d)
};

ResourceCostEvaluator.TryEvaluate(balances, costs, out var evaluation, out _);
// evaluation.CanPay == false
// resource 1: remaining 75, deficit 0
// resource 2: remaining 0, deficit 10
```

## Contract

- Input: IDが重複しない0〜32件の残量と、IDが重複しない1〜32件のcost
- State: なし。入力配列やresource stateを変更しない
- Output: 全体の支払可否と、cost入力順のavailable・required・remaining・deficit明細
- Dependency: 時刻、frame、Unity object、Resource Meter、他moduleへ依存しない

IDは正の整数、amountは有限の非負値です。cost側に存在して残量側に無いresourceは残量0として不足を返します。costに無い残量は無視します。不足は入力errorではなく、`TryEvaluate=true`かつ`CanPay=false`で表す正常なdomain結果です。

## Resource Meterとの境界

Resource Meterは1つのresource stateへ回復・消費を適用します。Resource Cost Evaluatorは複数resourceを変更せず一括判定し、呼び出し側が適用判断に使える明細だけを返します。Evaluatorはmeterの参照、予約、rollback、部分適用を行いません。

## Non-goals

resource stateの変更、予約、部分支払、currency交換、inventory item、refund、rollback、priority、network同期、永続化、singleton、threadingは対象外です。

## Sample

`Resource Cost Evaluator Basics`はpayable・shortage・missing balance・zero cost・invalid inputを実Buttonで評価します。960×600では5 Button 1列、640×360では3+2列です。
