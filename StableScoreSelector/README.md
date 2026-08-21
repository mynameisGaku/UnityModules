# Stable Score Selector

AI、target選択、自動行動等で毎回計算した候補scoreを受け取り、同点や小さな変化ではcurrent候補を維持し、明示した最小優位差を満たすchallengerへだけ切り替える純粋C# moduleです。

## Install

Unity Package ManagerのGit URLへ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/StableScoreSelector#stable-score-selector-v1.0.0
```

Unity 6000.5以降に対応します。Runtime APIはUnityEngineへ依存しません。付属sampleだけがbuilt-in UI Toolkitを使います。

## Quick start

```csharp
using GameplayDecision;

var candidates = new[]
{
    new StableScoreCandidate(10, 0.62d),
    new StableScoreCandidate(20, 0.68d)
};

StableScoreSelector.TrySelect(
    candidates,
    currentIdentifier: 10,
    minimumAdvantage: 0.10d,
    out var selection,
    out _);

// score差は0.06なのでID 10を維持します。
```

## Contract

- Input: IDが重複しない1〜32候補、0は未選択を表すcurrent ID、有限な0〜1の最小優位差
- Candidate: 正の一意IDと有限な0〜1 score
- Selection: currentが有効なら、最高challengerがcurrentより高く、かつscore差が最小優位差以上の時だけ切替
- Recovery: currentが0または入力から消えた場合は、入力順の安定tie-break後の最高score候補を選択
- Output: current・best・challenger・selected、判断理由、入力順の全候補明細
- State: なし。候補配列、game state、Unity objectを変更しない

最小優位差が0でも同scoreではcurrentを維持します。候補全体の最高scoreとcurrent以外の最高challengerは別々に記録されるため、利用側は維持・切替理由を再構築できます。

## Existing moduleとの境界

Utility Score Evaluatorは複数factorから各候補のweighted mean scoreを計算します。Stable Score Selectorは既に計算済みのscoreとcurrent IDを受け取り、微差での振動を抑えます。Input Command Arbiterは同一simulation stepの整数priorityを仲裁し、Weighted Choice Tableは明示sampleで抽選します。本moduleはそれらへ依存しません。

## Non-goals

score計算、World値取得、候補生成、action実行、random、上位N件sort、cooldown、時間によるlock、履歴、内部state、callback、network同期、永続化は対象外です。

## Sample

`Stable Score Selector Basics`は初回選択・微差維持・閾値切替・同点維持・current消失時の復帰を実Buttonで確認します。960×600では5 Button 1列、640×360では3+2列です。
