# Input Quantizer 1.0.0

## 問題

analog入力をそのままsimulation commandへ使うと、わずかなnoise、device範囲外値、丸め規則の違いがReplayや状態比較へ持ち込まれます。一方、Input System読取、smoothing、networkまで同時に担うと責務が広がります。

Input Quantizerは明示された有限1軸値だけを、小さなsigned integer commandへ変換します。

```text
Input: finite double axis value
State: immutable dead zone and steps configuration only
Output: InputQuantizationResult(short value or explicit error)
```

現在時刻、乱数、Unity API、global stateを読みません。

## 構成

`AxisQuantizer.TryCreate`へdead zoneと片側段階数を渡します。

- dead zone: 有限の`0 <= value < 1`
- steps: `1 <= value <= 32767`

default値は有効な構成ではありません。構成失敗は`InvalidConfiguration`として作成時と実行時の両方で明示されます。

## 量子化規則

1. NaNとInfinityを拒否する
2. 入力を`[-1, 1]`へclampする
3. 絶対値がdead zone以下なら`0`を返す
4. dead zone外の絶対値を`0..1`へ線形remapする
5. 片側段階数を掛け、最も近い整数へ丸める
6. exact-halfは0から遠ざかる方向へ丸め、元の符号を戻す

正負へ同じ規則を使うため、同じ絶対値は対称commandになります。入力`+2`と`-2`はそれぞれ最大正負commandへclampされます。

## 結果

`InputQuantizationResult.Succeeded`がtrueなら`Value`を利用できます。失敗時の`Value`は0ですが、成功0と区別するため必ず`Succeeded`または`Error`を確認してください。default結果は成功ではありません。

## Engine adapter

Input Systemや別device APIから値を読む処理は利用側に残します。frame境界でanalog値を取得し、本moduleのpure変換を通した`short`だけをsimulationへ渡すと、入力取得と状態遷移を分離できます。

## 非目標

button、vector、smoothing、curve、hysteresis、device calibration、Input System依存、global service、file I/O、network transport、Replay再生は含めません。
