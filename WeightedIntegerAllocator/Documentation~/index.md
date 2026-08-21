# Weighted Integer Allocator 1.0.0

## Purpose

XP、通貨、loot budget、team reward、damage share等の整数総量を比率配分する時、切り捨てでunitが消える問題を、非負整数weightとlargest remainder方式だけで再現可能に解決します。配分後のstate更新は呼び出し側に残します。

## Behavior

`TryAllocate`は1〜32件の`WeightedIntegerEntry`を入力順に検証します。entry IDは正の整数かつ配列内で一意、weightは0〜1,000,000,000、total unitsも0〜1,000,000,000です。

weight合計は`long`で求めます。各entryの積`totalUnits * weight`、base除算、整数剰余も`long`で計算します。上限内の最大積は1,000,000,000,000,000,000で、signed 64-bit範囲に収まります。

base配分の合計を総量から引いた残りunitは、整数剰余が大きいentryから1 unitずつ配ります。同じ剰余では入力indexが先のentryを優先します。出力順は変えません。

正のtotalに対してweight合計0は`ZeroTotalWeight`です。total 0ではweight合計0も許可し、全配分を0として成功します。zero weight entryは正のweightが他にある配分へ参加できますが、unitを受け取りません。

## Result and observability

成功時の`WeightedIntegerAllocation`はrequested total、allocated total、total weight、正weight件数、remainder unit数、entry数を保持します。

`TryGetLine`は入力順にentry ID・index・weight・base units・remainder numerator・追加1 unitの有無・allocated unitsを返します。利用側は最終unitだけでなく端数処理を再構築できます。

null配列、entry数、total、ID、ID重複、weight、zero total weightを`WeightedIntegerError`で区別します。失敗時はallocationをnullにし、部分配分を返しません。

## Determinism and ownership

validation、base計算、remainder順位探索、明細構築は入力index昇順です。浮動小数、sortの不定順、randomを使いません。同じ整数入力は同じ配分を返します。Allocatorは入力、Unity object、global stateを所有または変更しません。

## Limits

浮動小数weight、random、inventory、wallet、reward適用、負配分、持越し、上限超過、callback、network同期、永続化はv1対象外です。

## Verification

EditMode testsはnull・件数・ID・重複・total・weight・zero total weight、equal、exact ratio、largest remainder、同剰余tie、複数追加unit、zero weight、zero total、最大積・最大weight合計、合計保存、入力順、入力/結果不変、決定論、公開型面を検証します。sample testsとMono／IL2CPP Player gateは5つの実Button結果とwide/narrow実描画を検証します。
