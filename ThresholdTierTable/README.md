# Threshold Tier Table

最大32件の有限thresholdを昇順に保持し、評価値から現在tier・次tier・段階内progressを返す純粋C# moduleです。

## Install

Unity Package ManagerのGit URLへ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/ThresholdTierTable#threshold-tier-table-v1.0.0
```

Unity 6000.5以降に対応します。Runtime APIはUnityEngineへ依存しません。付属sampleだけがbuilt-in UI Toolkitを使います。

## Quick start

```csharp
using GameplayProgression;

ThresholdTierTable.TryCreate(3, out var table, out _);
table.TryAddTier(1, 0d, out _);    // Bronze
table.TryAddTier(2, 100d, out _);  // Silver
table.TryAddTier(3, 300d, out _);  // Gold
table.TryEvaluate(250d, out var evaluation, out _);

// evaluation.CurrentTier.Id == 2
// evaluation.NextTier.Id == 3
// evaluation.ProgressToNext == 0.75
```

## Contract

- Input: 容量1〜32、正のtier ID、有限threshold、有限query
- State: tier IDとinclusive thresholdをthreshold昇順で最大32件
- Output: query、現在tierとindex、次tier、0〜1の段階内progress
- Dependency: 時刻、frame、Unity object、他moduleへ依存しない

同じIDと同じthresholdは別々に拒否します。queryが最初のthreshold未満なら現在tier無し・最初のtierが次tier・progress 0です。queryがthresholdと等しい場合はそのtierをinclusiveに選択します。最終tier以降は次tier無し・progress 1です。

## Non-goals

level up event、報酬付与、経験値加算、値の永続化、補間出力、threshold自動生成、thread safety、singletonは対象外です。tier IDの意味とquery単位は利用側が所有します。

## Sample

`Threshold Tier Table Basics`はBronze 0・Silver 100・Gold 300を順不同で登録し、-10・0・50・250・500を実Buttonで評価します。960×600では5 Button 1列、640×360では3+2列です。
