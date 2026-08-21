# Rolling Sample Window 1.0.0

## Purpose

明示された有限sample列の直近範囲を、時刻やUnity lifecycleへ依存せず固定容量のFIFO stateとして保持します。表示平滑化、短期的な品質指標、gameplay telemetry等のsample意味は利用側に残します。

## Behavior

`TryCreate`は容量1〜32だけを受理します。`Add`はnewestへ1件追加し、満杯ならoldestを1件退避します。`TryGetSampleAt(0)`は常にoldestです。`Clear`は容量を維持したまま空へ戻します。

`SampleWindowSnapshot`は容量、件数、sample有無、min、max、mean、oldest、newestを保持します。空窓では`HasSamples=false`かつ数値fieldは0です。平均はoldest-first順で有限値の凸結合を更新し、単純合計のoverflowを避けます。

## Errors and observability

不正容量は`InvalidCapacity`、NaN・Infinityは`InvalidSample`、範囲外indexは`IndexOutOfRange`です。失敗した`Add`の前後snapshotは同一で、stateを変更しません。

`SampleWindowAddResult`は追加sample、退避有無・退避sample、前後snapshot、errorを保持します。内部arrayは公開せず、`Snapshot`と`TryGetSampleAt`から現在stateを再構築します。

## Limits

時間窓、重み、分散、percentile、thread safety、永続化、Unity objectはv1対象外です。最大32件の再集計を選び、min/maxの古いcacheや退避時補正を持ちません。

## Verification

EditMode testsは容量境界、FIFO wrap、退避、非有限値でのstate不変、min/max退避後再計算、overflowを避けるmean、clear、oldest-first取得、公開型面を検証します。sample testsとMono／IL2CPP Player gateは10・20・30・40のgolden sequenceとwide/narrow実描画を検証します。
