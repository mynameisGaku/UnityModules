# Input Quantizer

有限の1軸analog値を、dead zone付きの小さなsigned integer commandへ決定論的に変換するEngine非依存moduleです。

入力deviceやframe rateからsimulation境界を切り離し、Replay Tapeやnetwork commandへ渡しやすい値を作る用途に向いています。

## 導入

Package Managerの`Add package from git URL...`へ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/InputQuantizer#input-quantizer-v1.0.0
```

または`InputQuantizer`を`Assets/Modules/`へcopyします。

## 最小例

```csharp
if (!AxisQuantizer.TryCreate(0.1d, 8, out var quantizer, out var setupError)) return;

var positive = quantizer.Quantize(0.55d); // +4
var negative = quantizer.Quantize(-0.55d); // -4
var clamped = quantizer.Quantize(2d); // +8

if (!positive.Succeeded)
{
    var error = positive.Error;
}
```

## 固定契約

- `DeadZone`は有限の`0`以上`1`未満
- `StepsPerDirection`は`1`以上`32767`以下
- 入力は量子化前に`[-1, 1]`へclamp
- 絶対値がdead zone以下ならcommand `0`
- dead zone外を`0..1`へ線形remapし、最も近い整数へ丸める
- exact-halfは0から遠ざかる方向へ丸める
- NaNとInfinityは`NonFiniteInput`で拒否
- defaultまたは不正構成は`InvalidConfiguration`で拒否

`AxisQuantizer`と`InputQuantizationResult`はimmutableです。現在時刻、乱数、Unity API、global stateを読みません。

## 境界の置き方

Input Systemなどからanalog値を読む処理は利用側adapterに置きます。`AxisQuantizer`へ明示入力を渡し、返った`short`だけをsimulation、Replay Tape、通信payloadへ渡してください。

## 他moduleとの組合せ

Input Gateで操作mapを遮断し、許可されたanalog値だけをInput Quantizerへ渡せます。生成した`short`はCanonical Payload、Replay Tape、State Fingerprintへ載せられます。どのmoduleにもhard dependencyしません。

## 非目標

- Input SystemやLegacy Input Managerからの値取得
- button、2D/3D vector、smoothing、curve、hysteresis
- device calibration、control scheme、rebind
- global service、singleton、frame更新
- network transport、保存、Replay再生

## Sample

Package Managerから`Input Quantizer Basics`をimportすると、dead zone、正負対称値、clamp、NaNの非破壊拒否を設定済みSceneで確認できます。
