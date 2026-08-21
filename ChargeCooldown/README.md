# Charge Cooldown

複数chargeを持つabilityの消費と逐次回復を、Unity時刻ではなく明示simulation tickから決定論的に計算する小さなruntime moduleです。

## Install

Unity Package ManagerのGit URLへ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=ChargeCooldown#charge-cooldown-v1.0.0
```

## Example

```csharp
ChargeCooldown.TryCreateRules(3, 10, out var rules, out _);
ChargeCooldown.TryCreateState(rules, 100, 3, out var state, out _);

ChargeCooldown.TrySpend(state, rules, 100, out var spent, out _);
// spent.State.AvailableCharges == 2
// spent.State.NextRechargeTick == 110

ChargeCooldown.TryAdvance(spent.State, rules, 135, out var advanced, out _);
// advanced.State.AvailableCharges == 3
// advanced.State.NextRechargeTick == 0
```

## Contract

- Input: 1〜32 charge、正のrecharge interval、非負かつ非減少の明示tick、再構築可能state
- State: available charges、last evaluated tick、next recharge tick
- Output: previous/new state、回復数、消費成否、ready状態
- Policy: oldest recharge scheduleを維持し、tick jumpで成立済み回復をまとめてcatch up
- Failure: 不正rules/state、tick巻き戻し、64-bit tick overflowを明示errorで返し、部分結果を返さない

`Time.time`、Coroutine、MonoBehaviour、singleton、effect実行へ依存しません。PlayerとAI、replay、save/restore、offline simulationから同じAPIを利用できます。

## Non-goals

時間単位の変換、可変回復間隔、同時全回復、ability effect、UI、入力lock、pause連携、network同期、永続化はv1の責務外です。

## Sample

Package Managerから`Charge Cooldown Basics`をimportすると、3 chargeを消費し、+9/+1/+25 tickの境界とcatch-upを実Buttonで確認できます。
