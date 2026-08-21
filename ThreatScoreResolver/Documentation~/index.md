# Threat Score Resolver 1.0.0

## 計算契約

初期entryは1〜32件で、TargetIdは正かつ重複不可、Scoreは有限の非負値です。増減は0〜64件で、初期entryに存在するTargetIdと有限Deltaを持ちます。

増減は入力順に適用します。負のDeltaが現在scoreを超える場合は0へclampし、`ThreatScoreStep.RequestedDelta`には要求値、`AppliedDelta`には実際に適用した値を返します。正の加算が有限範囲を超える場合、部分結果を返さず`ScoreOverflow`で失敗します。

最終首位は最大scoreです。同scoreでは小さいTargetIdを選び、初期entryの並び順に依存しません。最終entry列自体は初期入力順を維持します。

## 失敗index

entry検証の失敗では`failureIndex`がentry index、増減検証・適用の失敗では増減indexです。nullまたは件数違反では-1です。失敗時のresolutionはnullで、入力列は変更されません。

## 非目標

本moduleは脅威度を保持するcomponentやAI target controllerを提供しません。時間減衰、距離、視線、taunt、陣営、target切替、callback、乱数、永続化は利用側で構成してください。

## 検証

EditMode testは入力順、0下限、同点tie-break、全エラー、最大件数、入力不変性を検証します。import sample testとPlayer gateは実Button 5操作と960×600／640×360のUI geometryを検証します。
