# Input Radial Dead Zone

有限の2D analog入力へ、方向を保つinner・outer radial dead zone補正を決定論的に適用するEngine非依存moduleです。

## 導入

Package Managerの`Add package from git URL...`へ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/InputRadialDeadZone#input-radial-dead-zone-v1.0.0
```

または`InputRadialDeadZone`を`Assets/Modules/`へcopyします。

## 最小例

```csharp
using InputDeadZones;

if (!InputRadialDeadZone.TryCreate(0.1d, 1d, out var deadZone, out var setupError)) return;

var zero = deadZone.Process(0.06d, 0.08d); // (0, 0), magnitude 0
var half = deadZone.Process(0.55d, 0d);     // (0.5, 0), magnitude 0.5
var unit = deadZone.Process(3d, 4d);        // (0.6, 0.8), magnitude 1
```

## 固定契約

- 構成は有限の`0 <= InnerDeadZone < OuterDeadZone <= 1`
- 入力は有限のdouble horizontal・vertical
- 入力magnitudeがinner以下ならinclusiveで`(0,0)`
- input magnitudeがouter以上ならinclusiveで同じ方向のunit vector
- innerとouterの間は`(magnitude - inner) / (outer - inner)`で0..1へ線形remap
- over-range入力もcomponentごとのclampではなくradial方向を保ってunitへ正規化
- NaNとInfinityは`NonFiniteInput`、defaultまたは不正構成は`InvalidConfiguration`
- 極端に大きい有限成分も二乗せず比率から方向を求めるため、magnitude計算でoverflowしない

`InputRadialDeadZone`と`InputRadialDeadZoneResult`はimmutableです。処理はstatelessで、時刻、乱数、Unity API、global stateを読みません。

## 境界

Input System等から`Vector2`を読む処理は利用側adapterに置き、double成分だけを本moduleへ渡します。補正後の連続成分をInput Direction Quantizerや利用側の移動処理へ明示的に渡せます。どのmoduleにもhard dependencyしません。

## 非目標

- Input System・Legacy Input Manager・binding
- 方向sectorへの量子化、1軸の段階化、button化
- smoothing、hysteresis、device calibration、curve
- command ID割当、buffer、sequence、effect callback
- global service、file I/O、network transport、Replay再生

## Sample

`Input Radial Dead Zone Basics`ではinner、mid、outer、over-range、NaN拒否を実Buttonで確認できます。
