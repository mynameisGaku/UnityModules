# Deterministic Random 1.0.0

## Requirements

利用側が明示したseedまたは保存状態から、Replay可能な疑似乱数値を得られること。
algorithm変更を保存stateのversion不一致として検出でき、同じ入力列から同じ出力列と次状態を再現できること。

## Future Problem Analysis

乱数位置をglobal stateや実行順へ隠すと、途中save、失敗再現、offline simulation、Replayで同じ抽選へ戻れません。一方、乱数moduleがloot tableやShuffleまで所有すると、domain ruleと乱数源の責務が混ざります。

## Module Boundary

`DeterministicRandomStream`はseed展開、xoshiro256** state遷移、一様な基本値だけを担当します。抽選対象、weight、collection、時刻、入力、保存形式は知りません。

## Contract

- Input: seed、version付きstate、draw operation、range
- State: algorithm version 1と4つの`ulong`
- Output: 64/32-bit値、bool、`[0,1)`浮動小数、半開区間整数
- Failure: `InvalidState`、`InvalidRange`
- Failure invariant: 不正state/rangeでは現在stateを変更しない

## Dependency Flow

```text
Seed / Saved State / Replay Input
               ↓
    DeterministicRandomStream
               ↓
 Exact Value + Next State
               ↓
 Game Rule owned by caller
```

Runtime asmdefは`noEngineReferences: true`です。Unity APIは同梱サンプルのadapter層だけが使います。

## Reproducibility Strategy

公開するのは値だけでなくversion付き状態です。reference state `[1,2,3,4]`から始まる既知の64-bit列と、seed 0のSplitMix64展開stateをgolden testで固定します。
範囲整数はmodulo biasを避けるrejection samplingです。範囲の違いは消費draw数を変え得るため、Replayではseedだけでなく操作順または保存stateを揃える必要があります。

## External Reference

xoshiro256**とSplitMix64のalgorithmはDavid Blackman氏とSebastiano Vigna氏のreference implementationを基準にしています。暗号学的用途ではありません。

## Avoided Overengineering

generic distribution、DI container、global service、jump/substream、serializer、lock、Replay recorderは追加していません。乱数の入力・状態・基本出力だけでEngineなしに独立検証できるため、それ以上は別責務です。
