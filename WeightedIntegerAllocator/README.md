# Weighted Integer Allocator

XP、通貨、loot budget、team reward等の整数総量を非負整数weight比で配分し、切り捨て後の残りunitをlargest remainder方式で決定論的に配る純粋C# moduleです。

## Install

Unity Package ManagerのGit URLへ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/WeightedIntegerAllocator#weighted-integer-allocator-v1.0.0
```

Unity 6000.5以降に対応します。Runtime APIはUnityEngineへ依存しません。付属sampleだけがbuilt-in UI Toolkitを使います。

## Quick start

```csharp
using GameplayAllocation;

var entries = new[]
{
    new WeightedIntegerEntry(10, weight: 1),
    new WeightedIntegerEntry(20, weight: 1),
    new WeightedIntegerEntry(30, weight: 1)
};

WeightedIntegerAllocator.TryAllocate(entries, totalUnits: 10, out var allocation, out _);
// 入力順に 4, 3, 3 unit。合計は常に10です。
```

## Contract

- Input: IDが重複しない1〜32 entry、0〜1,000,000,000の整数総量、各entryは正IDと0〜1,000,000,000の整数weight
- Base: `totalUnits * weight / totalWeight`を64-bit整数で切り捨て計算
- Remainder: 同じ積の整数剰余が大きいentryから残り1 unitを配り、同剰余は入力順で安定
- Output: total、weight合計、正weight件数、追加unit数、入力順のbase・剰余・追加unit・最終配分
- State: なし。entry配列、inventory、wallet、reward stateを変更しない

整数総量が正ならweight合計も正である必要があります。整数総量0では全weight 0を許可し、全entryへ0を返します。上限内では積を`long`に収め、浮動小数を使いません。

## Existing moduleとの境界

Weighted Choice Tableは明示sampleをweight区間へ写して1件を抽選します。Resource Cost Evaluatorは残量からcostを支払えるか判定します。Weighted Integer Allocatorは乱数も残量も持たず、1つの整数総量を全entryへ合計一致で配分します。

## Non-goals

浮動小数weight、random抽選、inventory slot、所持量更新、reward付与、上限超過の持越し、負配分、network同期、永続化、callbackは対象外です。

## Sample

`Weighted Integer Allocator Basics`はequal・exact weighted・largest remainder・zero weight・zero totalを実Buttonで確認します。960×600では5 Button 1列、640×360では3+2列です。
