# Stat Modifier Stack

## Boundaryとデータフロー

- Input: 正のmodifier ID、3種類のkind、有限値、または有限base値
- State: 有限base値と、ID昇順の最大32 modifier
- Output: 変更前後値、base、3 stage合計、件数、対象ID、error

Unity frame、時刻、random、global state、modifier source objectは境界外です。同じbase値と同じID・kind・値集合は、追加順に関係なく同じsnapshot順と評価結果になります。

## 3 stage式

```text
flatTotal = sum(Flat values by ascending ID)
additivePercentTotal = sum(AdditivePercent values by ascending ID)
multiplicativeFactor = product(MultiplicativeFactor values by ascending ID)
current = (base + flatTotal) * (1 + additivePercentTotal) * multiplicativeFactor
```

`AdditivePercent`の`0.2`は20 percent、`-0.2`はminus 20 percentです。modifier値の符号に暗黙制約を置かないため、負のFlat・percent・factorも明示入力として扱います。game固有の最小値・最大値は利用側の責務です。

## 決定論的な順序

modifierは追加順ではなくID昇順へ挿入します。各合計とfactor積も同じ順で計算するため、同じmodifier集合を異なる順で追加しても同じfloating-point演算順になります。`TryGetModifierAt`から同じ順序を観測できます。

## 失敗時の不変条件

非正ID、未定義kind、NaN・Infinity、重複ID、未登録ID、33件目はstateを変えず失敗します。有限入力でも中間合計、factor積、最終値が有限でなくなるadd・update・remove・base変更はrollbackし、直前のbase・modifier列・現在値を維持します。

## 検証

EditModeで3 stage、追加順独立、ID順snapshot、重複・容量・不正入力、update・remove・base変更・clear、overflow rollback、負値、zero正規化、value equalityを確認します。SampleとMono/IL2CPP Playerは960×600の5列・640×360の3+2列を実描画し、`timeScale=0`でも同じ5操作を再現します。
