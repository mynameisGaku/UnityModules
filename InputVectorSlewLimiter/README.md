# Input Vector Slew Limiter

有限の2D analog targetへ、明示simulation stepごとの最大vector差で近づく純粋processorです。

## 導入

```text
https://github.com/mynameisGaku/UnityModules.git?path=/InputVectorSlewLimiter#input-vector-slew-limiter-v1.0.0
```

## 最小例

```csharp
using InputSmoothing;

if (!InputVectorSlewLimiter.TryCreate(0.25d, 0d, 0d, out var limiter, out var error)) return;
var first = limiter.Process(1d, 0d);  // current (0.25, 0), reached false
var second = limiter.Process(1d, 0d); // current (0.50, 0), reached false
```

## 固定契約

- `MaximumDeltaPerStep`は有限かつ0より大きい
- 初期値・target・reset値の各成分は有限の`[-1,1]`
- target差のmagnitudeが上限以下ならinclusiveでtargetへ到達
- 上限より大きい場合は差vectorの方向を保って上限分だけ進む
- 不正targetと不正resetは現在状態を変えない
- 現在2成分を公開し、同じ構成・初期値・target順で同じ状態を再構築できる

内部で時刻、frame、deltaTime、乱数、Unity API、global stateを読みません。

## 境界と非目標

利用側がsimulation stepごとに`Process`を1回呼びます。実時間依存の速度へ変換する場合も、step durationから上限を利用側で明示計算します。Input System、dead zone、方向量子化、curve、加速度、予測、callback、global service、I/Oは含めません。

## Sample

`Input Vector Slew Limiter Basics`で1 step、2 steps、diagonal、近傍到達、範囲外拒否を確認できます。
