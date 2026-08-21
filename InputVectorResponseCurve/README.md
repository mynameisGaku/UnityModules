# Input Vector Response Curve

単位円内の有限2D analog入力へ、方向を保つmagnitude response curveを決定論的に適用するEngine非依存moduleです。

## 導入

Package Managerの`Add package from git URL...`へ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/InputVectorResponseCurve#input-vector-response-curve-v1.0.0
```

または`InputVectorResponseCurve`を`Assets/Modules/`へcopyします。

## 最小例

```csharp
using InputResponse;

if (!InputVectorResponseCurve.TryCreate(InputVectorResponseMode.Squared, out var curve, out var setupError)) return;

var result = curve.Process(0.3d, 0.4d);
// input magnitude 0.5 -> output magnitude 0.25
// output vector (0.15, 0.20)
```

## 固定契約

- 構成は`Linear`、`Squared`、`Cubic`、`SmoothStep`のいずれか
- 入力は有限double horizontal・verticalで、radial magnitudeが0以上1以下
- `Linear`: `outputMagnitude = magnitude`
- `Squared`: `outputMagnitude = magnitude²`
- `Cubic`: `outputMagnitude = magnitude³`
- `SmoothStep`: `outputMagnitude = magnitude² * (3 - 2 * magnitude)`
- zeroとunit境界をinclusiveに保持し、処理後も入力方向を維持
- NaNとInfinityは`NonFiniteInput`、単位円外は`InputOutOfRange`、defaultまたは未定義modeは`InvalidConfiguration`
- 失敗結果の数値は0。成功zeroとの区別には`Succeeded`か`Error`を使う

`InputVectorResponseCurve`と`InputVectorResponseResult`はimmutableです。処理はstatelessで、時刻、乱数、Unity API、global stateを読みません。

## 境界

Input System等から`Vector2`を読む処理は利用側adapterに置き、単位円内へ正規化したdouble成分だけを本moduleへ渡します。Input Radial Dead Zoneの成功出力を本moduleへ渡し、その後にInput Vector Slew Limiterを適用できます。どのmoduleにもhard dependencyしません。

## 非目標

- Input System・Legacy Input Manager・binding
- dead zone、clamp、入力正規化、device calibration
- 時間依存smoothing、slew制限、hysteresis
- 方向sectorへの量子化、1軸の段階化、button化
- command ID割当、buffer、sequence、effect callback
- global service、file I/O、network transport、Replay再生

## Sample

`Input Vector Response Curve Basics`では4種類のcurveと単位円外入力の拒否を実Buttonで確認できます。
