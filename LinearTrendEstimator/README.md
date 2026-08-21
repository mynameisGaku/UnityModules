# Linear Trend Estimator

2〜32個の等間隔な有限sampleへ最小二乗直線を当て、平均・傾き・切片・次sample予測を返す純粋C# moduleです。

## Install

Unity Package ManagerのGit URLへ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/LinearTrendEstimator#linear-trend-estimator-v1.0.0
```

Unity 6000.5以降に対応します。Runtime APIはUnityEngineへ依存しません。付属sampleだけがbuilt-in UI Toolkitを使います。

## Quick start

```csharp
using GameplayAnalysis;

var samples = new[] { 10d, 30d, 20d, 40d };
LinearTrendEstimator.TryEstimate(samples, out var estimate, out _);

// estimate.Mean == 25
// estimate.SlopePerSample == 8
// estimate.InterceptAtIndexZero == 13
// estimate.PredictedNextSample == 45
```

## Contract

- Input: 2〜32個の等間隔な有限`double` sample、または配列内の明示範囲
- State: なし。入力配列を変更しない
- Output: 件数、first/last、mean、sample indexあたりのslope、index 0のintercept、次indexのprediction
- Dependency: 時刻、frame、Unity object、他moduleへ依存しない

sample magnitudeで正規化してから最小二乗を計算し、単純な積和の不要なoverflowを避けます。有限入力でも有限な傾き・切片・予測を表現できない場合は`ResultOutOfRange`として部分結果を返しません。

## Non-goals

sample取得、時間間隔補正、方向分類、平滑化、外れ値除去、信頼区間、予測精度保証、回帰曲線、thread safety、永続化は対象外です。

## Sample

`Linear Trend Estimator Basics`はrising・flat・falling・noisyの4列と、有限結果を表現できないextreme列を実Buttonで評価します。960×600では5 Button 1列、640×360では3+2列です。
