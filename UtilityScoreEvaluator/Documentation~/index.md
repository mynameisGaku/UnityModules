# Utility Score Evaluator 1.0.0

## Purpose

AI、target選択、自動行動、UI推薦等が同じ候補評価を共有できるよう、呼び出し側が明示した正規化utilityとweightだけから最高scoreの候補と全寄与明細を構築します。World値の取得、候補IDの意味、action実行は呼び出し側に残します。

## Behavior

`TryEvaluate`は1〜32件の`UtilityScoreCandidate`を入力順に評価します。候補IDは正の整数かつ配列内で一意である必要があります。

各候補は1〜16件の`UtilityScoreFactor`を持ちます。factor IDは正の整数かつ候補内で一意、utilityは有限な0〜1、weightは有限な0より大きく1,000,000以下です。同じfactor IDを別候補で使うことは可能です。

候補scoreはfactor入力順で`sum(utility * weight) / sum(weight)`を計算します。最大scoreの候補を採用し、bit一致する同scoreでは入力indexが先の候補を維持します。

## Result and observability

成功時の`UtilityScoreEvaluation`は採用候補ID、入力index、scoreと候補数を保持します。`TryGetCandidateLine`は候補入力順にID、index、total weight、scoreを返し、各`UtilityScoreCandidateLine.TryGetFactorLine`はfactor入力順にutility、weight、weighted utilityを返します。

null配列、候補数、候補ID、候補ID重複、factor数、factor ID、候補内factor ID重複、utility、weightの不正を`UtilityScoreError`で区別します。失敗時はevaluationをnullにし、部分明細を返しません。

## Determinism and ownership

validationと計算は候補index、factor indexの昇順に実行し、出力も同じ順序を保ちます。同じbit列の入力は同じ計算順とtie-breakを通ります。候補は構築時にfactor配列を複製し、結果も独立した明細を保持します。Evaluatorは入力、Unity object、global stateを所有または変更しません。

## Limits

World/Scene探索、utility curve、候補生成、action実行、random、ranking、priority、cooldown、履歴、hysteresis、callback、時間、network同期、永続化はv1対象外です。

## Verification

EditMode testsはnull・件数・ID・重複・factor数・utility・weight、weighted mean、最高score、先頭tie-break、最大候補/factor、入力順、全寄与、入力/結果不変、bit安定性、公開型面を検証します。sample testsとMono／IL2CPP Player gateは5つの実Button結果とwide/narrow実描画を検証します。
