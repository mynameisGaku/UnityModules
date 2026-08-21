# Sample Statistics 1.0.0

## Purpose

明示された有限sample列を、時間やUnity lifecycleへ依存せず散らばりを含む要約統計へ変換します。sampleの意味、取得時刻、母集団として扱う範囲、結果の利用判断は呼び出し側に残します。

## Behavior

`TryAnalyze`は配列全体、または`startIndex`と`count`で指定した範囲を評価します。countは1〜32です。指定範囲だけを検証するため、範囲外の値は参照しません。

`SampleStatisticsResult`は件数、minimum、maximum、mean、range、sample数を分母としたpopulation variance、その平方根であるpopulation standard deviationを保持します。入力配列は変更しません。

## Errors and observability

null配列、負の開始index、不正件数、配列外範囲、非有限sample、有限結果を表現できない計算を`SampleStatisticsError`で区別します。失敗時のresultはdefaultで、部分的な数値を公開しません。

## Numeric boundary

入力順を固定したWelford法でmeanと平方偏差和を逐次更新します。同じ配列・範囲・順序は同じ計算順を通ります。各段階と最終range・母分散・母標準偏差が有限であることを確認し、overflowや非有限結果を明示的に拒否します。

## Limits

rolling window、sample variance、不偏分散、percentile、median、histogram、重み、外れ値除去、confidence interval、streaming state、thread safety、Unity objectはv1対象外です。

## Verification

EditMode testsはnull・範囲・件数・非有限値、1件、balanced、constant、symmetric spread、部分範囲、最大32件、最大有限値、結果overflow、bit安定性、入力不変、公開型面を検証します。sample testsとMono／IL2CPP Player gateは5つの実Button結果とwide/narrow実描画を検証します。
