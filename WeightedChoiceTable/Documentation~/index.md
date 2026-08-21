# Weighted Choice Table 1.0.0

## Purpose

呼出側が明示した有限sampleを、再構築可能なweighted entryへ写します。選択結果だけでなく、使用したticket・累積区間・total weight・sorted indexを返すため、replayやdiagnosticsで判定根拠を確認できます。

## Deterministic model

1. entryを正の`Identifier`で昇順へ並べる
2. 正の`Weight`をその順序で加算し、半開区間`[start, end)`を作る
3. `ticket = normalizedSample * totalWeight`を計算する
4. `start <= ticket < end`を満たす最初のentryを選ぶ

entryの追加順ではなくID順を唯一の評価順とします。同じID・weight集合とsampleなら、追加順が異なっても同じtable状態と選択結果になります。

## Input validation

- `Identifier`: 1以上。一意であること
- `Weight`: `NaN`・Infinityではない正数
- `NormalizedSample`: `NaN`・Infinityではない0以上1未満
- 件数: 最大32

追加または更新後のID順weight合計が有限でなければ`NumericOverflow`を返し、既存stateを保持します。0 weightは空区間を作るため受理しません。

## State and observation

`EntryCount`と`TotalWeight`は現在stateです。`TryGetEntryAt`はID昇順snapshotを返し、`TryGetEntry`はIDから同じsnapshotを返します。内部arrayや可変参照は公開しません。

`WeightedChoiceChangeResult`は対象ID、変更前後weight、変更前後total、変更前後件数、成功・実変更・errorを保持します。`WeightedChoiceSelectionResult`はsample、ticket、選択ID、index、weight、区間、total、errorを保持します。

## Failure policy

失敗した変更操作では`Succeeded=false`、`Changed=false`となり、変更前後totalと件数は同じです。空tableの選択は`EmptyTable`、無効sampleは`InvalidSample`です。選択操作は成功・失敗のどちらでもtableを変更しません。

## Floating-point boundary

累積加算は必ずID昇順です。`normalizedSample < 1`でも乗算丸めによりticketがtotalへ一致した場合、total直前の表現可能値へ補正します。これにより最大sampleが最後のentryから外れません。platform間bit同一を必要とする用途では、呼出側で入力weightとsampleを同一表現へ量子化してください。

## Non-goals

乱数生成、seed管理、sampling without replacement、rarityやpity規則、時間・期限、Unity object参照、他module自動連携は対象外です。

## Verification

EditMode testsはID順、追加順不変、半開境界、subnormal、最大有限値、overflow rollback、容量、更新・除去・clear、公開型面を検証します。import sample testsとMono／IL2CPP Player gateは2つのgolden sampleと960×600・640×360の実描画を検証します。
