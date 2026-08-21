# Input Vector Exponential Smoother

有限の2D analog targetへ、明示simulation stepごとに現在差の一定割合で近づくEngine非依存の純粋processorです。

## 導入

```text
https://github.com/mynameisGaku/UnityModules.git?path=/InputVectorExponentialSmoother#input-vector-exponential-smoother-v1.0.0
```

または`InputVectorExponentialSmoother`を`Assets/Modules/`へcopyします。

## 最小例

```csharp
using InputFiltering;

if (!InputVectorExponentialSmoother.TryCreate(0.5d, 0d, 0d, out var smoother, out var error)) return;

var first = smoother.Process(1d, 0d);  // current (0.50, 0), remaining 0.50
var second = smoother.Process(1d, 0d); // current (0.75, 0), remaining 0.25
```

## 固定契約

- `SmoothingFactor`は有限の`0 < factor <= 1`
- 初期値・target・reset値の各成分は有限の`[-1,1]`
- 各stepで`current += (target - current) * factor`
- factor `1`はtargetへexactに到達
- `AppliedDeltaMagnitude`は丸め後に実際に変わったvector差、`RemainingDeltaMagnitude`は更新後のtarget残差
- 小さな差がdouble丸めで進まなくても暗黙snapせず、適用差0・正の残差として観測可能
- 不正targetと不正resetは現在状態を変えない
- 現在2成分とfactorを公開し、同じ構成・初期値・target順で状態を再構築できる

内部で時刻、frame、deltaTime、乱数、Unity API、global stateを読みません。

## 境界

利用側がsimulation stepごとに`Process`を1回呼びます。実時間からfactorを求める場合も、step durationとfilter設計から利用側で明示計算します。Input Radial Dead Zone、Input Vector Response Curveの成功出力を渡せます。一定量で追従させる場合はInput Vector Slew Limiterを選びます。hard dependencyはありません。

## 非目標

- Input System・Legacy Input Manager・binding
- dead zone、入力正規化、magnitude curve、方向量子化
- 一定量slew制限、加速度、spring、予測、hysteresis
- deltaTime読取、自動snap距離、callback、global service
- file I/O、network transport、Replay再生

## Sample

`Input Vector Exponential Smoother Basics`では0.5 factorの1・2step、対角入力、factor 1到達、範囲外拒否を実Buttonで確認できます。
