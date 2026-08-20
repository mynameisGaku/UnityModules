# Input Repeat 1.0.0

## 問題

menu navigationや連射を`Update`回数、`Time.time`、Coroutineへ直接結び付けると、pause、fixed simulation、Replayでrepeat回数が変わります。また、frame落ちやtick jumpで1回しか処理しない実装は、本来到達していたrepeatを黙って失います。

Input Repeatは次の境界だけを所有します。

```text
Input: explicit nondecreasing simulation tick and current pressed state
State: press tick and emitted repeat count
Output: immutable initial/repeat/release counts or explicit error
```

Unity時刻、入力device、乱数、global stateを読みません。

## 作成

`InputRepeatTracker.TryCreate(initialDelayTicks, repeatIntervalTicks, initialTick, ...)`でscheduleを明示します。

- `initialDelayTicks`: 押下edgeから最初のrepeatまでの正のtick差
- `repeatIntervalTicks`: repeat間の正のtick差
- `initialTick`: callerが管理するsimulation timelineの開始位置

delayまたはintervalが0の場合はtrackerを作らず、対応する明示errorを返します。

## 押下とrepeat

非押下から押下になったsampleは`InitialTriggered=true`、`TriggerCount=1`を返します。保持中は次のelapsed scheduleでrepeat総数を計算します。

```text
elapsed < initialDelay: due = 0
elapsed >= initialDelay: due = 1 + (elapsed - initialDelay) / repeatInterval
newlyDue = due - alreadyEmitted
```

加算した絶対期限を保存せず差分で計算するため、`ulong.MaxValue`付近でもoverflowせず、tick jump時も未発行repeatを`RepeatTriggerCount`へまとめて返します。同じtickや期限間のsampleは0です。

## 解放とtimeline

押下から非押下になったsampleは`Released=true`を1回返し、repeat進捗を破棄します。以後の非押下sampleはreleaseを再発行しません。再度押下すれば、新しい初回triggerとscheduleを開始します。

`Snapshot`は現在tickとpressed状態だけを返し、edgeやtriggerを再発行しません。`Reset(tick)`は新しいtimelineの非押下状態へ明示的に初期化します。

## 失敗

現在tickより前の入力は`TickMovedBackward`です。現在tick、pressed状態、repeat進捗を変更しません。

## Engine adapter

入力adapterはbuttonのpressed状態を読み、simulation stepのtickとともにtrackerへ渡します。利用側は`TriggerCount`を逐次処理するか、複数repeatを1回のdomain操作へまとめるかを選びます。発行先、catch-up上限、効果の実行は利用側の責務です。

## 非目標

入力読取、button mapping、event購読、秒ベースschedule、Coroutine、callback実行、drop policy、加速curve、command buffer、sequence、chord、priority、global service、file I/O、network transportは含めません。
