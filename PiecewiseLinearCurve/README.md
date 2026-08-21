# Piecewise Linear Curve

最大32個の有限pointをX昇順へ保持し、有限queryを隣接2点から線形補間する純粋C# moduleです。範囲外queryは端点へclampし、値だけでなく使用point・index・補間率・clamp状態を返します。

## Install

```text
https://github.com/mynameisGaku/UnityModules.git?path=/PiecewiseLinearCurve#piecewise-linear-curve-v1.0.0
```

Unity 6000.5以降。Runtime assemblyはUnityEngineへ依存せず、UI Toolkit built-in moduleはsampleだけが利用します。

## Quick start

```csharp
using GameplayMath;

var curve = new PiecewiseLinearCurve();
curve.Add(0d, 0d);
curve.Add(10d, 100d);
curve.Add(20d, 50d);

var result = curve.Evaluate(15d);
// result.Value == 75
// result.LowerPoint == (10, 100)
// result.UpperPoint == (20, 50)
// result.Interpolation == 0.5
```

## Boundary

- Input: 一意な有限X、有限Y、有限query
- State: 最大32 point、X昇順
- Output: query・value・lower/upper point・index・補間率・clamp・error
- Dependency: 時間、AnimationCurve、Unity object、他moduleへ依存しない

`Add`・`Update`・`Remove`・`Clear`は変更前後のYとpoint件数を返します。重複X、非有限値、容量超過はstateを変更せず拒否します。完全一致queryは同じpointを上下端として返し、範囲外だけを端点へclampします。

## Non-goals

Bezier、接線、easing、時間所有、loop、外挿policy、Unity animation連携、singletonは対象外です。game固有のcurve意味とquery単位は利用側が所有します。

## Sample

`Piecewise Linear Curve Basics`では`(0,0)`・`(10,100)`・`(20,50)`を追加し、query 5→50、query 15→75を実Buttonで確認します。960×600では5 Button 1列、640×360では3+2列です。
