# Input Stabilizer 1.0.0

## 問題

analog入力をinteger commandへ量子化しても、境界付近では隣り合うcommandが交互に現れる場合があります。時間依存debounceを直接simulationへ入れると、frame rateやclockが隠れた入力になります。

Input Stabilizerはcallerが明示した1 commandを1 sampleとして扱い、同じ候補が必要回数連続した時だけ現在値を更新します。

```text
Input: one signed short command per explicit caller sample
State: current command, pending candidate, consecutive count
Output: immutable status with current/candidate/count/changed
```

現在時刻、Unity frame、乱数、Unity API、global stateを読みません。

## 構成

`InputCommandStabilizer.TryCreate`へ必要連続sample数と初期commandを渡します。必要数は`1..65535`です。1なら異なるcommandを受けた同じ呼出しで確定します。

## 状態遷移

- 入力が現在値と同じ: 待機候補を破棄
- 待機なしで異なる入力: 候補を作りcount 1
- 入力が同じ候補: countを加算
- 入力が別候補: 候補を差し替えてcount 1
- countが必要数へ到達: 候補を現在値へ確定し、待機を解除

正、負、0、`short.MinValue`、`short.MaxValue`へ同じ規則を使います。優先度や特別なneutral処理はありません。

## Status

`InputCommandStatus`は処理後の`CurrentCommand`、`CandidateCommand`、`CandidateSampleCount`、`RequiredConsecutiveSamples`、`Changed`を保持します。待機中でない場合、candidateはcurrentと同じでcountは0です。`Snapshot`は状態を進めず`Changed=false`のsnapshotを返します。

## Engine adapter

fixed simulation tickごとに量子化済みcommandを1回`Push`すると、確定遅延をsample数として再現できます。Update回数をsampleにするか、Simulation Clockのstepをsampleにするかは利用側が明示します。

## 非目標

入力device読取、analog dead zone、量子化、smoothing、時間debounce、rate limit、button buffer、event通知、global service、file I/O、network transportは含めません。
