# Utility Score Evaluator

AI、target選択、自動行動等の候補ごとに明示した0〜1のutilityと正weightを集約し、stateを変更せず最高scoreの候補と全factor寄与明細を返す純粋C# moduleです。

## Install

Unity Package ManagerのGit URLへ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/UtilityScoreEvaluator#utility-score-evaluator-v1.0.0
```

Unity 6000.5以降に対応します。Runtime APIはUnityEngineへ依存しません。付属sampleだけがbuilt-in UI Toolkitを使います。

## Quick start

```csharp
using GameplayDecision;

var candidates = new[]
{
    new UtilityScoreCandidate(10, new[]
    {
        new UtilityScoreFactor(1, utility: 0.8d, weight: 3d),
        new UtilityScoreFactor(2, utility: 0.2d, weight: 1d)
    }),
    new UtilityScoreCandidate(20, new[]
    {
        new UtilityScoreFactor(1, utility: 0.7d, weight: 1d)
    })
};

UtilityScoreEvaluator.TryEvaluate(candidates, out var evaluation, out _);
// Candidate 20 wins with score 0.7; Candidate 10 score is 0.65.
```

## Contract

- Input: IDが重複しない1〜32候補。各候補はIDが重複しない1〜16 factorを持ち、utilityは有限な0〜1、weightは有限な0超〜1,000,000
- State: なし。候補配列、factor配列、game stateを変更しない
- Output: weighted meanが最大の候補、安定した先頭tie-break、入力順の全候補score、factor入力順のutility・weight・weighted utility
- Dependency: World、AI Controller、時刻、frame、random、Unity object、他moduleへ依存しない

各候補scoreは`sum(utility * weight) / sum(weight)`です。同scoreでは入力順が先の候補を採用します。候補とfactorの全明細を残すため、利用側は選択理由を再構築できます。

## Existing moduleとの境界

Input Command Arbiterは同一simulation stepのcommandを整数priorityで1件に仲裁します。Weighted Choice Tableは明示sampleをweight区間へ写して抽選します。ContainersのTopNBufferは汎用collectionです。Utility Score Evaluatorは候補ごとの複数utilityをweighted meanへ集約し、安定した選択と全寄与明細を返します。

## Non-goals

World値の取得、utility curve、候補生成、action実行、random抽選、上位N件sort、cooldown、履歴、hysteresis、state変更、callback、時間、network同期、永続化は対象外です。

## Sample

`Utility Score Evaluator Basics`はhighest・weighted・tie・all factor lines・invalid inputを実Buttonで評価します。960×600では5 Button 1列、640×360では3+2列です。
