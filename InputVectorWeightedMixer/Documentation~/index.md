# Input Vector Weighted Mixer 1.0.0

## 分解

- Input: 最大32件の順序付き`InputVectorContribution[]`。各entryは有限horizontal・vertical・weight
- State: なし
- Output: 合成成分、weight合計、総件数、正weight件数、数値clamp有無、失敗indexまたは明示error

入力device、Unity frame、時刻、random、global stateは境界外です。同じ順序の配列は同じ結果になります。

## 正規化加重平均

正のweightがある場合は次式を使います。

```text
output = sum(vector[i] * weight[i]) / sum(weight[i])
```

weightは割合なので、`0.75`と`0.25`は`(1,0)`と`(0,1)`を`(0.75,0.25)`へ合成します。全weightが0、またはempty配列の場合は、失敗ではなくneutralな`(0,0)`を返します。

## 数値境界

全entryを計算前に検証します。zero weightでも不正成分を無視しないため、ログから同じ失敗indexを再現できます。

計算時は最大weightで全weightをscaleし、compensated summationを使います。これにより最小の正のdouble weightでも相対比率を保ちます。数学上`[-1,1]`内となる各出力成分が丸めで境界を越えた場合だけclampし、`WasNumericallyClamped`へ記録します。

## 非目標

Input System読取、source登録、priority選択、negative weight、加算mix、magnitude clamp、dead zone、curve、時間filter、callback、状態保持、I/Oは含めません。

## 検証

EditModeでnull・件数上限・empty・全zero・equal/unequal weight・subnormal比率・全errorと失敗index・入力非変更・equalityを検証します。sampleとMono/IL2CPP Playerは960×600の5列・640×360の3+2列を実描画し、timeScale=0でも同じ5操作を再現します。
