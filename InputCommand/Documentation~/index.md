# Input Command 1.0.0

## 問題

離散command(Jump、Dash、Light、Heavy)の扱いは、先行入力の保持、順序判定、同時押し判定、優先順位の決定、相反する方向入力の解決、ノイズ除去という小さな判断の集まりです。別packageへ分けず、同じ目的で導入する6つの独立部品としてまとめています。

Input Commandは、この6つを1つのassemblyへまとめ、同じ境界だけを所有します。

```text
Input: explicit command IDs, ticks, axis candidates, samples, and priorities required by each stage
State: per-stage deterministic state owned by the caller
Output: immutable status struct or explicit error enum
```

Unity時刻、入力device、乱数、global stateを読みません。

## 段

| 段 | 型 | namespace | 役割 |
|---|---|---|---|
| 安定化 | `InputCommandStabilizer` | `InputStabilization` | 同じ候補が連続N回続いた時だけ確定する |
| 軸競合解決 | `InputAxisConflictResolver` | `InputAxisConflict` | 相反する2方向の同時押下を宣言policyで-1/0/1へ解決する |
| 先行入力 | `InputCommandBuffer` | `InputBuffering` | 固定容量・inclusive retention windowでcommandを保持しFIFOで消費する |
| 順序判定 | `InputSequenceMatcher` | `InputSequencing` | pattern一致をgap timeoutとrestart規則付きで判定する |
| 同時押し判定 | `InputChordMatcher` | `InputChording` | 必要command集合の同時成立をspan上限とrearm規則付きで判定する |
| 優先順位 | `InputCommandArbiter` | `InputArbitration` | 有効候補から最大priorityを選び、同値は入力配列の小さいindexで解決する |

段は独立です。必要な段だけを使えます。

## Tick

tickは利用側が進めます。`InputCommandBuffer.TryAdvanceTo`、`InputSequenceMatcher.TryPush`、`InputChordMatcher.TrySample`、`InputAxisConflictResolver.TrySample`は同じtickまたは未来tickだけを受理し、逆行tickは状態を変えずerrorを返します。差分で期限を判定するため`ulong.MaxValue`付近でもoverflowしません。

`InputCommandStabilizer`はsample回数で動くためtickを受け取りません。`InputCommandArbiter`は状態を持たない静的選択です。

## Error

各段は独自のerror enum(`InputCommandBufferError`、`InputSequenceError`、`InputChordError`、`InputCommandArbitrationError`、`InputAxisConflictError`、`InputStabilizationError`)を返します。失敗時に既存stateを暗黙変更しません。

## Engine adapter

Input System等のadapterがbutton edgeを正のcommand idへ変換し、Simulation Clock等がstep開始時にtickを進めます。どのbuttonをどのidへ割り当てるか、どのtickでactionを許可するかは利用側のdomain ruleです。

## 移行

旧6packageからの移行では、namespace、型名、member、動作を変更していないためsource / API互換です。一方、runtime assembly名は変わるためbinary互換ではありません。旧packageを削除して本packageを追加し、自作asmdefのReferencesを`InputCommand.Runtime`へ変更してください。旧assemblyを参照するprecompiled DLLは再buildが必要です。

## 非目標

入力読取、edge検出、analog処理、秒・frame・Coroutineによる期限管理、event通知、global service、singleton、UI、file I/O、network transport、Replay再生は含めません。
