# Resource Cost Evaluator 1.0.0

## Purpose

player操作、AI判断、購入UIなどが同じ複数resource costを評価できるよう、明示残量とcostだけから支払可否とresource別の明細を構築します。state更新、予約、実際の消費順序は呼び出し側に残します。

## Behavior

`TryEvaluate`は0〜32件の`balances`と1〜32件の`costs`を受け取ります。両配列は正のresource IDと有限の非負amountを持ち、それぞれの配列内でIDが一意である必要があります。

成功時の`ResourceCostEvaluation`は`CanPay`とcost件数を保持します。`TryGetLine`はcost入力順に`ResourceCostLine`を返します。各明細はavailable、required、remaining、deficit、resource単位の支払可否を保持します。不足時はremaining 0、支払可能時はdeficit 0です。

残量に無いresourceはavailable 0として評価します。costに無い残量entryは結果へ含めません。zero costは残量entryが無くても支払可能です。

## Errors and observability

null配列、件数範囲、非正ID、非有限amount、負amount、残量ID重複、cost ID重複を`ResourceCostError`で区別します。失敗時はevaluationをnullにし、部分明細を返しません。不足そのものはerrorではありません。

## Determinism and ownership

validationと検索は配列indexの昇順に実行し、出力はcost入力順を保ちます。同じbit列の入力は同じ計算順を通ります。Evaluatorは入力配列、Resource Meter、Unity object、global stateを所有または変更しません。

## Limits

実際の消費、部分支払、予約、rollback、refund、currency交換、複数候補の最適化、inventory item、network同期、永続化はv1対象外です。異なる単位を合算した「総不足量」も定義しません。

## Verification

EditMode testsはnull・件数・ID・有限値・負値・重複、全支払、不足、未登録残量、zero cost、入力順、最大32件、最大有限値、入力不変、結果不変、bit安定性、公開型面を検証します。sample testsとMono／IL2CPP Player gateは5つの実Button結果とwide/narrow実描画を検証します。
