# Input Sequence Matcher 1.0.0

## 問題

comboや段階入力を`Update`回数や`Time.time`へ直接結び付けると、pause、fixed simulation、Replayで一致結果が変わります。また、入力読取とsequence判定を同じclassへ置くと、device差やframe timingを含む状態になり、純粋な判定を再利用しにくくなります。

Input Sequence Matcherは次の境界だけを所有します。

```text
Input: positive command id and explicit nondecreasing simulation tick
State: cloned pattern, progress, current tick, last matched tick
Output: immutable match, timeout, restart, progress, or explicit error
```

Unity時刻、入力device、乱数、global stateを読みません。

## 作成

`InputSequenceMatcher.TryCreate(pattern, maximumGapTicks, initialTick, ...)`でpatternとtimelineを明示します。

- `pattern`: `1..64`個の正のcommand id。作成時に複製され、caller側の変更はmatcherへ影響しない
- `maximumGapTicks`: 隣接する一致command間で許可する最大tick差。0なら同じtickだけを許可する
- `initialTick`: callerが管理するsimulation timelineの開始位置

null、範囲外の長さ、正でないpattern commandはmatcherを作らず明示errorを返します。

## 入力と間隔

`TryPush(tick, commandId, ...)`は同じtickまたは未来tickだけを受理します。進捗がある場合、次の条件を満たす間だけ継続します。

```text
CurrentInputTick - LastMatchedTick <= MaximumGapTicks
```

差分で判定するため、`ulong.MaxValue`付近でも期限加算のoverflowを起こしません。上限を超えた場合は進捗を0へ戻して`TimedOut`を立て、その現在入力を新しいpattern候補として続けて処理します。

## Matchとrestart

期待commandと一致すれば`Progress`を1進めます。pattern末尾まで一致した入力は`Matched=true`を1回返し、内部進捗は次の照合に備えて0へ戻ります。

不一致時は、それまで進んでいたpatternを破棄したことを`Restarted`で示します。現在commandがpattern先頭と同じなら進捗1から再開し、それ以外なら0へ戻ります。v1は複雑なprefix tableを持たず、この単一先頭restartだけを契約とします。

## 状態と失敗

`Snapshot`は状態を進めず、現在tick、進捗、pattern長、次の期待commandを返します。`Reset(tick)`は進捗を破棄して新しいtimelineへ初期化します。

正でない入力commandは`InvalidCommandId`、現在tickより前の入力は`TickMovedBackward`です。どちらも既存状態を変更しません。

## Engine adapter

入力adapterはbutton edgeを利用側の正のcommand idへ変換します。Simulation Clock等が管理するtickとともにmatcherへ渡し、`Matched`をdomain actionへ変換します。どの入力をどのidへ割り当てるか、どのpatternを使うか、match後に何を実行するかは利用側の責務です。

## 非目標

入力読取、edge検出、held/repeat、analog処理、command buffer、priority arbitration、複数pattern、branch、wildcard、chord、秒ベース期限、event通知、global service、file I/O、network transportは含めません。
