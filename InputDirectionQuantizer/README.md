# Input Direction Quantizer

有限の2D analog値を、radial dead zone付きの4-wayまたは8-way方向へ決定論的に変換するEngine非依存moduleです。

## 導入

Package Managerの`Add package from git URL...`へ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/InputDirectionQuantizer#input-direction-quantizer-v1.0.0
```

または`InputDirectionQuantizer`を`Assets/Modules/`へcopyします。

## 最小例

```csharp
using InputDirectionQuantization;

if (!InputDirectionQuantizer.TryCreate(0.1d, InputDirectionMode.EightWay, out var quantizer, out var setupError)) return;

var right = quantizer.Quantize(0.9d, 0.1d);      // (1, 0)
var upLeft = quantizer.Quantize(-0.7d, 0.7d);    // (-1, 1)
var neutral = quantizer.Quantize(0.06d, 0.08d);  // (0, 0)
```

## 固定契約

- `DeadZone`は有限の`0`以上`1`未満
- modeは`FourWay`または`EightWay`
- horizontal・verticalをそれぞれ`[-1,1]`へclampしてからradial長さを判定
- 長さがdead zone以下ならinclusiveでneutral `(0,0)`
- FourWayは絶対値の大きい軸を選び、exact tieはvertical
- EightWayは固定した`tan 22.5°`境界をcardinal側へinclusiveに割り当て、その間をdiagonalとする
- 成功方向の各成分は`-1`、`0`、`1`
- NaNとInfinityは`NonFiniteInput`、defaultまたは不正構成は`InvalidConfiguration`

`InputDirectionQuantizer`と`InputDirectionQuantizationResult`はimmutableです。時刻、乱数、Unity API、global state、三角関数を読みません。

## 境界

Input System等から`Vector2`を読む処理は利用側adapterに置き、double成分だけを本moduleへ渡します。返った2成分はInput Stabilizer、Input Sequence Matcher、Replay Tape、Canonical Payload等へ明示的に変換できます。どのmoduleにもhard dependencyしません。

## 非目標

- Input System・Legacy Input Manager・binding
- 1軸の多段階量子化、analog強度、正規化済みvector
- smoothing、hysteresis、device calibration、curve
- command ID割当、sequence、effect callback
- global service、file I/O、network transport、Replay再生

## Sample

`Input Direction Quantizer Basics`ではradial境界、8-way cardinal・diagonal、4-way tie、NaN拒否を実Buttonで確認できます。
