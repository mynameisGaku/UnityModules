# Sample Statistics

1〜32個の有限sampleから、最小値・最大値・平均・range・母分散・母標準偏差を返す純粋C# moduleです。

## Install

Unity Package ManagerのGit URLへ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/SampleStatistics#sample-statistics-v1.0.0
```

Unity 6000.5以降に対応します。Runtime APIはUnityEngineへ依存しません。付属sampleだけがbuilt-in UI Toolkitを使います。

## Quick start

```csharp
using GameplayAnalysis;

var samples = new[] { 1d, 2d, 3d, 4d };
SampleStatistics.TryAnalyze(samples, out var result, out _);

// result.Mean == 2.5
// result.Range == 3
// result.PopulationVariance == 1.25
// result.PopulationStandardDeviation == sqrt(1.25)
```

## Contract

- Input: 1〜32個の有限`double` sample、または配列内の明示範囲
- State: なし。入力配列を変更しない
- Output: 件数、minimum、maximum、mean、range、母分散、母標準偏差
- Dependency: 時刻、frame、Unity object、他moduleへ依存しない

入力順を固定したWelford法でmeanと平方偏差和を更新します。同じ入力順から同じ結果を再現でき、単純な`sum / count`より不要な加算overflowを避けます。有限入力でも有限なrange・母分散・母標準偏差を表現できない場合は`ResultOutOfRange`として部分結果を返しません。

## Non-goals

sample取得、rolling window、sample分散、percentile、median、histogram、重み付き統計、外れ値除去、信頼区間、thread safety、永続化は対象外です。

## Sample

`Sample Statistics Basics`はbalanced・constant・spread・subrangeの4列と、有限結果を表現できないextreme列を実Buttonで評価します。960×600では5 Button 1列、640×360では3+2列です。
