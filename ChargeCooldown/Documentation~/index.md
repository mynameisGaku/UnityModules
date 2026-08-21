# Charge Cooldown 1.0.0

## Requirements

PlayerまたはAIが使用する複数charge制abilityについて、ゲーム全体やUnity clockなしで消費・回復結果を再現できることを目的とします。

## Module boundary

```text
Rules + Current State + Explicit Tick
                    ↓
              ChargeCooldown
                    ↓
       New State + Restored Count + Spent
                    ↓
          Ability / UI / VFX / Save adapter
```

runtime assemblyは`UnityEngine`を参照せず、時間取得、effect発火、保存、表示を行いません。

## Data model

- `ChargeCooldownRules`: 最大charge数と1 chargeの回復tick数
- `ChargeCooldownState`: 利用可能数、最終評価tick、次回復tick
- `ChargeCooldownResult`: 前後state、今回回復した数、消費成否、操作後ready
- `ChargeCooldownError`: rules/state/tick/overflowの失敗理由

stateが満量の場合、`NextRechargeTick`は0です。満量未満の場合は`LastEvaluatedTick`より後のtickを保持します。これにより3 fieldを保存すればscheduleを復元できます。

## Recharge policy

満量から最初のchargeを消費した時点で`currentTick + interval`を次回復tickに設定します。回復中に追加消費しても最古のscheduleを維持します。`TryAdvance`と`TrySpend`はcurrent tickまでに成立した回復をまとめて適用し、最大数で停止してscheduleを0へ戻します。

利用可能chargeが0の`TrySpend`は有効な要求です。回復だけを反映し、`ChargeSpent == false`を返します。状態検証の失敗とは区別されます。

## Validation and precedence

rules、tick、state、tick rollbackの順に検証します。失敗時のresult/stateはdefaultです。次回復tickを`long`で表現できない場合は`TickOverflow`を返し、入力stateを変更しません。

## Reproducibility

同じrules、state、tick、API呼び出し順から同じresultを返します。内部clock、random、global state、実行frame数を読みません。save/restore後、replay、fast-forward、大量offline simulationで同じロジックを共有できます。

## Non-goals

- seconds/frameからtickへの変換
- runtime clockや自動Update
- 可変interval、同時全回復、priority
- ability effect、animation、audio、VFX
- UI、入力lock、pause、scene orchestration
- network同期、保存形式、singleton

## Verification

EditMode testsは境界rules、canonical state、復元、逐次回復、tick jump、空消費、rollback、overflow、equalityを検証します。import済みsample testsとMono/IL2CPP Player gateは実Buttonとwide/narrow UI geometryを検証します。
