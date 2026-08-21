# Numeric Requirement Evaluator 1.0.0

## Purpose

ability、quest、AI、UI等が同じ複数数値条件を評価できるよう、明示された実値・基準値・比較方法だけから全体判定と全明細を構築します。値の取得、条件IDの意味、未達表示、次の処理は呼び出し側に残します。

## Behavior

`TryEvaluate`は1〜32件の`NumericRequirement`を入力順に評価します。IDは正の整数かつ配列内で一意、実値と基準値は有限である必要があります。

比較方法はAtLeast、AtMost、GreaterThan、LessThan、EqualWithinTolerance、OutsideToleranceの6種類です。前4種類は許容差0のみを受理します。後2種類は有限の非負許容差を使い、境界をinclusive equality／strict outsideとして区別します。

成功時の`NumericRequirementEvaluation`は`AllSatisfied`と条件件数を保持します。`TryGetLine`は入力順にactual、expected、comparison、tolerance、signed delta、absolute delta、条件成立可否を返します。1件が未達でも後続を評価し、全明細を保持します。

## Errors and observability

null配列、件数範囲、非正ID、非有限値、未定義比較、許容差不正、ID重複、有限差を表現できない計算を`NumericRequirementError`で区別します。失敗時はevaluationをnullにし、部分明細を返しません。条件未達そのものはerrorではありません。

## Determinism and ownership

validationと計算は配列indexの昇順に実行し、出力も同じ順序を保ちます。同じbit列の入力は同じ計算順を通ります。Evaluatorは入力配列、Unity object、global stateを所有または変更しません。

## Limits

値の探索、文字列parser、AND/OR式tree、短絡評価、callback、localization、resource消費、優先度、state、時間、network同期、永続化はv1対象外です。異なる条件のdeltaを合算しません。

## Verification

EditMode testsはnull・件数・ID・有限値・比較・許容差・重複、6比較の境界、全成立、混在、入力順、最大32件、最大有限値、差overflow、入力不変、結果不変、bit安定性、公開型面を検証します。sample testsとMono／IL2CPP Player gateは5つの実Button結果とwide/narrow実描画を検証します。
