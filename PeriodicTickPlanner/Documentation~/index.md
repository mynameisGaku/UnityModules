# Periodic Tick Planner

## 目的

定期effectはframe落ち、pause復帰、replayのtick jumpでも発火を漏らさず、同じ入力から同じcatch-up範囲を得る必要があります。このmoduleは明示simulation tickだけを使い、今回処理する連続した発火範囲と次cursorを状態変更なしで返します。

## Cursor契約

active cursorは`NextTick >= 0`、`IntervalTicks`と`RemainingCount`が1〜1,000,000,000で、最後の予定tickが`long.MaxValue`以内です。完了済みcursorは`PeriodicTickState.Completed`の`NextTick=0`、`IntervalTicks=1`、`RemainingCount=0`だけを受理します。

## 計画規則

- `NextTick`以上`ThroughTick`以下の予定tickをinclusiveに数える
- 到来総数を`DueCount`として返す
- `MaximumEmissionCount`までを今回の連続発火範囲へ含める
- 上限を超えた到来分は消費せず、`NextState`から同じ`ThroughTick`で続けて計画できる
- 全予定を消費した時だけcanonical完了cursorへ遷移する
- 発火0件では最初・最後のtickを-1とする

1回の最大発火数は1,000,000です。残り回数は最大1,000,000,000ですが、大きなjumpは除算と乗算で算出し、発火数に比例したloopを行いません。

## 検証順序

次回tick、間隔、残り回数、完了表現、schedule overflow、評価tick、今回上限の順で検証します。失敗時はdefault計画と対応する`PeriodicTickError`を返し、cursorを進めません。

## 非目標

実時間取得、Simulation Clock所有、frame更新、damage/heal、stack解決、effect ID、強度比較、MonoBehaviour、Coroutine、callback、threading、network同期、永続化はv1対象外です。

## 検証

EditMode testsはinclusive境界、未来、完了、catch-up分割、巨大jump、long上限、入力保持、決定性、全失敗境界と優先順位を検証します。Sample testsとMono／IL2CPP Player gateは5つの実Button結果と960×600／640×360実描画を検証します。
