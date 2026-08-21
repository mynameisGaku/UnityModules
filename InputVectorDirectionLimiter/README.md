# Input Vector Direction Limiter

unit circle内の有限2D targetへ、明示simulation stepごとに方向回転だけを制限するEngine非依存の純粋processorです。targetのmagnitudeはそのstepで即時反映します。

## 導入

```text
https://github.com/mynameisGaku/UnityModules.git?path=/InputVectorDirectionLimiter#input-vector-direction-limiter-v1.0.0
```

または`InputVectorDirectionLimiter`を`Assets/Modules/`へcopyします。

## 最小例

```csharp
using System;
using InputSmoothing;

if (!InputVectorDirectionLimiter.TryCreate(Math.PI / 4d, 1d, 0d, out var limiter, out var error)) return;

var first = limiter.Process(0d, 1d);  // 方向45°、remaining 45°
var second = limiter.Process(0d, 1d); // target方向へexact到達
```

## 固定契約

- `MaximumTurnRadians`は有限の`0 <= value <= PI`
- 初期値・target・reset値は各成分`[-1,1]`かつmagnitude `<= 1`
- 非ゼロの現在方向からtargetの最短方向へ、1 stepの最大radian以内で回転
- magnitudeはtargetの値をそのstepで即時反映
- 正反対方向の180°tieは反時計回りを選ぶ
- 現在stateがzeroならprior方向を仮定せずtargetを直接受理する
- zero targetはstateをzeroへ更新し、次の非ゼロtargetは新しい方向として受理する
- 不正targetと不正resetは現在stateを変えない
- 現在2成分、実適用・残りradian、prior方向の有無、数値補正を結果から再構築できる

内部で時刻、frame、deltaTime、乱数、Unity API、global stateを読みません。

## 境界

利用側がsimulation stepごとに`Process`を1回呼びます。Input Radial Dead ZoneやInput Vector Response Curveの成功出力を渡せます。vector差のmagnitudeを一定量に制限する場合はInput Vector Slew Limiter、差の一定割合を適用する場合はInput Vector Exponential Smootherを選びます。hard dependencyはありません。

## 非目標

- Input System・Legacy Input Manager・binding
- dead zone、magnitude curve、方向量子化、weighted mix
- magnitudeのslew・指数平滑・加速度・spring・予測
- deltaTime読取、callback、global service
- file I/O、network transport、Replay再生

## Sample

`Input Vector Direction Limiter Basics`では45°回転、2 step到達、時計回り、180°tie-break、unit circle外拒否を実Buttonで確認できます。
