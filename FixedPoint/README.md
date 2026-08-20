# Fixed Point

小数値をsigned Q16.16のraw整数として保持し、同じ入力から同じ基本演算結果を得るEngine非依存moduleです。

Replayや固定step simulationで、float差、暗黙overflow、丸め方向の不一致を避けたい小さなstate値に使います。

## 導入

Package Managerの`Add package from git URL...`へ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/FixedPoint#fixed-point-v1.0.0
```

または`FixedPoint`を`Assets/Modules/`へcopyします。

## 最小例

```csharp
var speed = Fixed32.FromRatio(3, 2);
if (!speed.Succeeded) return;

var adjusted = Fixed32.Add(speed.Value, Fixed32.FromRatio(-1, 4).Value);
var doubled = Fixed32.Multiply(adjusted.Value, Fixed32.FromInt32(2).Value);
var final = Fixed32.Divide(doubled.Value, Fixed32.FromInt32(4).Value);

var raw = final.Value.RawValue; // 40960
var display = final.Value.ToDouble(); // 0.625
```

## 固定契約

- signed Q16.16、fractional bit数16、scale 65536
- 表現範囲`-32768`から`32767.9999847412109375`
- FromRatio、Multiply、Divideの余りは0方向へ丸める
- Add/Subtract/Multiply/Divide/Negate/Absはoverflowを明示
- division-by-zeroを明示
- 算術operatorは提供せず、`Fixed32Result`の確認を要求
- 値はimmutableで、失敗操作は入力値を変更しない

`FromRaw`は保存済みまたは通信済みの検証済みraw値を戻すための入口です。通常の整数・比率生成は`FromInt32`と`FromRatio`を使ってください。

## integerとengine境界

- `TruncateToInt32`: 0方向
- `FloorToInt32`: 負の無限大方向
- `CeilingToInt32`: 正の無限大方向
- `ToDouble`: 表示、Transformなどのengine adapter向け

simulationのstate更新はFixed32で行い、Unity APIへ反映する境界でだけdoubleまたはfloatへ変換します。

## 他moduleとの組合せ

Canonical Payloadへ`RawValue`をint32として書けば、Replay Tapeや保存向けのportable bytesにできます。State Fingerprintにも同じraw値を渡せます。どのmoduleにもhard dependencyしません。

## 非目標

- vector、matrix、quaternion、三角関数、平方根
- 物理engineの決定論保証
- float/doubleからの入力変換、文字列parse
- 任意precision、unit system、network同期
- file I/O、global service、singleton

## Sample

Package Managerから`Fixed Point Basics`をimportすると、設定済みSceneで1.5生成、-0.25加算、2倍、4除算、overflow非破壊を確認できます。
