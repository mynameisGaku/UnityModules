# Threat Score Resolver

複数対象の非負threat scoreへ有限の増減を入力順に適用し、各step、最終score、安定した首位を返す純粋なUnity moduleです。

## 特徴

- 1〜32対象、0〜64増減を有界に処理
- 負の増減は0下限でclampし、要求量と実適用量を区別
- 同scoreの首位は小さい正IDを選ぶ安定tie-break
- 入力配列とgame stateを変更しない
- NaN、Infinity、重複ID、未知ID、加算overflowを明示エラー化
- Runtime assemblyはUnityEngine非依存

## 導入

UPMのGit URLへ次を指定します。

`https://github.com/mynameisGaku/UnityModules.git?path=/ThreatScoreResolver#threat-score-resolver-v1.0.0`

## 最小例

```csharp
var entries = new[]
{
    new ThreatScoreEntry(1, 10d),
    new ThreatScoreEntry(2, 20d)
};
var deltas = new[]
{
    new ThreatScoreAdjustment(1, 15d),
    new ThreatScoreAdjustment(2, -5d)
};

if (ThreatScoreResolver.TryResolve(entries, deltas, out var result, out var error, out var failureIndex))
{
    UnityEngine.Debug.Log($"Leader: {result.LeaderTargetId} / {result.LeaderScore}");
}
```

## v1の非目標

時間減衰、距離補正、taunt、target切替、AI state変更、乱数、callback、保存、network同期は利用側の責務です。
