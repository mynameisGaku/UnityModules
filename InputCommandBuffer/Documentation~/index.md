# Input Command Buffer 1.0.0

## 問題

JumpやDashを受理できる直前にbuttonが押されると、入力eventをその場で捨てる実装では操作が抜けて見えます。一方、`Time.time`やframe数をbuffer内部で読むと、pause、fixed simulation、Replayで期限の判定が変わります。

Input Command Bufferは次の境界だけを所有します。

```text
Input: positive command id and explicit nondecreasing simulation tick
State: fixed-capacity ordered entries, current tick, monotonic sequence
Output: immutable recorded/consumed command or explicit error
```

Unity時刻、入力device、乱数、global stateを読みません。

## 構成

`InputCommandBuffer.TryCreate(capacity, retentionTicks, initialTick, ...)`で固定容量、追加有効tick数、初期tickを明示します。

- `capacity`: `1..1024`
- `retentionTicks`: 記録tick後も有効とする追加tick数。0なら記録tickだけ有効
- `initialTick`: callerが管理するsimulation timelineの開始位置

## Tickと期限

`TryAdvanceTo`は同じtickまたは未来tickだけを受理します。現在tickを更新した後、次を満たさないentryを削除します。

```text
CurrentTick - RecordedTick <= RetentionTicks
```

差分で判定するため、`ulong.MaxValue`付近でも期限加算のoverflowを起こしません。逆行tickは状態を変えず`TickMovedBackward`を返します。

## 記録と消費

`TryRecord`は現在tick、利用側command id、単調増加sequenceを`BufferedInputCommand`へ固定します。同じidと同じtickの重複も独立entryです。固定容量が期限内entryで埋まっている場合は`CapacityExceeded`を返し、既存entryを上書きしません。

`TryPeek`は最も古い一致を変更せず返し、`TryConsume`はその1件だけを削除します。検索対象でないentryの相対順序は維持されます。

## Timelineの終了

`Clear`は現在tickと順序番号を維持してentryだけを削除します。新しいmatch、Scene、Replayへ切り替える場合は`Reset(tick)`でentryと順序番号を破棄し、新しいtimelineを明示します。

## Error

`InvalidCapacity`、`InvalidCommandId`、`TickMovedBackward`、`CapacityExceeded`、`NotFound`、`SequenceExhausted`を区別します。失敗時に既存commandを暗黙削除しません。

## Engine adapter

Input adapterがbutton edgeをcommand idとして`TryRecord`し、Simulation Clock等のstep開始時に`TryAdvanceTo`します。actionが可能になった時だけ対象idを`TryConsume`します。どのbuttonをどのidへ割り当てるか、どのtickでactionを許可するかは利用側のdomain ruleです。

## 非目標

入力読取、edge検出、held/repeat、analog処理、秒ベース期限、combo認識、priority arbitration、自動上書き、event通知、global service、file I/O、network transportは含めません。
