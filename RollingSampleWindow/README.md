# Rolling Sample Window

最大32件の有限sampleを固定長FIFO窓へ保持し、追加ごとの退避値と前後状態、現在のmin・max・mean・oldest・newestを返す純粋C# moduleです。

## Install

Unity Package ManagerのGit URLへ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/RollingSampleWindow#rolling-sample-window-v1.0.0
```

Unity 6000.5以降に対応します。Runtime APIはUnityEngineへ依存しません。付属sampleだけがbuilt-in UI Toolkitを使います。

## Quick start

```csharp
using GameplayMetrics;

RollingSampleWindow.TryCreate(3, out var window, out _);
window.Add(10d);
window.Add(20d);
window.Add(30d);
var result = window.Add(40d);

// result.HadEviction == true
// result.EvictedSample == 10
// result.CurrentSnapshot.Mean == 30
// oldest/newest == 20/40
```

## Contract

- Input: 容量1〜32、有限`double` sample
- State: 固定容量、oldest-first FIFO、最大32件
- Output: 追加値、退避有無と退避値、前後snapshot、count/min/max/mean/oldest/newest
- Dependency: 時刻、frame、Unity object、他moduleへ依存しない

満杯時の`Add`はoldestを1件だけ退避します。非有限sampleは`InvalidSample`としてstateを変更せず拒否します。`TryGetSampleAt`はoldest-first indexで同じ窓を再構築できます。平均はFIFO順に有限値の凸結合として計算し、単純合計の不要なoverflowを避けます。

## Non-goals

時間窓、重み付き平均、分散、percentile、補間、thread safety、永続化、singletonは対象外です。sampleの意味・単位・取得周期は利用側が所有します。

## Sample

`Rolling Sample Window Basics`では容量3へ10・20・30・40を追加し、40追加時の10退避とmean 30を実Buttonで確認した後、clearします。960×600では5 Button 1列、640×360では3+2列です。
