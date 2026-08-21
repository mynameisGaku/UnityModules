# Threshold Tier Table 1.0.0

## Purpose

経験値、評価値、難易度指標等の有限値を、明示した複数thresholdから現在段階と次段階までの進捗へ変換します。段階の意味、値の更新、報酬や演出は利用側に残します。

## Behavior

`TryCreate`は容量1〜32だけを受理します。`TryAddTier`は正のIDと有限thresholdを受理し、threshold昇順へ挿入します。IDまたはthresholdの重複はstateを変更せず拒否します。`TryRemoveTier`はIDで1件削除し、`TryGetTierAt(0)`は常に最小thresholdです。

`TryEvaluate`は最大threshold以下を持つ最後のtierを現在tierとして選びます。最初のthreshold未満では`HasCurrentTier=false`、次tierは最初のtier、progressは0です。thresholdと等しい値はそのtierをinclusiveに選びます。最終tierでは`HasNextTier=false`、progressは1です。

## Errors and observability

容量、ID、threshold、query、重複、容量超過、未登録ID、index、空tableの失敗理由を`ThresholdTierError`で区別します。失敗操作はtableを変更しません。

`ThresholdTierEvaluation`はquery、現在tierの有無・index・値、次tierの有無・値、0以上1以下のprogressを保持します。`Count`、`Capacity`、`TryGetTierAt`から現在stateを再構築できます。

## Numeric boundary

thresholdとqueryはNaN・Infinityを拒否します。正負の極端な有限threshold間でも、差がInfinityへoverflowする場合は各値を0.5倍してから比率を計算し、有限な0〜1へ収めます。

## Limits

level up event、報酬、経験値の蓄積、threshold生成、連続値の補間、thread safety、永続化、Unity objectはv1対象外です。

## Verification

EditMode testsは容量境界、順不同挿入、重複、容量超過、削除、clear、inclusive境界、未到達・段階内・最終tier、極端な有限値、公開型面を検証します。sample testsとMono／IL2CPP Player gateは-10・0・50・250・500のgolden sequenceとwide/narrow実描画を検証します。
