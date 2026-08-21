# Numeric Requirement Evaluator

複数の実値・基準値・比較方法・許容差から、stateを変更せず「全条件を満たすか」と条件別の成立明細を返す純粋C# moduleです。

## Install

Unity Package ManagerのGit URLへ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/NumericRequirementEvaluator#numeric-requirement-evaluator-v1.0.0
```

Unity 6000.5以降に対応します。Runtime APIはUnityEngineへ依存しません。付属sampleだけがbuilt-in UI Toolkitを使います。

## Quick start

```csharp
using GameplayRules;

var requirements = new[]
{
    new NumericRequirement(1, actualValue: 5d, expectedValue: 3d, NumericRequirementComparison.AtLeast),
    new NumericRequirement(2, actualValue: 5d, expectedValue: 4d, NumericRequirementComparison.AtMost)
};

NumericRequirementEvaluator.TryEvaluate(requirements, out var evaluation, out _);
// evaluation.AllSatisfied == false
// ID 1 passes; ID 2 remains observable as unmet.
```

## Contract

- Input: IDが重複しない1〜32件の有限な実値・基準値、定義済み比較方法、有限の非負許容差
- State: なし。入力配列やgame stateを変更しない
- Output: 全体の成立可否と、入力順のactual・expected・comparison・tolerance・delta・absolute delta・成立明細
- Dependency: 時刻、frame、Unity object、他moduleへ依存しない

大小比較は許容差0を要求します。`EqualWithinTolerance`は絶対差が許容差以下、`OutsideTolerance`は絶対差が許容差より大きい場合に成立します。未達条件は入力errorではなく、`TryEvaluate=true`かつ`AllSatisfied=false`で表す正常なdomain結果です。

## Existing moduleとの境界

Threshold Tier Tableは単一値をtierへ写します。Resource Cost Evaluatorはresource残量とcostから支払後残量・不足量を返します。Numeric Requirement Evaluatorは意味を持たない一般数値条件を比較し、値の取得やresource消費を行いません。

## Non-goals

値の取得、文字列式、AND/OR tree、短絡評価、resource消費、localization、優先度、callback、state、時間、network同期、永続化は対象外です。

## Sample

`Numeric Requirement Evaluator Basics`はall pass・mixed・tolerance・strict・invalid inputを実Buttonで評価します。960×600では5 Button 1列、640×360では3+2列です。
