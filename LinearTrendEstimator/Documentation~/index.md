# Linear Trend Estimator 1.0.0

## Purpose

明示された等間隔sample列を、時間やUnity lifecycleへ依存せず最小二乗の直線傾向へ変換します。sampleの意味、取得間隔、傾きの分類や利用判断は呼び出し側に残します。

## Behavior

`TryEstimate`は配列全体、または`startIndex`と`count`で指定した範囲を評価します。countは2〜32です。指定範囲だけを検証するため、範囲外の値は参照しません。sample indexは選択範囲の先頭を0とします。

`LinearTrendEstimate`は件数、first、last、mean、indexが1増えるごとのslope、index 0のintercept、count位置へ延長したnext predictionを保持します。入力配列は変更しません。

## Errors and observability

null配列、負の開始index、不正件数、配列外範囲、非有限sample、有限結果を表現できない計算を`LinearTrendError`で区別します。失敗時のestimateはdefaultで、部分的な数値を公開しません。

## Numeric boundary

各sampleを選択範囲の最大絶対値で正規化してからmean・covariance・slopeを計算します。これにより有限sampleの積和が不要にoverflowすることを避けます。最後に元scaleへ戻したmean・slope・intercept・predictionがすべて有限であることを確認します。

## Limits

不等間隔時刻、相関係数、分散、外れ値除去、重み付き回帰、多項式回帰、方向enum、予測保証、thread safety、Unity objectはv1対象外です。

## Verification

EditMode testsはnull・範囲・件数・非有限値、上昇・横ばい・下降・noise、部分範囲、最大32件、最大有限値、結果overflow、入力不変、公開型面を検証します。sample testsとMono／IL2CPP Player gateは5つの実Button結果とwide/narrow実描画を検証します。
