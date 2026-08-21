# Piecewise Linear Curve 1.0.0

## Purpose

有限なkey point集合から、評価値と使用segmentを再構築可能に返します。`AnimationCurve`や時間へ依存せず、progression・difficulty・response mapping等の意味は利用側に残します。

## Model

pointは一意なXで昇順に並びます。queryがpoint Xと完全一致すればそのYを返します。2点間では`t = (query - lower.X) / (upper.X - lower.X)`を求め、`value = lower.Y * (1 - t) + upper.Y * t`で評価します。範囲外では最寄り端点を上下両pointとして返し、`Clamped=true`とします。

X差が通常の有限範囲を超える場合は各Xを0.5倍して比率を求めます。Y補間は有限pointの凸結合として計算し、途中差分の不要なoverflowを避けます。

## Validation and state

X・Y・queryはNaNとInfinityを拒否します。同じXは`DuplicateX`、33点目は`CapacityReached`です。`TryGetPointAt`はX昇順snapshotを返し、`TryGetPoint`は完全一致Xから同じsnapshotを返します。内部arrayは公開しません。

`CurveChangeResult`は対象X、変更前後Y、変更前後件数、成功・実変更・errorを保持します。`CurveEvaluationResult`はquery、value、上下pointとindex、補間率、clamp、errorを保持します。失敗操作はstate非変更です。

## Non-goals

Bezier・Hermite・接線・easing・外挿・loop・時間進行・Unity object・他module自動連携は対象外です。

## Verification

EditMode testsは順序不変、完全一致、両segment、端点clamp、単一点、極端X/Y、更新・除去・容量・無効値・公開型面を検証します。sample testsとMono／IL2CPP Player gateはgolden queryとwide/narrow実描画を検証します。
