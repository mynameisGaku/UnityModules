# Simulation Clock 1.0.0

## Requirements

利用側が明示した整数経過時間から、固定stepシミュレーションが今回実行すべき連続範囲と補間端数を得られること。
時計状態を保存・復元し、同じ設定・初期状態・入力列から同じ結果を再現できること。

## Problem Analysis

エンジンの現在時刻、frame rate、global time scaleをロジック内部で読む設計は、テスト・Replay・offline simulationで隠れた依存になります。一方、時計がゲーム処理までcallback実行すると、失敗・順序・所有権が混ざります。

## Module Boundary

`FixedStepClock`は整数tickの量子化と有界catch-upだけを担当します。ゲーム状態、入力、random、物理、描画は知りません。利用側は`FixedStepAdvanceResult`の連続stepを実行し、補間率をpresentationへ渡します。

## Data Model

- Input: `elapsedTicks`
- Parameters: `FixedStepClockSettings`
- State: `FixedStepClockState`
- Output: `FixedStepAdvanceResult`
- Failure: `FixedStepClockError`

## Dependency Flow

```text
Engine / Replay / Test elapsed input
                ↓
         FixedStepClock
                ↓
 Step Range / State / Drop / Alpha
                ↓
 Game Logic and Presentation owned by caller
```

Runtime asmdefは`noEngineReferences: true`です。Unity APIは同梱サンプルのadapter層だけが使います。

## Determinism Contract

同じ`FixedStepClockSettings`、`FixedStepClockState`、同じ順序の整数`elapsedTicks`列は、同じ各Advance結果と最終状態を返します。
catch-up上限へ達しない範囲では、同じ合計時間の分割入力も同じ状態へ収束します。上限を超える場合は呼び出し単位ごとにdrop判定するため、分割方法も入力契約の一部です。

## Why This Boundary

- ActorやSceneなしで単体テストできる
- 状態を値として保存・比較・復元できる
- 記録した整数入力列を高速に再生できる
- Unity時間、Replay時間、AI simulation時間を同じ時計へ渡せる
- catch-up超過を隠さず観測できる

## Avoided Overengineering

step実行callback、event、DI container、Replay serializer、random service、rollback bufferは追加していません。時計の入力・状態・出力だけで独立検証できるため、それ以上の抽象化はv1の現実的な利益がありません。
